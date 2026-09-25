using EventStorming.PublicIntegration.Model;
using EventStorming.PublicIntegration.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.PublicIntegration.Slices.AuthenticateApiKey;

public sealed class AuthenticateApiKeyCommandHandler(
    IAuthenticateApiKeyStore store,
    IApiKeySecretGenerator secrets,
    IClock clock) : IAuthenticateApiKeyCommandHandler
{
    /// <summary>"Last used" is informational, so it is written at most once a minute per key.</summary>
    private static readonly TimeSpan LastUsedResolution = TimeSpan.FromMinutes(1);

    public async Task<AuthenticateApiKeyResult> Handle(AuthenticateApiKeyCommand command, CancellationToken cancellationToken)
    {
        if (!ApiKeyFormat.TryParse(command.PresentedKey, out var keyId, out var secret))
        {
            return AuthenticateApiKeyResult.Failed(Refused("api-key-malformed", "The API key is not in the expected format.",
                "Send the full key exactly as it was shown when it was created: 'Authorization: Bearer es_…'."));
        }

        var key = await store.Find(keyId, cancellationToken);
        if (key is null || !secrets.Matches(secret, key.SecretHash))
        {
            return AuthenticateApiKeyResult.Failed(Refused("api-key-invalid", "The API key is not valid.",
                "Check the key was copied completely, or create a new one in the team's settings."));
        }

        var now = clock.UtcNow;
        if (key.RevokedAt is not null)
        {
            return AuthenticateApiKeyResult.Failed(Refused("api-key-revoked", "The API key has been revoked.", "Create a new key in the team's settings."));
        }

        if (key.ExpiresAt is { } expiresAt && expiresAt <= now)
        {
            return AuthenticateApiKeyResult.Failed(Refused("api-key-expired", "The API key has expired.", "Create a new key in the team's settings."));
        }

        if (key.LastUsedAt is null || now - key.LastUsedAt.Value >= LastUsedResolution)
        {
            await store.TouchLastUsed(key.Id, now, cancellationToken);
        }

        return AuthenticateApiKeyResult.Succeeded(new AuthenticatedApiKey(key.Id, key.TeamId, key.Name, ApiScopes.Normalize(key.Scopes)));
    }

    private static Failure Refused(string code, string message, string fix) => new(FailureKind.Unauthenticated, code, message, Fix: fix);
}
