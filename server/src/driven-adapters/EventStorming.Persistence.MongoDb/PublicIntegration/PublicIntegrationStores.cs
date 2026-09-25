using EventStorming.Persistence.MongoDb.Infrastructure;
using EventStorming.PublicIntegration.Model;
using EventStorming.PublicIntegration.Slices.AuthenticateApiKey;
using EventStorming.PublicIntegration.Slices.CreateApiKey;
using EventStorming.PublicIntegration.Slices.ListApiKeys;
using EventStorming.PublicIntegration.Slices.RecordIdempotentResponse;
using EventStorming.PublicIntegration.Slices.ReserveIdempotencyKey;
using EventStorming.PublicIntegration.Slices.RevokeApiKey;
using MongoDB.Driver;

namespace EventStorming.Persistence.MongoDb.PublicIntegration;

internal sealed class ApiKeyStores(MongoDatabase mongo) :
    ICreateApiKeyStore,
    IListApiKeysStore,
    IRevokeApiKeyStore,
    IAuthenticateApiKeyStore
{
    public Task Insert(ApiKey key, CancellationToken cancellationToken) =>
        PublicIntegrationCollections.ApiKeysIn(mongo).InsertOneAsync(MongoApiKey.From(key), cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<ApiKey>> KeysOf(Guid teamId, CancellationToken cancellationToken)
    {
        var keys = await PublicIntegrationCollections.ApiKeysIn(mongo).Find(key => key.TeamId == teamId).ToListAsync(cancellationToken);
        return keys.Select(key => key.ToModel()).ToList();
    }

    public async Task<ApiKey?> Revoke(Guid teamId, Guid keyId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        var keys = PublicIntegrationCollections.ApiKeysIn(mongo);
        await keys.UpdateOneAsync(
            key => key.Id == keyId && key.TeamId == teamId && key.RevokedAt == null,
            Builders<MongoApiKey>.Update.Set(key => key.RevokedAt, revokedAt),
            cancellationToken: cancellationToken);
        var key = await keys.Find(candidate => candidate.Id == keyId && candidate.TeamId == teamId).FirstOrDefaultAsync(cancellationToken);
        return key?.ToModel();
    }

    public async Task<ApiKey?> Find(Guid keyId, CancellationToken cancellationToken)
    {
        var key = await PublicIntegrationCollections.ApiKeysIn(mongo).Find(candidate => candidate.Id == keyId).FirstOrDefaultAsync(cancellationToken);
        return key?.ToModel();
    }

    public Task TouchLastUsed(Guid keyId, DateTimeOffset usedAt, CancellationToken cancellationToken) =>
        PublicIntegrationCollections.ApiKeysIn(mongo).UpdateOneAsync(
            key => key.Id == keyId,
            Builders<MongoApiKey>.Update.Set(key => key.LastUsedAt, usedAt),
            cancellationToken: cancellationToken);
}

internal sealed class IdempotencyStores(MongoDatabase mongo) : IReserveIdempotencyKeyStore, IRecordIdempotentResponseStore
{
    public async Task<IdempotencyRecord?> TryInsert(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        var records = PublicIntegrationCollections.IdempotencyIn(mongo);
        try
        {
            await records.InsertOneAsync(MongoIdempotencyRecord.From(record), cancellationToken: cancellationToken);
            return null;
        }
        catch (MongoException exception) when (MongoErrors.IsDuplicateKey(exception))
        {
            var existing = await records.Find(candidate => candidate.Id == record.Id).FirstOrDefaultAsync(cancellationToken);
            return existing?.ToModel();
        }
    }

    public Task Replace(IdempotencyRecord record, CancellationToken cancellationToken) =>
        PublicIntegrationCollections.IdempotencyIn(mongo).ReplaceOneAsync(
            candidate => candidate.Id == record.Id,
            MongoIdempotencyRecord.From(record),
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

    public Task Complete(string recordId, StoredResponse response, CancellationToken cancellationToken) =>
        PublicIntegrationCollections.IdempotencyIn(mongo).UpdateOneAsync(
            candidate => candidate.Id == recordId,
            Builders<MongoIdempotencyRecord>.Update
                .Set(candidate => candidate.Completed, true)
                .Set(candidate => candidate.Response, MongoIdempotencyRecord.From(response)),
            cancellationToken: cancellationToken);

    public Task Release(string recordId, CancellationToken cancellationToken) =>
        PublicIntegrationCollections.IdempotencyIn(mongo).DeleteOneAsync(candidate => candidate.Id == recordId, cancellationToken);
}
