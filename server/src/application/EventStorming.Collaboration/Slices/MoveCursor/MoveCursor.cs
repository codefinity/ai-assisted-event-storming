using EventStorming.Collaboration.Model;
using EventStorming.SharedKernel;

namespace EventStorming.Collaboration.Slices.MoveCursor;

/// <summary>A participant's pointer moved. Coordinates are in board space, so every viewer draws it in the right place whatever their zoom.</summary>
public sealed record MoveCursorCommand(string ConnectionId, Guid BoardId, double X, double Y);

public sealed class MoveCursorResult : IUseCaseResult
{
    private MoveCursorResult(IReadOnlyList<Failure> failures) => Failures = failures;

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static MoveCursorResult Succeeded() => new([]);

    public static MoveCursorResult Failed(Failure failure) => new([failure]);
}

public interface IMoveCursorCommandHandler
{
    Task<MoveCursorResult> Handle(MoveCursorCommand command, CancellationToken cancellationToken);
}

public interface IMoveCursorStore
{
    Task<Participant?> Find(string connectionId, CancellationToken cancellationToken);
}
