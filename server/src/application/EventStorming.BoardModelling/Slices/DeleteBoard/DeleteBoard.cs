using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.DeleteBoard;

/// <summary>
/// Deleting a board removes it and everything on it - every element and connection - for good.
/// Unlike archiving, it cannot be undone.
/// </summary>
public sealed record DeleteBoardCommand(Actor Actor, Guid BoardId);

public sealed class DeleteBoardResult : IUseCaseResult
{
    private DeleteBoardResult(IReadOnlyList<Failure> failures)
    {
        Failures = failures;
    }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static DeleteBoardResult Succeeded() => new([]);

    public static DeleteBoardResult Failed(Failure failure) => new([failure]);
}

public interface IDeleteBoardCommandHandler
{
    Task<DeleteBoardResult> Handle(DeleteBoardCommand command, CancellationToken cancellationToken);
}

public interface IDeleteBoardStore
{
    /// <summary>Removes the board with its elements and connections, all or nothing. False if there was no such board.</summary>
    Task<bool> Delete(Guid boardId, CancellationToken cancellationToken);
}
