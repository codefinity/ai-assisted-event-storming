using EventStorming.PublicIntegration.Model;
using EventStorming.SharedKernel;

namespace EventStorming.PublicIntegration.Slices.CreateApiKey;

public sealed record CreateApiKeyCommand(Actor Actor, Guid TeamId, string Name, IReadOnlyList<string> Scopes, DateTimeOffset? ExpiresAt = null);

/// <summary><see cref="Key"/> is the full key, returned this once and never again.</summary>
public sealed record CreatedApiKey(ApiKeySummary Summary, string Key);

public sealed class CreateApiKeyResult : IUseCaseResult
{
    private CreateApiKeyResult(CreatedApiKey? created, IReadOnlyList<Failure> failures)
    {
        Created = created;
        Failures = failures;
    }

    public CreatedApiKey? Created { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static CreateApiKeyResult Succeeded(CreatedApiKey created) => new(created, []);

    public static CreateApiKeyResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static CreateApiKeyResult Failed(Failure failure) => new(null, [failure]);
}

public interface ICreateApiKeyCommandHandler
{
    Task<CreateApiKeyResult> Handle(CreateApiKeyCommand command, CancellationToken cancellationToken);
}

public interface ICreateApiKeyStore
{
    Task Insert(ApiKey key, CancellationToken cancellationToken);
}
