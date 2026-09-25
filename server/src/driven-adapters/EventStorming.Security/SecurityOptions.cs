namespace EventStorming.Security;

/// <summary>
/// <see cref="SigningKey"/> signs access tokens (HMAC-SHA256, at least 32 bytes once decoded from
/// Base64). The host supplies it from configuration and never from source control.
/// <see cref="PasswordHashIterations"/> is lowered only by tests; stored hashes record their own cost,
/// so changing it never breaks existing passwords.
/// </summary>
public sealed record SecurityOptions(
    string Issuer,
    string Audience,
    string SigningKey,
    int AccessTokenMinutes = 15,
    int PasswordHashIterations = 600_000);
