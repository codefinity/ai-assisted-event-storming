using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.RestoreBoard;

public sealed record RestoreBoardCommand(Actor Actor, Guid BoardId);

public sealed class RestoreBoardResult : IUseCaseResult
{
    private RestoreBoardResult(Board? board, IReadOnlyList<Failure> failures)
    {
        Board = board;
        Failures = failures;
    }

    public Board? Board { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static RestoreBoardResult Succeeded(Board board) => new(board, []);

    public static RestoreBoardResult Failed(Failure failure) => new(null, [failure]);
}

public interface IRestoreBoardCommandHandler
{
    Task<RestoreBoardResult> Handle(RestoreBoardCommand command, CancellationToken cancellationToken);
}

public interface IRestoreBoardStore
{
    /// <summary>Clears archivedAt, and returns the board either way.</summary>
    Task<Board?> Restore(Guid boardId, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken);
}
