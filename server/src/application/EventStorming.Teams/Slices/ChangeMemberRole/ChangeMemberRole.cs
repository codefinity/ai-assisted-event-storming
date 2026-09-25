using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Teams.Slices.ChangeMemberRole;

public sealed record ChangeMemberRoleCommand(Actor Actor, Guid TeamId, Guid AccountId, string Role);

public sealed class ChangeMemberRoleResult : IUseCaseResult
{
    private ChangeMemberRoleResult(IReadOnlyList<Failure> failures) => Failures = failures;

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static ChangeMemberRoleResult Succeeded() => new([]);

    public static ChangeMemberRoleResult Failed(IReadOnlyList<Failure> failures) => new(failures);

    public static ChangeMemberRoleResult Failed(Failure failure) => new([failure]);
}

public interface IChangeMemberRoleCommandHandler
{
    Task<ChangeMemberRoleResult> Handle(ChangeMemberRoleCommand command, CancellationToken cancellationToken);
}

public interface IChangeMemberRoleStore
{
    Task<Team?> FindTeam(Guid teamId, CancellationToken cancellationToken);

    /// <summary>Sets the role only if the team is still at <paramref name="expectedVersion"/>; false means it changed meanwhile.</summary>
    Task<bool> SetRole(Guid teamId, long expectedVersion, Guid accountId, TeamRole role, CancellationToken cancellationToken);
}
