using EventStorming.PublicIntegration.Model;
using EventStorming.SharedKernel;

namespace EventStorming.PublicIntegration.Slices.ReserveIdempotencyKey;

/// <summary>
/// Claims an Idempotency-Key before a write runs. If the key was already used for the same request, the
/// stored response comes back to be replayed instead of running the write again.
/// </summary>
public sealed record ReserveIdempotencyKeyCommand(Guid ApiKeyId, string Key, string RequestHash);

/// <param name="Replay">Null when the key is newly reserved and the write should run; otherwise the response to send again.</param>
public sealed record IdempotencyReservation(StoredResponse? Replay);

public sealed class ReserveIdempotencyKeyResult : IUseCaseResult
{
    private ReserveIdempotencyKeyResult(IdempotencyReservation? reservation, IReadOnlyList<Failure> failures)
    {
        Reservation = reservation;
        Failures = failures;
    }

    public IdempotencyReservation? Reservation { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static ReserveIdempotencyKeyResult Succeeded(IdempotencyReservation reservation) => new(reservation, []);

    public static ReserveIdempotencyKeyResult Failed(Failure failure) => new(null, [failure]);
}

public interface IReserveIdempotencyKeyCommandHandler
{
    Task<ReserveIdempotencyKeyResult> Handle(ReserveIdempotencyKeyCommand command, CancellationToken cancellationToken);
}

public interface IReserveIdempotencyKeyStore
{
    /// <summary>Inserts the record unless one with the same id exists; returns the existing record in that case, null otherwise.</summary>
    Task<IdempotencyRecord?> TryInsert(IdempotencyRecord record, CancellationToken cancellationToken);

    /// <summary>Replaces an expired or abandoned record with a fresh reservation.</summary>
    Task Replace(IdempotencyRecord record, CancellationToken cancellationToken);
}
