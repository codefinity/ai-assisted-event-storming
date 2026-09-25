using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Teams.Slices.GetTeam;

public sealed record GetTeamQuery(Actor Actor, Guid TeamId);

public sealed class GetTeamResult : IUseCaseResult
{
    private GetTeamResult(TeamDetails? team, IReadOnlyList<Failure> failures)
    {
        Team = team;
        Failures = failures;
    }

    public TeamDetails? Team { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static GetTeamResult Succeeded(TeamDetails team) => new(team, []);

    public static GetTeamResult Failed(Failure failure) => new(null, [failure]);
}

public interface IGetTeamQueryHandler
{
    Task<GetTeamResult> Handle(GetTeamQuery query, CancellationToken cancellationToken);
}

public interface IGetTeamStore
{
    Task<Team?> Find(Guid teamId, CancellationToken cancellationToken);

    /// <summary>Display names and emails for the members - read through to the Identity context's accounts.</summary>
    Task<IReadOnlyDictionary<Guid, MemberProfile>> Profiles(IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken);
}
