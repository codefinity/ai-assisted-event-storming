using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.RenameBoard;

public sealed record RenameBoardCommand(Actor Actor, Guid BoardId, string Name);

public sealed class RenameBoardResult : IUseCaseResult
{
    private RenameBoardResult(Board? board, IReadOnlyList<Failure> failures)
    {
        Board = board;
        Failures = failures;
    }

    public Board? Board { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static RenameBoardResult Succeeded(Board board) => new(board, []);

    public static RenameBoardResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static RenameBoardResult Failed(Failure failure) => new(null, [failure]);
}

public interface IRenameBoardCommandHandler
{
    Task<RenameBoardResult> Handle(RenameBoardCommand command, CancellationToken cancellationToken);
}

public interface IRenameBoardStore
{
    Task<Board?> Rename(Guid boardId, string name, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken);
}
