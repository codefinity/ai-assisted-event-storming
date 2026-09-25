using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.CreateBoard;

/// <param name="Level">One of "big-picture", "process-modelling", "software-design".</param>
public sealed record CreateBoardCommand(Actor Actor, Guid TeamId, string Name, string Level);

public sealed class CreateBoardResult : IUseCaseResult
{
    private CreateBoardResult(Board? board, IReadOnlyList<Failure> failures)
    {
        Board = board;
        Failures = failures;
    }

    public Board? Board { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static CreateBoardResult Succeeded(Board board) => new(board, []);

    public static CreateBoardResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static CreateBoardResult Failed(Failure failure) => new(null, [failure]);
}

public interface ICreateBoardCommandHandler
{
    Task<CreateBoardResult> Handle(CreateBoardCommand command, CancellationToken cancellationToken);
}

public interface ICreateBoardStore
{
    Task Insert(Board board, CancellationToken cancellationToken);
}
