using EventStorming.PublicIntegration.Model;
using EventStorming.SharedKernel;

namespace EventStorming.PublicIntegration.Slices.RecordIdempotentResponse;

/// <summary>
/// After a write finishes, keeps its response for replays. A null <see cref="Response"/> releases the key
/// instead - used when the write failed in a way worth retrying (a server error), so the retry runs again.
/// </summary>
public sealed record RecordIdempotentResponseCommand(Guid ApiKeyId, string Key, StoredResponse? Response);

public sealed class RecordIdempotentResponseResult : IUseCaseResult
{
    private RecordIdempotentResponseResult()
    {
    }

    public IReadOnlyList<Failure> Failures => [];

    public bool Success => true;

    public static RecordIdempotentResponseResult Succeeded() => new();
}

public interface IRecordIdempotentResponseCommandHandler
{
    Task<RecordIdempotentResponseResult> Handle(RecordIdempotentResponseCommand command, CancellationToken cancellationToken);
}

public interface IRecordIdempotentResponseStore
{
    Task Complete(string recordId, StoredResponse response, CancellationToken cancellationToken);

    Task Release(string recordId, CancellationToken cancellationToken);
}
