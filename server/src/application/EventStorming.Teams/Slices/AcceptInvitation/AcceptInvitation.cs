using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Teams.Slices.AcceptInvitation;

public sealed record AcceptInvitationCommand(Actor Actor, string Token);

public sealed class AcceptInvitationResult : IUseCaseResult
{
    private AcceptInvitationResult(TeamSummary? team, IReadOnlyList<Failure> failures)
    {
        Team = team;
        Failures = failures;
    }

    public TeamSummary? Team { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static AcceptInvitationResult Succeeded(TeamSummary team) => new(team, []);

    public static AcceptInvitationResult Failed(Failure failure) => new(null, [failure]);
}

public interface IAcceptInvitationCommandHandler
{
    Task<AcceptInvitationResult> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken);
}

public interface IAcceptInvitationStore
{
    Task<Invitation?> FindByHash(string tokenHash, CancellationToken cancellationToken);

    Task<Team?> FindTeam(Guid teamId, CancellationToken cancellationToken);

    /// <summary>The accepting account's email, read through to the Identity context.</summary>
    Task<string?> EmailOf(Guid accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the member and, for a single-use invitation, marks it accepted - together, atomically.
    /// Returns false if the account became a member in the meantime.
    /// </summary>
    Task<bool> AddMember(Guid teamId, Membership membership, Guid? consumeInvitationId, DateTimeOffset acceptedAt, CancellationToken cancellationToken);
}
