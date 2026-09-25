using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.GetBoard;

public sealed class GetBoardQueryHandler(IBoardAccess access, IGetBoardStore store) : IGetBoardQueryHandler
{
    public async Task<GetBoardResult> Handle(GetBoardQuery query, CancellationToken cancellationToken)
    {
        var granted = await access.ForBoard(query.BoardId, query.Actor, cancellationToken);
        if (BoardRules.RequireView(granted) is { } refused)
        {
            return GetBoardResult.Failed(refused);
        }

        var board = await store.Find(query.BoardId, cancellationToken);
        return board is null
            ? GetBoardResult.Failed(Failures.NotFound("The board"))
            : GetBoardResult.Succeeded(board, granted.Permission);
    }
}
