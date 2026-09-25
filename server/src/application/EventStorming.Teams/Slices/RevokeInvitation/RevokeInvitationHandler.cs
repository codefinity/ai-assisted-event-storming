using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Teams.Slices.RevokeInvitation;

public sealed class RevokeInvitationCommandHandler(IRevokeInvitationStore store, IClock clock) : IRevokeInvitationCommandHandler
{
    public async Task<RevokeInvitationResult> Handle(RevokeInvitationCommand command, CancellationToken cancellationToken)
    {
        var team = await store.FindTeam(command.TeamId, cancellationToken);
        var myRole = team is null || command.Actor.Kind != ActorKind.Account ? null : team.RoleOf(command.Actor.Id);
        if (team is null || myRole is null)
        {
            return RevokeInvitationResult.Failed(Failures.NotFound("The team"));
        }

        if (myRole != TeamRole.Owner)
        {
            return RevokeInvitationResult.Failed(Failures.Forbidden("Only a team Owner can revoke invitations."));
        }

        return await store.Revoke(team.Id, command.InvitationId, clock.UtcNow, cancellationToken)
            ? RevokeInvitationResult.Succeeded()
            : RevokeInvitationResult.Failed(Failures.NotFound("The invitation"));
    }
}
