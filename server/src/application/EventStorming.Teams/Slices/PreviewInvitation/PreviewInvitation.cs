using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Teams.Slices.PreviewInvitation;

/// <summary>What the invitation page shows before someone decides to accept - available without signing in.</summary>
public sealed record PreviewInvitationQuery(string Token);

public sealed record InvitationPreview(string TeamName, string InvitedBy, TeamRole Role, InvitationKind Kind, string? Email, DateTimeOffset ExpiresAt, InvitationStatus Status);

/// <summary>An invitation together with the names the preview needs from its team and its inviter.</summary>
public sealed record InvitationWithNames(Invitation Invitation, string TeamName, string InvitedBy);

public sealed class PreviewInvitationResult : IUseCaseResult
{
    private PreviewInvitationResult(InvitationPreview? preview, IReadOnlyList<Failure> failures)
    {
        Preview = preview;
        Failures = failures;
    }

    public InvitationPreview? Preview { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static PreviewInvitationResult Succeeded(InvitationPreview preview) => new(preview, []);

    public static PreviewInvitationResult Failed(Failure failure) => new(null, [failure]);
}

public interface IPreviewInvitationQueryHandler
{
    Task<PreviewInvitationResult> Handle(PreviewInvitationQuery query, CancellationToken cancellationToken);
}

public interface IPreviewInvitationStore
{
    Task<InvitationWithNames?> FindByHash(string tokenHash, CancellationToken cancellationToken);
}
