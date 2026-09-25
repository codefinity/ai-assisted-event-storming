namespace EventStorming.Identity.Shared;

public sealed record GeneratedRefreshToken(string Value, string Hash);

/// <summary>
/// Refresh tokens are long random secrets. Only <see cref="GeneratedRefreshToken.Hash"/> is ever
/// stored, and <see cref="HashOf"/> is how a presented token is looked up again.
/// </summary>
public interface IRefreshTokenGenerator
{
    GeneratedRefreshToken Generate();

    string HashOf(string refreshToken);
}
