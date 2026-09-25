using EventStorming.Teams.Model;

namespace EventStorming.Teams.Shared;

public sealed record GeneratedInvitationToken(string Value, string Hash);

/// <summary>Invitation tokens are random secrets carried in the invitation link; only their hash is stored.</summary>
public interface IInvitationTokenGenerator
{
    GeneratedInvitationToken Generate();

    string HashOf(string token);
}

/// <summary>What an invitation email says. The adapter turns <see cref="Token"/> into a link to the web app.</summary>
public sealed record InvitationMail(string To, string TeamName, string InvitedBy, TeamRole Role, string Token, DateTimeOffset ExpiresAt);

public interface IInvitationMailer
{
    Task Send(InvitationMail mail, CancellationToken cancellationToken);
}

public static class InvitationPolicy
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
}
