using EventStorming.PublicIntegration.Model;
using EventStorming.SharedKernel;

namespace EventStorming.PublicIntegration.Slices.ReserveIdempotencyKey;

public sealed class ReserveIdempotencyKeyCommandHandler(IReserveIdempotencyKeyStore store, IClock clock) : IReserveIdempotencyKeyCommandHandler
{
    public async Task<ReserveIdempotencyKeyResult> Handle(ReserveIdempotencyKeyCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Key) || command.Key.Length > IdempotencyPolicy.MaxKeyLength || command.Key.Any(character => character < 0x21 || character > 0x7E))
        {
            return ReserveIdempotencyKeyResult.Failed(Failures.Invalid("Idempotency-Key", "invalid-idempotency-key",
                $"An Idempotency-Key is 1 to {IdempotencyPolicy.MaxKeyLength} visible ASCII characters.",
                "Send a fresh UUID for each distinct write, e.g. 'Idempotency-Key: 5f1c…'."));
        }

        var now = clock.UtcNow;
        var fresh = new IdempotencyRecord(
            IdempotencyPolicy.RecordId(command.ApiKeyId, command.Key),
            command.RequestHash,
            Completed: false,
            Response: null,
            now,
            now + IdempotencyPolicy.Retention);

        var existing = await store.TryInsert(fresh, cancellationToken);
        if (existing is null)
        {
            return ReserveIdempotencyKeyResult.Succeeded(new IdempotencyReservation(null));
        }

        var abandoned = !existing.Completed && now - existing.CreatedAt > IdempotencyPolicy.AbandonedAfter;
        if (existing.ExpiresAt <= now || abandoned)
        {
            await store.Replace(fresh, cancellationToken);
            return ReserveIdempotencyKeyResult.Succeeded(new IdempotencyReservation(null));
        }

        if (existing.RequestHash != command.RequestHash)
        {
            return ReserveIdempotencyKeyResult.Failed(Failures.Invalid("Idempotency-Key", "idempotency-key-reused",
                "This Idempotency-Key was already used for a different request.",
                "Use a new Idempotency-Key for every distinct write; reuse a key only to retry the identical request."));
        }

        if (!existing.Completed)
        {
            return ReserveIdempotencyKeyResult.Failed(Failures.Conflict("idempotency-in-progress",
                "A request with this Idempotency-Key is still being processed.", "Idempotency-Key",
                "Wait a moment and retry the same request with the same key."));
        }

        return ReserveIdempotencyKeyResult.Succeeded(new IdempotencyReservation(existing.Response));
    }
}
