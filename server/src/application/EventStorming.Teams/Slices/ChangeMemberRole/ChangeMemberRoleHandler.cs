using EventStorming.SharedKernel;
using EventStorming.Teams.Model;
using FluentValidation;

namespace EventStorming.Teams.Slices.ChangeMemberRole;

public sealed class ChangeMemberRoleCommandHandler(
    IValidator<ChangeMemberRoleCommand> validator,
    IChangeMemberRoleStore store) : IChangeMemberRoleCommandHandler
{
    public async Task<ChangeMemberRoleResult> Handle(ChangeMemberRoleCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return ChangeMemberRoleResult.Failed(validation.ToFailures());
        }

        var team = await store.FindTeam(command.TeamId, cancellationToken);
        var myRole = team is null || command.Actor.Kind != ActorKind.Account ? null : team.RoleOf(command.Actor.Id);
        if (team is null || myRole is null)
        {
            return ChangeMemberRoleResult.Failed(Failures.NotFound("The team"));
        }

        if (myRole != TeamRole.Owner)
        {
            return ChangeMemberRoleResult.Failed(Failures.Forbidden("Only a team Owner can change roles."));
        }

        var currentRole = team.RoleOf(command.AccountId);
        if (currentRole is null)
        {
            return ChangeMemberRoleResult.Failed(Failures.NotFound("The member"));
        }

        var newRole = TeamRoles.Parse(command.Role);
        if (currentRole == newRole)
        {
            return ChangeMemberRoleResult.Succeeded();
        }

        if (currentRole == TeamRole.Owner && team.OwnerCount() == 1)
        {
            return ChangeMemberRoleResult.Failed(Failures.Conflict(
                "last-owner",
                "A team must always have at least one Owner.",
                "role",
                "Make another member an Owner first."));
        }

        return await store.SetRole(team.Id, team.Version, command.AccountId, newRole, cancellationToken)
            ? ChangeMemberRoleResult.Succeeded()
            : ChangeMemberRoleResult.Failed(Failures.Conflict("team-changed", "The team changed while the role was being updated.", fix: "Reload the team and try again."));
    }
}

public sealed class ChangeMemberRoleCommandValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    public ChangeMemberRoleCommandValidator()
    {
        RuleFor(command => command.Role)
            .Must(TeamRoles.IsValid).WithErrorCode("unknown-role").WithMessage("The role must be owner, editor or viewer.")
            .WithFix("Send one of: owner, editor, viewer.");
    }
}
