using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.RestoreBoard;

public sealed class RestoreBoardCommandHandler(
    IBoardAccess access,
    IRestoreBoardStore store,
    IBoardChangeBroadcaster broadcaster,
    IClock clock) : IRestoreBoardCommandHandler
{
    public async Task<RestoreBoardResult> Handle(RestoreBoardCommand command, CancellationToken cancellationToken)
    {
        var granted = await access.ForBoard(command.BoardId, command.Actor, cancellationToken);
        if (BoardRules.RequireEdit(granted with { Archived = false }) is { } refused)
        {
            return RestoreBoardResult.Failed(refused);
        }

        var board = await store.Restore(command.BoardId, command.Actor.Ref, clock.UtcNow, cancellationToken);
        if (board is null)
        {
            return RestoreBoardResult.Failed(Failures.NotFound("The board"));
        }

        broadcaster.DetailsChanged(board);
        return RestoreBoardResult.Succeeded(board);
    }
}
