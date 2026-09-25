using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.GetBoardSnapshot;

/// <summary>Everything the web app needs to open a board: its details, all its content, and the caller's permission.</summary>
public sealed record GetBoardSnapshotQuery(Actor Actor, Guid BoardId);

public sealed record BoardSnapshot(Board Board, BoardPermission Permission, IReadOnlyList<Element> Elements, IReadOnlyList<Connection> Connections);

public sealed class GetBoardSnapshotResult : IUseCaseResult
{
    private GetBoardSnapshotResult(BoardSnapshot? snapshot, IReadOnlyList<Failure> failures)
    {
        Snapshot = snapshot;
        Failures = failures;
    }

    public BoardSnapshot? Snapshot { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static GetBoardSnapshotResult Succeeded(BoardSnapshot snapshot) => new(snapshot, []);

    public static GetBoardSnapshotResult Failed(Failure failure) => new(null, [failure]);
}

public interface IGetBoardSnapshotQueryHandler
{
    Task<GetBoardSnapshotResult> Handle(GetBoardSnapshotQuery query, CancellationToken cancellationToken);
}

public interface IGetBoardSnapshotStore
{
    Task<Board?> FindBoard(Guid boardId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Element>> Elements(Guid boardId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Connection>> Connections(Guid boardId, CancellationToken cancellationToken);
}
