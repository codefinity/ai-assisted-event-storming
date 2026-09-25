namespace EventStorming.Teams.Model;

/// <summary>
/// A link invitation can be used by anyone holding the link until it expires or is revoked. An email
/// invitation is for one address only and can be used once.
/// </summary>
public enum InvitationKind
{
    Link,
    Email,
}

public enum InvitationStatus
{
    Valid,
    Expired,
    Revoked,
    Used,
}

public sealed record Invitation(
    Guid Id,
    Guid TeamId,
    InvitationKind Kind,
    string? Email,
    TeamRole Role,
    string TokenHash,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? AcceptedAt = null,
    Guid? AcceptedBy = null,
    DateTimeOffset? RevokedAt = null);

public static class InvitationStatuses
{
    public static InvitationStatus Of(Invitation invitation, DateTimeOffset now) =>
        invitation.RevokedAt is not null ? InvitationStatus.Revoked
        : invitation.Kind == InvitationKind.Email && invitation.AcceptedAt is not null ? InvitationStatus.Used
        : invitation.ExpiresAt <= now ? InvitationStatus.Expired
        : InvitationStatus.Valid;
}

/// <summary>What the team's Owners see in the list of open invitations. The token itself is never shown again.</summary>
public sealed record InvitationView(Guid Id, InvitationKind Kind, string? Email, TeamRole Role, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt)
{
    public static InvitationView From(Invitation invitation) =>
        new(invitation.Id, invitation.Kind, invitation.Email, invitation.Role, invitation.CreatedAt, invitation.ExpiresAt);
}
