using EventStorming.Persistence.MongoDb.Identity;
using EventStorming.Persistence.MongoDb.Infrastructure;
using EventStorming.Teams.Model;
using EventStorming.Teams.Slices.AcceptInvitation;
using EventStorming.Teams.Slices.ChangeMemberRole;
using EventStorming.Teams.Slices.CreateInvitation;
using EventStorming.Teams.Slices.CreateTeam;
using EventStorming.Teams.Slices.GetTeam;
using EventStorming.Teams.Slices.ListInvitations;
using EventStorming.Teams.Slices.ListMyTeams;
using EventStorming.Teams.Slices.PreviewInvitation;
using EventStorming.Teams.Slices.RemoveMember;
using EventStorming.Teams.Slices.RevokeInvitation;
using MongoDB.Driver;

namespace EventStorming.Persistence.MongoDb.Teams;

internal sealed class CreateTeamStore(MongoDatabase mongo) : ICreateTeamStore
{
    public Task Insert(Team team, CancellationToken cancellationToken) =>
        TeamsCollections.TeamsIn(mongo).InsertOneAsync(MongoTeam.From(team), cancellationToken: cancellationToken);
}

internal sealed class ListMyTeamsStore(MongoDatabase mongo) : IListMyTeamsStore
{
    public async Task<IReadOnlyList<Team>> TeamsOf(Guid accountId, CancellationToken cancellationToken)
    {
        var teams = await TeamsCollections.TeamsIn(mongo)
            .Find(team => team.Members.Any(member => member.AccountId == accountId))
            .ToListAsync(cancellationToken);
        return teams.Select(team => team.ToModel()).ToList();
    }
}

internal sealed class GetTeamStore(MongoDatabase mongo) : IGetTeamStore
{
    public Task<Team?> Find(Guid teamId, CancellationToken cancellationToken) => TeamsCollections.FindTeam(mongo, teamId, cancellationToken);

    /// <summary>Anti-corruption read: the Teams context learns names and emails from Identity's accounts, nothing more.</summary>
    public async Task<IReadOnlyDictionary<Guid, MemberProfile>> Profiles(IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
    {
        var accounts = await IdentityCollections.AccountsIn(mongo)
            .Find(account => accountIds.Contains(account.Id))
            .Project(account => new { account.Id, account.DisplayName, account.Email })
            .ToListAsync(cancellationToken);
        return accounts.ToDictionary(account => account.Id, account => new MemberProfile(account.DisplayName, account.Email));
    }
}

internal sealed class CreateInvitationStore(MongoDatabase mongo) : ICreateInvitationStore
{
    public Task<Team?> FindTeam(Guid teamId, CancellationToken cancellationToken) => TeamsCollections.FindTeam(mongo, teamId, cancellationToken);

    public Task Insert(Invitation invitation, CancellationToken cancellationToken) =>
        TeamsCollections.InvitationsIn(mongo).InsertOneAsync(MongoInvitation.From(invitation), cancellationToken: cancellationToken);
}

internal sealed class ListInvitationsStore(MongoDatabase mongo) : IListInvitationsStore
{
    public Task<Team?> FindTeam(Guid teamId, CancellationToken cancellationToken) => TeamsCollections.FindTeam(mongo, teamId, cancellationToken);

    public async Task<IReadOnlyList<Invitation>> InvitationsOf(Guid teamId, CancellationToken cancellationToken)
    {
        var invitations = await TeamsCollections.InvitationsIn(mongo).Find(invitation => invitation.TeamId == teamId).ToListAsync(cancellationToken);
        return invitations.Select(invitation => invitation.ToModel()).ToList();
    }
}

internal sealed class RevokeInvitationStore(MongoDatabase mongo) : IRevokeInvitationStore
{
    public Task<Team?> FindTeam(Guid teamId, CancellationToken cancellationToken) => TeamsCollections.FindTeam(mongo, teamId, cancellationToken);

