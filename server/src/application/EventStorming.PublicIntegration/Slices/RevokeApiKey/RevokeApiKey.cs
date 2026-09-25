using EventStorming.PublicIntegration.Model;
using EventStorming.SharedKernel;

namespace EventStorming.PublicIntegration.Slices.RevokeApiKey;

public sealed record RevokeApiKeyCommand(Actor Actor, Guid TeamId, Guid KeyId);

public sealed class RevokeApiKeyResult : IUseCaseResult
{
    private RevokeApiKeyResult(ApiKeySummary? key, IReadOnlyList<Failure> failures)
    {
        Key = key;
        Failures = failures;
    }

    public ApiKeySummary? Key { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static RevokeApiKeyResult Succeeded(ApiKeySummary key) => new(key, []);

    public static RevokeApiKeyResult Failed(Failure failure) => new(null, [failure]);
}

public interface IRevokeApiKeyCommandHandler
{
    Task<RevokeApiKeyResult> Handle(RevokeApiKeyCommand command, CancellationToken cancellationToken);
}

public interface IRevokeApiKeyStore
{
    /// <summary>Marks the team's key revoked (if it is not already) and returns it; null if the team has no such key.</summary>
    Task<ApiKey?> Revoke(Guid teamId, Guid keyId, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}
