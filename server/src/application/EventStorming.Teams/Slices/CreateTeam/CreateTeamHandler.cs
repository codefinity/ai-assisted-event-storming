using EventStorming.SharedKernel;
using EventStorming.Teams.Model;
using FluentValidation;

namespace EventStorming.Teams.Slices.CreateTeam;

public sealed class CreateTeamCommandHandler(
    IValidator<CreateTeamCommand> validator,
    ICreateTeamStore store,
    IClock clock) : ICreateTeamCommandHandler
{
    public async Task<CreateTeamResult> Handle(CreateTeamCommand command, CancellationToken cancellationToken)
    {
        if (command.Actor.Kind != ActorKind.Account)
        {
            return CreateTeamResult.Failed(Failures.Forbidden("Teams are created by people, not by API keys."));
        }

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return CreateTeamResult.Failed(validation.ToFailures());
        }

        var now = clock.UtcNow;
        var team = new Team(
            Guid.CreateVersion7(),
            command.Name.Trim(),
            now,
            command.Actor.Id,
            [new Membership(command.Actor.Id, TeamRole.Owner, now)],
            Version: 1);

        await store.Insert(team, cancellationToken);
        return CreateTeamResult.Succeeded(new TeamSummary(team.Id, team.Name, TeamRole.Owner, 1));
    }
}

public sealed class CreateTeamCommandValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamCommandValidator()
    {
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode("required").WithMessage("A team needs a name.").WithFix("Send a name such as \"Checkout squad\".")
            .MaximumLength(80).WithErrorCode("too-long").WithMessage("A team name must be at most 80 characters.");
    }
}