    public async Task<bool> Revoke(Guid teamId, Guid invitationId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        var invitations = TeamsCollections.InvitationsIn(mongo);
        await invitations.UpdateOneAsync(
            invitation => invitation.Id == invitationId && invitation.TeamId == teamId && invitation.RevokedAt == null,
            Builders<MongoInvitation>.Update.Set(invitation => invitation.RevokedAt, revokedAt),
            cancellationToken: cancellationToken);
        return await invitations.CountDocumentsAsync(invitation => invitation.Id == invitationId && invitation.TeamId == teamId, cancellationToken: cancellationToken) > 0;
    }
}

internal sealed class PreviewInvitationStore(MongoDatabase mongo) : IPreviewInvitationStore
{
    public async Task<InvitationWithNames?> FindByHash(string tokenHash, CancellationToken cancellationToken)
    {
        var invitation = await TeamsCollections.InvitationsIn(mongo).Find(candidate => candidate.TokenHash == tokenHash).FirstOrDefaultAsync(cancellationToken);
        if (invitation is null)
        {
            return null;
        }

        var team = await TeamsCollections.FindTeam(mongo, invitation.TeamId, cancellationToken);
        if (team is null)
        {
            return null;
        }

        var inviter = await IdentityCollections.AccountsIn(mongo)
            .Find(account => account.Id == invitation.CreatedBy)
            .Project(account => account.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
        return new InvitationWithNames(invitation.ToModel(), team.Name, inviter ?? "A team member");
    }
}

internal sealed class AcceptInvitationStore(MongoDatabase mongo) : IAcceptInvitationStore
{
    public async Task<Invitation?> FindByHash(string tokenHash, CancellationToken cancellationToken)
    {
        var invitation = await TeamsCollections.InvitationsIn(mongo).Find(candidate => candidate.TokenHash == tokenHash).FirstOrDefaultAsync(cancellationToken);
        return invitation?.ToModel();
    }

    public Task<Team?> FindTeam(Guid teamId, CancellationToken cancellationToken) => TeamsCollections.FindTeam(mongo, teamId, cancellationToken);

    public Task<string?> EmailOf(Guid accountId, CancellationToken cancellationToken) =>
        IdentityCollections.AccountsIn(mongo)
            .Find(account => account.Id == accountId)
            .Project(account => (string?)account.Email)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> AddMember(Guid teamId, Membership membership, Guid? consumeInvitationId, DateTimeOffset acceptedAt, CancellationToken cancellationToken) =>
        mongo.InTransaction(async (session, token) =>
        {
            var added = await TeamsCollections.TeamsIn(mongo).UpdateOneAsync(
                session,
                team => team.Id == teamId && !team.Members.Any(member => member.AccountId == membership.AccountId),
                Builders<MongoTeam>.Update
                    .Push(team => team.Members, MongoMembership.From(membership))
                    .Inc(team => team.Version, 1),
                cancellationToken: token);

            if (added.ModifiedCount == 1 && consumeInvitationId is { } invitationId)
            {
                await TeamsCollections.InvitationsIn(mongo).UpdateOneAsync(
                    session,
                    invitation => invitation.Id == invitationId,
                    Builders<MongoInvitation>.Update
                        .Set(invitation => invitation.AcceptedAt, acceptedAt)
                        .Set(invitation => invitation.AcceptedBy, membership.AccountId),
                    cancellationToken: token);
            }

            return added.ModifiedCount == 1;
        }, cancellationToken);
}

internal sealed class ChangeMemberRoleStore(MongoDatabase mongo) : IChangeMemberRoleStore
{
    public Task<Team?> FindTeam(Guid teamId, CancellationToken cancellationToken) => TeamsCollections.FindTeam(mongo, teamId, cancellationToken);

    public async Task<bool> SetRole(Guid teamId, long expectedVersion, Guid accountId, TeamRole role, CancellationToken cancellationToken)
    {
        var result = await TeamsCollections.TeamsIn(mongo).UpdateOneAsync(
            team => team.Id == teamId && team.Version == expectedVersion && team.Members.Any(member => member.AccountId == accountId),
            Builders<MongoTeam>.Update
                // "$" is the member the filter matched; roles are stored by name.
                .Set("members.$.role", role.ToString())
                .Inc(team => team.Version, 1),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }
}

internal sealed class RemoveMemberStore(MongoDatabase mongo) : IRemoveMemberStore
{
    public Task<Team?> FindTeam(Guid teamId, CancellationToken cancellationToken) => TeamsCollections.FindTeam(mongo, teamId, cancellationToken);

    public async Task<bool> Remove(Guid teamId, long expectedVersion, Guid accountId, CancellationToken cancellationToken)
    {
        var result = await TeamsCollections.TeamsIn(mongo).UpdateOneAsync(
            team => team.Id == teamId && team.Version == expectedVersion,
            Builders<MongoTeam>.Update
                .PullFilter(team => team.Members, member => member.AccountId == accountId)
                .Inc(team => team.Version, 1),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }
}
