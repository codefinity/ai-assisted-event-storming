using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Teams.Slices.ListInvitations;

public sealed class ListInvitationsQueryHandler(IListInvitationsStore store, IClock clock) : IListInvitationsQueryHandler
{
    public async Task<ListInvitationsResult> Handle(ListInvitationsQuery query, CancellationToken cancellationToken)
    {
        var team = await store.FindTeam(query.TeamId, cancellationToken);
        var myRole = team is null || query.Actor.Kind != ActorKind.Account ? null : team.RoleOf(query.Actor.Id);
        if (team is null || myRole is null)
        {
            return ListInvitationsResult.Failed(Failures.NotFound("The team"));
        }

        if (myRole != TeamRole.Owner)
        {
            return ListInvitationsResult.Failed(Failures.Forbidden("Only a team Owner can see its invitations."));
        }

        var now = clock.UtcNow;
        var invitations = await store.InvitationsOf(team.Id, cancellationToken);
        return ListInvitationsResult.Succeeded(invitations
            .Where(invitation => InvitationStatuses.Of(invitation, now) == InvitationStatus.Valid)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .Select(InvitationView.From)
            .ToList());
    }
}
