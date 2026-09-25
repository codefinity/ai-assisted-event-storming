using EventStorming.Identity.Model;
using EventStorming.Identity.Shared;
using EventStorming.PublicIntegration.Shared;
using EventStorming.SharedKernel;
using EventStorming.Teams.Shared;

namespace EventStorming.Specs.Support.Fakes;

/// <summary>Deterministic and cheap: "hashed:&lt;password&gt;", so scenarios can still prove nothing is stored in plain text.</summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hashed:" + password;

    public bool Verify(string password, string passwordHash) => passwordHash == "hashed:" + password;
}

/// <summary>The token names the account it was issued to, so steps can assert who holds it.</summary>
public sealed class FakeAccessTokenIssuer(IClock clock) : IAccessTokenIssuer
{
    public IssuedAccessToken Issue(AccountSummary account) => new($"access-for:{account.Id}", clock.UtcNow.AddMinutes(15));
}

/// <summary>Numbered secrets ("secret-1", "secret-2", ...) whose "hash" is visibly derived from them.</summary>
public sealed class FakeSecrets : IRefreshTokenGenerator, IInvitationTokenGenerator, IApiKeySecretGenerator
{
    private int issued;

    public string LastIssued { get; private set; } = string.Empty;

    GeneratedRefreshToken IRefreshTokenGenerator.Generate()
    {
        var value = NewSecret();
        return new GeneratedRefreshToken(value, HashOf(value));
    }

    GeneratedInvitationToken IInvitationTokenGenerator.Generate()
    {
        var value = NewSecret();
        return new GeneratedInvitationToken(value, HashOf(value));
    }

    public string HashOf(string secret) => "hash:" + secret;

    public string NewSecret()
    {
        LastIssued = $"secret-{++issued}";
        return LastIssued;
    }

    public bool Matches(string secret, string secretHash) => HashOf(secret) == secretHash;
}
