using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.ArchiveBoard;

/// <summary>Archiving hides a board from the dashboard and makes it read-only. It can be restored at any time.</summary>
public sealed record ArchiveBoardCommand(Actor Actor, Guid BoardId);

public sealed class ArchiveBoardResult : IUseCaseResult
{
    private ArchiveBoardResult(Board? board, IReadOnlyList<Failure> failures)
    {
        Board = board;
        Failures = failures;
    }

    public Board? Board { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static ArchiveBoardResult Succeeded(Board board) => new(board, []);

    public static ArchiveBoardResult Failed(Failure failure) => new(null, [failure]);
}

public interface IArchiveBoardCommandHandler
{
    Task<ArchiveBoardResult> Handle(ArchiveBoardCommand command, CancellationToken cancellationToken);
}

public interface IArchiveBoardStore
{
    /// <summary>Sets archivedAt if the board is not archived yet, and returns the board either way.</summary>
    Task<Board?> Archive(Guid boardId, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken);
}
