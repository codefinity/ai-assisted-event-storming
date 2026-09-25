using EventStorming.Collaboration.Model;
using EventStorming.Collaboration.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.Collaboration.Slices.ShareDragPreview;

public sealed class ShareDragPreviewCommandHandler(IShareDragPreviewStore store, IPresenceBroadcaster broadcaster) : IShareDragPreviewCommandHandler
{
    public async Task<ShareDragPreviewResult> Handle(ShareDragPreviewCommand command, CancellationToken cancellationToken)
    {
        if (command.Moves.Count > PresenceLimits.MaxDragPreviewElements)
        {
            return ShareDragPreviewResult.Failed(Failures.Invalid("moves", "too-many",
                $"A drag preview shows at most {PresenceLimits.MaxDragPreviewElements} elements."));
        }

        if (command.Moves.Any(move => !IsCoordinate(move.X) || !IsCoordinate(move.Y)))
        {
            return ShareDragPreviewResult.Failed(Failures.Invalid("moves", "out-of-range", "Drag coordinates must be finite board coordinates."));
        }

        var participant = await store.Find(command.ConnectionId, cancellationToken);
        if (participant is null || participant.BoardId != command.BoardId)
        {
            return ShareDragPreviewResult.Failed(Failures.Conflict("not-joined", "This connection has not joined the board.", fix: "Join the board first."));
        }

        broadcaster.DragPreviewed(participant, command.Moves);
        return ShareDragPreviewResult.Succeeded();
    }

    private static bool IsCoordinate(double value) => double.IsFinite(value) && Math.Abs(value) <= PresenceLimits.MaxCoordinate;
}
