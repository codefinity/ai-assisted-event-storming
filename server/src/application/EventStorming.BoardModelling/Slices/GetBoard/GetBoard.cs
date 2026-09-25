using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.GetBoard;

public sealed record GetBoardQuery(Actor Actor, Guid BoardId);

public sealed class GetBoardResult : IUseCaseResult
{
    private GetBoardResult(Board? board, BoardPermission permission, IReadOnlyList<Failure> failures)
    {
        Board = board;
        Permission = permission;
        Failures = failures;
    }

    public Board? Board { get; }

    public BoardPermission Permission { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static GetBoardResult Succeeded(Board board, BoardPermission permission) => new(board, permission, []);

    public static GetBoardResult Failed(Failure failure) => new(null, BoardPermission.None, [failure]);
}

public interface IGetBoardQueryHandler
{
    Task<GetBoardResult> Handle(GetBoardQuery query, CancellationToken cancellationToken);
}

public interface IGetBoardStore
{
    Task<Board?> Find(Guid boardId, CancellationToken cancellationToken);
}
