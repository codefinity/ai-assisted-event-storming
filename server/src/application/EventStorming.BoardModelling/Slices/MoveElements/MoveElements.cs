using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.MoveElements;

public sealed record ElementMove(Guid ElementId, Position Position);

/// <summary>
/// Moves one or many elements at once (a drag of a selection, an undo of one). Elements that no longer
/// exist are skipped rather than failing the whole move: someone else may just have deleted one.
/// </summary>
public sealed record MoveElementsCommand(Actor Actor, Guid BoardId, IReadOnlyList<ElementMove> Moves, string? OperationId = null);

public sealed class MoveElementsResult : IUseCaseResult
{
    private MoveElementsResult(BoardChangeSet? changes, IReadOnlyList<Failure> failures)
    {
        Changes = changes;
        Failures = failures;
    }

    public BoardChangeSet? Changes { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static MoveElementsResult Succeeded(BoardChangeSet changes) => new(changes, []);

    public static MoveElementsResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static MoveElementsResult Failed(Failure failure) => new(null, [failure]);
}

public interface IMoveElementsCommandHandler
{
    Task<MoveElementsResult> Handle(MoveElementsCommand command, CancellationToken cancellationToken);
}

public sealed record MovedElements(long Revision, IReadOnlyList<Element> Elements);

public interface IMoveElementsStore
{
    /// <summary>Sets each element's position and increments its version. Returns the post-images of the elements that exist.</summary>
    Task<MovedElements> Move(Guid boardId, IReadOnlyList<ElementMove> moves, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken);
}
