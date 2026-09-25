using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Teams.Slices.RemoveMember;

/// <summary>An Owner removes a member, or any member removes themselves (leaving the team).</summary>
public sealed record RemoveMemberCommand(Actor Actor, Guid TeamId, Guid AccountId);

public sealed class RemoveMemberResult : IUseCaseResult
{
    private RemoveMemberResult(IReadOnlyList<Failure> failures) => Failures = failures;

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static RemoveMemberResult Succeeded() => new([]);

    public static RemoveMemberResult Failed(Failure failure) => new([failure]);
}

public interface IRemoveMemberCommandHandler
{
    Task<RemoveMemberResult> Handle(RemoveMemberCommand command, CancellationToken cancellationToken);
}

public interface IRemoveMemberStore
{
    Task<Team?> FindTeam(Guid teamId, CancellationToken cancellationToken);

    /// <summary>Removes the member only if the team is still at <paramref name="expectedVersion"/>.</summary>
    Task<bool> Remove(Guid teamId, long expectedVersion, Guid accountId, CancellationToken cancellationToken);
}
