using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.DuplicateBoard;

/// <summary>Copies a board and all of its content into a new board of the same team. Without a name the copy is called "Copy of …".</summary>
public sealed record DuplicateBoardCommand(Actor Actor, Guid BoardId, string? Name);

public sealed class DuplicateBoardResult : IUseCaseResult
{
    private DuplicateBoardResult(Board? board, IReadOnlyList<Failure> failures)
    {
        Board = board;
        Failures = failures;
    }

    public Board? Board { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static DuplicateBoardResult Succeeded(Board board) => new(board, []);

    public static DuplicateBoardResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static DuplicateBoardResult Failed(Failure failure) => new(null, [failure]);
}

public interface IDuplicateBoardCommandHandler
{
    Task<DuplicateBoardResult> Handle(DuplicateBoardCommand command, CancellationToken cancellationToken);
}

public interface IDuplicateBoardStore
{
    Task<Board?> FindBoard(Guid boardId, CancellationToken cancellationToken);

    /// <summary>Inserts <paramref name="copy"/> and a copy of every element and connection of the source, with new ids, in one transaction.</summary>
    Task<Board> Duplicate(Guid sourceBoardId, Board copy, CancellationToken cancellationToken);
}
