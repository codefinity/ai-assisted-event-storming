using EventStorming.Collaboration.Shared;

namespace EventStorming.Collaboration.Slices.LeaveBoard;

public sealed class LeaveBoardCommandHandler(ILeaveBoardStore store, IPresenceBroadcaster broadcaster) : ILeaveBoardCommandHandler
{
    public async Task<LeaveBoardResult> Handle(LeaveBoardCommand command, CancellationToken cancellationToken)
    {
        var left = await store.Remove(command.ConnectionId, cancellationToken);
        if (left is not null)
        {
            broadcaster.Left(left);
        }

        return LeaveBoardResult.Succeeded(left);
    }
}
