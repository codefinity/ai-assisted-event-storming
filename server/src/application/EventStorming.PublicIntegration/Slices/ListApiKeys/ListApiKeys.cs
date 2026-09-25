using EventStorming.PublicIntegration.Model;
using EventStorming.SharedKernel;

namespace EventStorming.PublicIntegration.Slices.ListApiKeys;

/// <summary>Every key of a team, newest first, including revoked ones - never their secrets.</summary>
public sealed record ListApiKeysQuery(Actor Actor, Guid TeamId);

public sealed class ListApiKeysResult : IUseCaseResult
{
    private ListApiKeysResult(IReadOnlyList<ApiKeySummary> keys, IReadOnlyList<Failure> failures)
    {
        Keys = keys;
        Failures = failures;
    }

    public IReadOnlyList<ApiKeySummary> Keys { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static ListApiKeysResult Succeeded(IReadOnlyList<ApiKeySummary> keys) => new(keys, []);

    public static ListApiKeysResult Failed(Failure failure) => new([], [failure]);
}

public interface IListApiKeysQueryHandler
{
    Task<ListApiKeysResult> Handle(ListApiKeysQuery query, CancellationToken cancellationToken);
}

public interface IListApiKeysStore
{
    Task<IReadOnlyList<ApiKey>> KeysOf(Guid teamId, CancellationToken cancellationToken);
}
