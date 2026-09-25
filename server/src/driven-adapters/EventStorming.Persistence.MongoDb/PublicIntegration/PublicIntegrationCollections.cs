using EventStorming.Persistence.MongoDb.Infrastructure;
using EventStorming.PublicIntegration.Model;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace EventStorming.Persistence.MongoDb.PublicIntegration;

internal sealed class MongoApiKey
{
    [BsonId]
    public Guid Id { get; init; }

    public Guid TeamId { get; init; }

    public required string Name { get; init; }

    public required string DisplayPrefix { get; init; }

    public required string SecretHash { get; init; }

    public List<string> Scopes { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }

    public Guid CreatedBy { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }

    public ApiKey ToModel() => new(Id, TeamId, Name, DisplayPrefix, SecretHash, Scopes, CreatedAt, CreatedBy, ExpiresAt, LastUsedAt, RevokedAt);

    public static MongoApiKey From(ApiKey key) => new()
    {
        Id = key.Id,
        TeamId = key.TeamId,
        Name = key.Name,
        DisplayPrefix = key.DisplayPrefix,
        SecretHash = key.SecretHash,
        Scopes = key.Scopes.ToList(),
        CreatedAt = key.CreatedAt,
        CreatedBy = key.CreatedBy,
        ExpiresAt = key.ExpiresAt,
        LastUsedAt = key.LastUsedAt,
        RevokedAt = key.RevokedAt,
    };
}

internal sealed class MongoStoredResponse
{
    public int Status { get; init; }

    public required string ContentType { get; init; }

    public required string Body { get; init; }

    public string? Location { get; init; }
}

internal sealed class MongoIdempotencyRecord
{
    [BsonId]
    public required string Id { get; init; }

    public required string RequestHash { get; init; }

    public bool Completed { get; init; }

    public MongoStoredResponse? Response { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public IdempotencyRecord ToModel() => new(
        Id,
        RequestHash,
        Completed,
        Response is null ? null : new StoredResponse(Response.Status, Response.ContentType, Response.Body, Response.Location),
        CreatedAt,
        ExpiresAt);

    public static MongoIdempotencyRecord From(IdempotencyRecord record) => new()
    {
        Id = record.Id,
        RequestHash = record.RequestHash,
        Completed = record.Completed,
        Response = record.Response is null ? null : From(record.Response),
        CreatedAt = record.CreatedAt,
        ExpiresAt = record.ExpiresAt,
    };

    public static MongoStoredResponse From(StoredResponse response) => new()
    {
        Status = response.Status,
        ContentType = response.ContentType,
        Body = response.Body,
        Location = response.Location,
    };
}

internal static class PublicIntegrationCollections
{
    public const string ApiKeys = "apiKeys";
    public const string IdempotencyRecords = "idempotencyRecords";

    public static IMongoCollection<MongoApiKey> ApiKeysIn(MongoDatabase mongo) => mongo.Collection<MongoApiKey>(ApiKeys);

    public static IMongoCollection<MongoIdempotencyRecord> IdempotencyIn(MongoDatabase mongo) => mongo.Collection<MongoIdempotencyRecord>(IdempotencyRecords);

    public static async Task EnsureIndexes(MongoDatabase mongo, CancellationToken cancellationToken)
    {
        await ApiKeysIn(mongo).Indexes.CreateOneAsync(
            new CreateIndexModel<MongoApiKey>(
                Builders<MongoApiKey>.IndexKeys.Ascending(key => key.TeamId),
                new CreateIndexOptions { Name = "teamId" }),
            cancellationToken: cancellationToken);

        await IdempotencyIn(mongo).Indexes.CreateOneAsync(
            new CreateIndexModel<MongoIdempotencyRecord>(
                Builders<MongoIdempotencyRecord>.IndexKeys.Ascending(record => record.ExpiresAt),
                new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "expiresAt_ttl" }),
            cancellationToken: cancellationToken);
    }
}
