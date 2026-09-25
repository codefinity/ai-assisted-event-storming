using EventStorming.SharedKernel;

namespace EventStorming.PublicIntegration.Slices.AuthenticateApiKey;

/// <summary>Resolves a presented key ("es_…") to the team and scopes it stands for, or refuses it with the reason.</summary>
public sealed record AuthenticateApiKeyCommand(string? PresentedKey);

public sealed record AuthenticatedApiKey(Guid KeyId, Guid TeamId, string Name, IReadOnlyList<string> Scopes);

public sealed class AuthenticateApiKeyResult : IUseCaseResult
{
    private AuthenticateApiKeyResult(AuthenticatedApiKey? key, IReadOnlyList<Failure> failures)
    {
        Key = key;
        Failures = failures;
    }

    public AuthenticatedApiKey? Key { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static AuthenticateApiKeyResult Succeeded(AuthenticatedApiKey key) => new(key, []);

    public static AuthenticateApiKeyResult Failed(Failure failure) => new(null, [failure]);
}

public interface IAuthenticateApiKeyCommandHandler
{
    Task<AuthenticateApiKeyResult> Handle(AuthenticateApiKeyCommand command, CancellationToken cancellationToken);
}

public interface IAuthenticateApiKeyStore
{
    Task<Model.ApiKey?> Find(Guid keyId, CancellationToken cancellationToken);

    Task TouchLastUsed(Guid keyId, DateTimeOffset usedAt, CancellationToken cancellationToken);
}
