using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.ArchiveBoard;

public sealed class ArchiveBoardCommandHandler(
    IBoardAccess access,
    IArchiveBoardStore store,
    IBoardChangeBroadcaster broadcaster,
    IClock clock) : IArchiveBoardCommandHandler
{
    public async Task<ArchiveBoardResult> Handle(ArchiveBoardCommand command, CancellationToken cancellationToken)
    {
        var granted = await access.ForBoard(command.BoardId, command.Actor, cancellationToken);

        // Archiving an archived board is a harmless no-op, so only the permission matters here.
        if (BoardRules.RequireEdit(granted with { Archived = false }) is { } refused)
        {
            return ArchiveBoardResult.Failed(refused);
        }

        var board = await store.Archive(command.BoardId, command.Actor.Ref, clock.UtcNow, cancellationToken);
        if (board is null)
        {
            return ArchiveBoardResult.Failed(Failures.NotFound("The board"));
        }

        broadcaster.DetailsChanged(board);
        return ArchiveBoardResult.Succeeded(board);
    }
}
