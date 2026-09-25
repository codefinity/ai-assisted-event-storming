using EventStorming.PublicIntegration.Model;
using EventStorming.PublicIntegration.Slices.AuthenticateApiKey;
using EventStorming.PublicIntegration.Slices.CreateApiKey;
using EventStorming.PublicIntegration.Slices.ListApiKeys;
using EventStorming.PublicIntegration.Slices.RecordIdempotentResponse;
using EventStorming.PublicIntegration.Slices.ReserveIdempotencyKey;
using EventStorming.PublicIntegration.Slices.RevokeApiKey;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Shouldly;
using Xunit;

namespace EventStorming.Persistence.MongoDb.IntegrationTests;

public sealed class PublicIntegrationStoreTests(MongoContainer mongo)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Api_keys_are_stored_listed_touched_and_revoked()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var teamId = Guid.NewGuid();
        var key = new ApiKey(Guid.NewGuid(), teamId, "Claude", "es_abc…", "secret-hash", ["read", "write"], Now, Guid.NewGuid());
        await scope.GetRequiredService<ICreateApiKeyStore>().Insert(key, CancellationToken.None);

        (await scope.GetRequiredService<IListApiKeysStore>().KeysOf(teamId, CancellationToken.None)).ShouldHaveSingleItem().Scopes.ShouldBe(["read", "write"]);

        var authenticate = scope.GetRequiredService<IAuthenticateApiKeyStore>();
        await authenticate.TouchLastUsed(key.Id, Now.AddMinutes(5), CancellationToken.None);
        (await authenticate.Find(key.Id, CancellationToken.None))!.LastUsedAt.ShouldBe(Now.AddMinutes(5));

        var revoke = scope.GetRequiredService<IRevokeApiKeyStore>();
        (await revoke.Revoke(Guid.NewGuid(), key.Id, Now, CancellationToken.None)).ShouldBeNull("another team cannot revoke it");
        (await revoke.Revoke(teamId, key.Id, Now, CancellationToken.None))!.RevokedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task An_idempotency_key_is_reserved_once_then_completed_or_released()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var reserve = scope.GetRequiredService<IReserveIdempotencyKeyStore>();
        var record = scope.GetRequiredService<IRecordIdempotentResponseStore>();
        var reservation = new IdempotencyRecord("key-1", "hash", false, null, Now, Now.AddHours(24));

        (await reserve.TryInsert(reservation, CancellationToken.None)).ShouldBeNull();
        (await reserve.TryInsert(reservation, CancellationToken.None)).ShouldNotBeNull().Completed.ShouldBeFalse();

        await record.Complete("key-1", new StoredResponse(201, "application/json", "{}", "/api/v1/boards/1"), CancellationToken.None);
        var completed = (await reserve.TryInsert(reservation, CancellationToken.None)).ShouldNotBeNull();
        completed.Response.ShouldNotBeNull().Location.ShouldBe("/api/v1/boards/1");

        await record.Release("key-1", CancellationToken.None);
        (await reserve.TryInsert(reservation, CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task Startup_creates_the_unique_and_expiry_indexes()
    {
        await using var provider = await mongo.NewStores();
        var database = provider.GetRequiredService<IMongoClient>().GetDatabase(provider.GetRequiredService<MongoOptions>().Database);

        async Task<List<BsonDocument>> IndexesOf(string collection) =>
            await (await database.GetCollection<BsonDocument>(collection).Indexes.ListAsync()).ToListAsync();

        (await IndexesOf("accounts")).ShouldContain(index => index["name"] == "email_unique" && index["unique"] == true);
        (await IndexesOf("refreshTokens")).ShouldContain(index => index["name"] == "expiresAt_ttl");
        (await IndexesOf("connections")).ShouldContain(index => index["name"] == "board_endpoints_unique");
        (await IndexesOf("idempotencyRecords")).ShouldContain(index => index["name"] == "expiresAt_ttl");
        (await IndexesOf("invitations")).ShouldContain(index => index["name"] == "tokenHash_unique");
    }
}
