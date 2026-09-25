using EventStorming.Collaboration.Model;
using EventStorming.Collaboration.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.Collaboration.Slices.MoveCursor;

public sealed class MoveCursorCommandHandler(IMoveCursorStore store, IPresenceBroadcaster broadcaster) : IMoveCursorCommandHandler
{
    public async Task<MoveCursorResult> Handle(MoveCursorCommand command, CancellationToken cancellationToken)
    {
        if (!IsCoordinate(command.X) || !IsCoordinate(command.Y))
        {
            return MoveCursorResult.Failed(Failures.Invalid("x", "out-of-range", "Cursor coordinates must be finite board coordinates."));
        }

        var participant = await store.Find(command.ConnectionId, cancellationToken);
        if (participant is null || participant.BoardId != command.BoardId)
        {
            return MoveCursorResult.Failed(NotJoined);
        }

        broadcaster.CursorMoved(participant, command.X, command.Y);
        return MoveCursorResult.Succeeded();
    }

    internal static bool IsCoordinate(double value) => double.IsFinite(value) && Math.Abs(value) <= PresenceLimits.MaxCoordinate;

    internal static readonly Failure NotJoined = Failures.Conflict("not-joined", "This connection has not joined the board.", fix: "Join the board first.");
}
