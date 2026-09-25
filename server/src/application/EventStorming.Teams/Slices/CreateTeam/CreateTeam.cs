using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Teams.Slices.CreateTeam;

public sealed record CreateTeamCommand(Actor Actor, string Name);

public sealed class CreateTeamResult : IUseCaseResult
{
    private CreateTeamResult(TeamSummary? team, IReadOnlyList<Failure> failures)
    {
        Team = team;
        Failures = failures;
    }

    public TeamSummary? Team { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static CreateTeamResult Succeeded(TeamSummary team) => new(team, []);

    public static CreateTeamResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static CreateTeamResult Failed(Failure failure) => new(null, [failure]);
}

public interface ICreateTeamCommandHandler
{
    Task<CreateTeamResult> Handle(CreateTeamCommand command, CancellationToken cancellationToken);
}

public interface ICreateTeamStore
{
    Task Insert(Team team, CancellationToken cancellationToken);
}
