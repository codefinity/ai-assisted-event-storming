namespace EventStorming.Identity.Model;

/// <summary>A person who can sign in. <see cref="Email"/> is always stored normalized (trimmed, lower case).</summary>
public sealed record Account(Guid Id, string Email, string DisplayName, string PasswordHash, DateTimeOffset CreatedAt);

/// <summary>What the rest of the system may know about an account - never the password hash.</summary>
public sealed record AccountSummary(Guid Id, string Email, string DisplayName);

public static class EmailAddresses
{
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
