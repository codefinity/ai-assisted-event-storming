namespace EventStorming.Identity.Model;

/// <summary>
/// One link in a session's refresh-token chain. Only the hash of the token is stored. Every use rotates
/// the token (<see cref="ReplacedById"/>), and every token minted from the same sign-in shares a
/// <see cref="FamilyId"/>, so presenting a token that was already rotated revokes the whole family.
/// </summary>
public sealed record RefreshToken(
    Guid Id,
    Guid AccountId,
    Guid FamilyId,
    string TokenHash,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    Guid? ReplacedById = null,
    DateTimeOffset? RotatedAt = null,
    DateTimeOffset? RevokedAt = null,
    string? RevokedReason = null);

/// <summary>What a successful sign-in or refresh hands back to the driving adapter.</summary>
public sealed record SessionTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AccountSummary Account);
