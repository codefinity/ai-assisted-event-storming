using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Teams.Slices.ListMyTeams;

public sealed record ListMyTeamsQuery(Actor Actor);

public sealed class ListMyTeamsResult : IUseCaseResult
{
    private ListMyTeamsResult(IReadOnlyList<TeamSummary> teams, IReadOnlyList<Failure> failures)
    {
        Teams = teams;
        Failures = failures;
    }

    public IReadOnlyList<TeamSummary> Teams { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static ListMyTeamsResult Succeeded(IReadOnlyList<TeamSummary> teams) => new(teams, []);

    public static ListMyTeamsResult Failed(Failure failure) => new([], [failure]);
}

public interface IListMyTeamsQueryHandler
{
    Task<ListMyTeamsResult> Handle(ListMyTeamsQuery query, CancellationToken cancellationToken);
}

public interface IListMyTeamsStore
{
    Task<IReadOnlyList<Team>> TeamsOf(Guid accountId, CancellationToken cancellationToken);
}
