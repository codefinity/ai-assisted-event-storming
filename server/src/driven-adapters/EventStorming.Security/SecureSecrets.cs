using System.Security.Cryptography;
using System.Text;
using EventStorming.Identity.Shared;
using EventStorming.PublicIntegration.Shared;
using EventStorming.Teams.Shared;

namespace EventStorming.Security;

/// <summary>
/// 256-bit random secrets - refresh tokens, invitation tokens, API-key secrets - handed out once and
/// stored only as a SHA-256 digest. A slow hash is unnecessary here: unlike a password, each secret has
/// full entropy, so its digest cannot be brute-forced.
/// </summary>
internal sealed class SecureSecrets : IRefreshTokenGenerator, IInvitationTokenGenerator, IApiKeySecretGenerator
{
    GeneratedRefreshToken IRefreshTokenGenerator.Generate()
    {
        var value = NewSecret();
        return new GeneratedRefreshToken(value, Digest(value));
    }

    GeneratedInvitationToken IInvitationTokenGenerator.Generate()
    {
        var value = NewSecret();
        return new GeneratedInvitationToken(value, Digest(value));
    }

    public string HashOf(string secret) => Digest(secret);

    public string NewSecret() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public bool Matches(string secret, string secretHash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Digest(secret)), Encoding.ASCII.GetBytes(secretHash));

    private static string Digest(string secret) => Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
