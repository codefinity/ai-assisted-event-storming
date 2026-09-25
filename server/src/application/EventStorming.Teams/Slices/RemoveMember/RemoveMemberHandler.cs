using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Teams.Slices.RemoveMember;

public sealed class RemoveMemberCommandHandler(IRemoveMemberStore store) : IRemoveMemberCommandHandler
{
    public async Task<RemoveMemberResult> Handle(RemoveMemberCommand command, CancellationToken cancellationToken)
    {
        var team = await store.FindTeam(command.TeamId, cancellationToken);
        var myRole = team is null || command.Actor.Kind != ActorKind.Account ? null : team.RoleOf(command.Actor.Id);
        if (team is null || myRole is null)
        {
            return RemoveMemberResult.Failed(Failures.NotFound("The team"));
        }

        var leaving = command.AccountId == command.Actor.Id;
        if (!leaving && myRole != TeamRole.Owner)
        {
            return RemoveMemberResult.Failed(Failures.Forbidden("Only a team Owner can remove other members."));
        }

        var targetRole = team.RoleOf(command.AccountId);
        if (targetRole is null)
        {
            return RemoveMemberResult.Failed(Failures.NotFound("The member"));
        }

        if (targetRole == TeamRole.Owner && team.OwnerCount() == 1)
        {
            return RemoveMemberResult.Failed(Failures.Conflict(
                "last-owner",
                leaving ? "The last Owner cannot leave the team." : "The last Owner cannot be removed.",
                fix: "Make another member an Owner first."));
        }

        return await store.Remove(team.Id, team.Version, command.AccountId, cancellationToken)
            ? RemoveMemberResult.Succeeded()
            : RemoveMemberResult.Failed(Failures.Conflict("team-changed", "The team changed while the member was being removed.", fix: "Reload the team and try again."));
    }
}
