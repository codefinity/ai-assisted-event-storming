using EventStorming.PublicIntegration.Model;

namespace EventStorming.PublicIntegration.Slices.RecordIdempotentResponse;

public sealed class RecordIdempotentResponseCommandHandler(IRecordIdempotentResponseStore store) : IRecordIdempotentResponseCommandHandler
{
    public async Task<RecordIdempotentResponseResult> Handle(RecordIdempotentResponseCommand command, CancellationToken cancellationToken)
    {
        var recordId = IdempotencyPolicy.RecordId(command.ApiKeyId, command.Key);
        if (command.Response is null)
        {
            await store.Release(recordId, cancellationToken);
        }
        else
        {
            await store.Complete(recordId, command.Response, cancellationToken);
        }

        return RecordIdempotentResponseResult.Succeeded();
    }
}
