using EventStorming.Teams.Model;
using EventStorming.Teams.Shared;
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

namespace EventStorming.Specs.Support.Fakes;

/// <summary>Every Teams store port over two lists, reading account names through the Identity fake like the real ACL does.</summary>
public sealed class InMemoryTeams(InMemoryIdentity identity) :
    ICreateTeamStore,
    IListMyTeamsStore,
    IGetTeamStore,
    ICreateInvitationStore,
    IListInvitationsStore,
    IRevokeInvitationStore,
    IPreviewInvitationStore,
    IAcceptInvitationStore,
    IChangeMemberRoleStore,
    IRemoveMemberStore
{
    public List<Team> Teams { get; } = [];

    public List<Invitation> Invitations { get; } = [];

    public Team Named(string name) => Teams.Single(team => team.Name == name);

    public Task Insert(Team team, CancellationToken cancellationToken)
    {
        Teams.Add(team);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Team>> TeamsOf(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Team>>(Teams.Where(team => team.Members.Any(member => member.AccountId == accountId)).ToList());

    public Task<Team?> Find(Guid teamId, CancellationToken cancellationToken) => FindTeam(teamId, cancellationToken);

    public Task<Team?> FindTeam(Guid teamId, CancellationToken cancellationToken) =>
        Task.FromResult(Teams.FirstOrDefault(team => team.Id == teamId));

    public Task<IReadOnlyDictionary<Guid, MemberProfile>> Profiles(IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, MemberProfile>>(identity.Accounts
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionary(account => account.Id, account => new MemberProfile(account.DisplayName, account.Email)));

    public Task Insert(Invitation invitation, CancellationToken cancellationToken)
    {
        Invitations.Add(invitation);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Invitation>> InvitationsOf(Guid teamId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Invitation>>(Invitations.Where(invitation => invitation.TeamId == teamId).ToList());

    public Task<bool> Revoke(Guid teamId, Guid invitationId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        var index = Invitations.FindIndex(invitation => invitation.Id == invitationId && invitation.TeamId == teamId);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        Invitations[index] = Invitations[index] with { RevokedAt = Invitations[index].RevokedAt ?? revokedAt };
        return Task.FromResult(true);
    }

    Task<InvitationWithNames?> IPreviewInvitationStore.FindByHash(string tokenHash, CancellationToken cancellationToken)
    {
        var invitation = Invitations.FirstOrDefault(candidate => candidate.TokenHash == tokenHash);
        var team = invitation is null ? null : Teams.FirstOrDefault(candidate => candidate.Id == invitation.TeamId);
        return Task.FromResult(invitation is null || team is null
            ? null
            : new InvitationWithNames(invitation, team.Name, identity.Accounts.FirstOrDefault(account => account.Id == invitation.CreatedBy)?.DisplayName ?? "A team member"));
    }

    Task<Invitation?> IAcceptInvitationStore.FindByHash(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Invitations.FirstOrDefault(candidate => candidate.TokenHash == tokenHash));

    public Task<string?> EmailOf(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult(identity.Accounts.FirstOrDefault(account => account.Id == accountId)?.Email);

    public Task<bool> AddMember(Guid teamId, Membership membership, Guid? consumeInvitationId, DateTimeOffset acceptedAt, CancellationToken cancellationToken)
    {
        var index = Teams.FindIndex(team => team.Id == teamId);
        if (index < 0 || Teams[index].Members.Any(member => member.AccountId == membership.AccountId))
        {
            return Task.FromResult(false);
        }

        Teams[index] = Teams[index] with { Members = [.. Teams[index].Members, membership], Version = Teams[index].Version + 1 };
        if (consumeInvitationId is { } invitationId)
        {
            var invitation = Invitations.FindIndex(candidate => candidate.Id == invitationId);
            Invitations[invitation] = Invitations[invitation] with { AcceptedAt = acceptedAt, AcceptedBy = membership.AccountId };
        }

        return Task.FromResult(true);
    }

    public Task<bool> SetRole(Guid teamId, long expectedVersion, Guid accountId, TeamRole role, CancellationToken cancellationToken) =>
        Change(teamId, expectedVersion, members => members.Select(member => member.AccountId == accountId ? member with { Role = role } : member).ToList());

    public Task<bool> Remove(Guid teamId, long expectedVersion, Guid accountId, CancellationToken cancellationToken) =>
        Change(teamId, expectedVersion, members => members.Where(member => member.AccountId != accountId).ToList());

    private Task<bool> Change(Guid teamId, long expectedVersion, Func<IReadOnlyList<Membership>, IReadOnlyList<Membership>> change)
    {
        var index = Teams.FindIndex(team => team.Id == teamId && team.Version == expectedVersion);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        Teams[index] = Teams[index] with { Members = change(Teams[index].Members), Version = Teams[index].Version + 1 };
        return Task.FromResult(true);
    }
}

public sealed class RecordingMailer : IInvitationMailer
{
    public List<InvitationMail> Sent { get; } = [];

    public Task Send(InvitationMail mail, CancellationToken cancellationToken)
    {
        Sent.Add(mail);
        return Task.CompletedTask;
    }
}
