using EventStorming.Persistence.MongoDb.Infrastructure;
using EventStorming.Teams.Model;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace EventStorming.Persistence.MongoDb.Teams;

internal sealed class MongoMembership
{
    public Guid AccountId { get; init; }

    public TeamRole Role { get; init; }

    public DateTimeOffset JoinedAt { get; init; }

    public Membership ToModel() => new(AccountId, Role, JoinedAt);

    public static MongoMembership From(Membership membership) =>
        new() { AccountId = membership.AccountId, Role = membership.Role, JoinedAt = membership.JoinedAt };
}

internal sealed class MongoTeam
{
    [BsonId]
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public Guid CreatedBy { get; init; }

    public List<MongoMembership> Members { get; init; } = [];

    public long Version { get; init; }

    public Team ToModel() => new(Id, Name, CreatedAt, CreatedBy, Members.Select(member => member.ToModel()).ToList(), Version);

    public static MongoTeam From(Team team) => new()
    {
        Id = team.Id,
        Name = team.Name,
        CreatedAt = team.CreatedAt,
        CreatedBy = team.CreatedBy,
        Members = team.Members.Select(MongoMembership.From).ToList(),
        Version = team.Version,
    };
}

internal sealed class MongoInvitation
{
    [BsonId]
    public Guid Id { get; init; }

    public Guid TeamId { get; init; }

    public InvitationKind Kind { get; init; }

    public string? Email { get; init; }

    public TeamRole Role { get; init; }

    public required string TokenHash { get; init; }

    public Guid CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? AcceptedAt { get; init; }

    public Guid? AcceptedBy { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }

    public Invitation ToModel() =>
        new(Id, TeamId, Kind, Email, Role, TokenHash, CreatedBy, CreatedAt, ExpiresAt, AcceptedAt, AcceptedBy, RevokedAt);

    public static MongoInvitation From(Invitation invitation) => new()
    {
        Id = invitation.Id,
        TeamId = invitation.TeamId,
        Kind = invitation.Kind,
        Email = invitation.Email,
        Role = invitation.Role,
        TokenHash = invitation.TokenHash,
        CreatedBy = invitation.CreatedBy,
        CreatedAt = invitation.CreatedAt,
        ExpiresAt = invitation.ExpiresAt,
        AcceptedAt = invitation.AcceptedAt,
        AcceptedBy = invitation.AcceptedBy,
        RevokedAt = invitation.RevokedAt,
    };
}

internal static class TeamsCollections
{
    public const string Teams = "teams";
    public const string Invitations = "invitations";

    public static IMongoCollection<MongoTeam> TeamsIn(MongoDatabase mongo) => mongo.Collection<MongoTeam>(Teams);

    public static IMongoCollection<MongoInvitation> InvitationsIn(MongoDatabase mongo) => mongo.Collection<MongoInvitation>(Invitations);

    public static async Task EnsureIndexes(MongoDatabase mongo, CancellationToken cancellationToken)
    {
        await TeamsIn(mongo).Indexes.CreateOneAsync(
            new CreateIndexModel<MongoTeam>(
                Builders<MongoTeam>.IndexKeys.Ascending("members.accountId"),
                new CreateIndexOptions { Name = "members_accountId" }),
            cancellationToken: cancellationToken);

        await InvitationsIn(mongo).Indexes.CreateManyAsync(
            [
                new CreateIndexModel<MongoInvitation>(
                    Builders<MongoInvitation>.IndexKeys.Ascending(invitation => invitation.TokenHash),
                    new CreateIndexOptions { Unique = true, Name = "tokenHash_unique" }),
                new CreateIndexModel<MongoInvitation>(
                    Builders<MongoInvitation>.IndexKeys.Ascending(invitation => invitation.TeamId),
                    new CreateIndexOptions { Name = "teamId" }),
            ],
            cancellationToken);
    }

    public static async Task<Team?> FindTeam(MongoDatabase mongo, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await TeamsIn(mongo).Find(candidate => candidate.Id == teamId).FirstOrDefaultAsync(cancellationToken);
        return team?.ToModel();
    }
}
