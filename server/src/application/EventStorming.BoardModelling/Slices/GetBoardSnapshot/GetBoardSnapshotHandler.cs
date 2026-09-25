using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.GetBoardSnapshot;

public sealed class GetBoardSnapshotQueryHandler(IBoardAccess access, IGetBoardSnapshotStore store) : IGetBoardSnapshotQueryHandler
{
    public async Task<GetBoardSnapshotResult> Handle(GetBoardSnapshotQuery query, CancellationToken cancellationToken)
    {
        var granted = await access.ForBoard(query.BoardId, query.Actor, cancellationToken);
        if (BoardRules.RequireView(granted) is { } refused)
        {
            return GetBoardSnapshotResult.Failed(refused);
        }

        // The board is read first, so its revision is never newer than the content read after it. A
        // client that later receives a change with a higher revision therefore never skips one.
        var board = await store.FindBoard(query.BoardId, cancellationToken);
        if (board is null)
        {
            return GetBoardSnapshotResult.Failed(Failures.NotFound("The board"));
        }

        var elements = await store.Elements(board.Id, cancellationToken);
        var connections = await store.Connections(board.Id, cancellationToken);
        return GetBoardSnapshotResult.Succeeded(new BoardSnapshot(board, granted.Permission, elements, connections));
    }
}
