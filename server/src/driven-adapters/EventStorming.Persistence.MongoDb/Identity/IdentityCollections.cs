using EventStorming.Identity.Model;
using EventStorming.Persistence.MongoDb.Infrastructure;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace EventStorming.Persistence.MongoDb.Identity;

internal sealed class MongoAccount
{
    [BsonId]
    public Guid Id { get; init; }

    public required string Email { get; init; }

    public required string DisplayName { get; init; }

    public required string PasswordHash { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public Account ToModel() => new(Id, Email, DisplayName, PasswordHash, CreatedAt);

    public static MongoAccount From(Account account) => new()
    {
        Id = account.Id,
        Email = account.Email,
        DisplayName = account.DisplayName,
        PasswordHash = account.PasswordHash,
        CreatedAt = account.CreatedAt,
    };
}

internal sealed class MongoRefreshToken
{
    [BsonId]
    public Guid Id { get; init; }

    public Guid AccountId { get; init; }

    public Guid FamilyId { get; init; }

    public required string TokenHash { get; init; }

    public DateTimeOffset IssuedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public Guid? ReplacedById { get; init; }

    public DateTimeOffset? RotatedAt { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }

    public string? RevokedReason { get; init; }

    public RefreshToken ToModel() =>
        new(Id, AccountId, FamilyId, TokenHash, IssuedAt, ExpiresAt, ReplacedById, RotatedAt, RevokedAt, RevokedReason);

    public static MongoRefreshToken From(RefreshToken token) => new()
    {
        Id = token.Id,
        AccountId = token.AccountId,
        FamilyId = token.FamilyId,
        TokenHash = token.TokenHash,
        IssuedAt = token.IssuedAt,
        ExpiresAt = token.ExpiresAt,
        ReplacedById = token.ReplacedById,
        RotatedAt = token.RotatedAt,
        RevokedAt = token.RevokedAt,
        RevokedReason = token.RevokedReason,
    };
}

internal static class IdentityCollections
{
    public const string Accounts = "accounts";
    public const string RefreshTokens = "refreshTokens";

    public static IMongoCollection<MongoAccount> AccountsIn(MongoDatabase mongo) => mongo.Collection<MongoAccount>(Accounts);

    public static IMongoCollection<MongoRefreshToken> RefreshTokensIn(MongoDatabase mongo) => mongo.Collection<MongoRefreshToken>(RefreshTokens);

    public static async Task EnsureIndexes(MongoDatabase mongo, CancellationToken cancellationToken)
    {
        await AccountsIn(mongo).Indexes.CreateOneAsync(
            new CreateIndexModel<MongoAccount>(
                Builders<MongoAccount>.IndexKeys.Ascending(account => account.Email),
                new CreateIndexOptions { Unique = true, Name = "email_unique" }),
            cancellationToken: cancellationToken);

        var tokens = RefreshTokensIn(mongo);
        await tokens.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<MongoRefreshToken>(
                    Builders<MongoRefreshToken>.IndexKeys.Ascending(token => token.TokenHash),
                    new CreateIndexOptions { Unique = true, Name = "tokenHash_unique" }),
                new CreateIndexModel<MongoRefreshToken>(
                    Builders<MongoRefreshToken>.IndexKeys.Ascending(token => token.FamilyId),
                    new CreateIndexOptions { Name = "familyId" }),
                new CreateIndexModel<MongoRefreshToken>(
                    Builders<MongoRefreshToken>.IndexKeys.Ascending(token => token.ExpiresAt),
                    new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "expiresAt_ttl" }),
            ],
            cancellationToken);
    }

    public static Task RevokeFamily(MongoDatabase mongo, Guid familyId, string reason, DateTimeOffset revokedAt, CancellationToken cancellationToken) =>
        RefreshTokensIn(mongo).UpdateManyAsync(
            token => token.FamilyId == familyId && token.RevokedAt == null,
            Builders<MongoRefreshToken>.Update
                .Set(token => token.RevokedAt, revokedAt)
                .Set(token => token.RevokedReason, reason),
            cancellationToken: cancellationToken);
}
