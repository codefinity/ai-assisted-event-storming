using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.DeleteBoard;

public sealed class DeleteBoardCommandHandler(
    IBoardAccess access,
    IDeleteBoardStore store,
    IBoardChangeBroadcaster broadcaster) : IDeleteBoardCommandHandler
{
    public async Task<DeleteBoardResult> Handle(DeleteBoardCommand command, CancellationToken cancellationToken)
    {
        var granted = await access.ForBoard(command.BoardId, command.Actor, cancellationToken);

        // An archived board can be deleted too: that is the usual way a board reaches the end of its life.
        if (BoardRules.RequireEdit(granted with { Archived = false }) is { } refused)
        {
            return DeleteBoardResult.Failed(refused);
        }

        if (!await store.Delete(command.BoardId, cancellationToken))
        {
            return DeleteBoardResult.Failed(Failures.NotFound("The board"));
        }

        broadcaster.Deleted(command.BoardId, command.Actor.Ref);
        return DeleteBoardResult.Succeeded();
    }
}
