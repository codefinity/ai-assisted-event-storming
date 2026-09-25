using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Teams.Slices.RevokeInvitation;

public sealed record RevokeInvitationCommand(Actor Actor, Guid TeamId, Guid InvitationId);

public sealed class RevokeInvitationResult : IUseCaseResult
{
    private RevokeInvitationResult(IReadOnlyList<Failure> failures) => Failures = failures;

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static RevokeInvitationResult Succeeded() => new([]);

    public static RevokeInvitationResult Failed(Failure failure) => new([failure]);
}

public interface IRevokeInvitationCommandHandler
{
    Task<RevokeInvitationResult> Handle(RevokeInvitationCommand command, CancellationToken cancellationToken);
}

public interface IRevokeInvitationStore
{
    Task<Team?> FindTeam(Guid teamId, CancellationToken cancellationToken);

    /// <summary>False when the team has no such invitation.</summary>
    Task<bool> Revoke(Guid teamId, Guid invitationId, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}
