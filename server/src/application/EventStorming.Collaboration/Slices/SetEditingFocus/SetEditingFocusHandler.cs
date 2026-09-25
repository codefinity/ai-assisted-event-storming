using EventStorming.Collaboration.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.Collaboration.Slices.SetEditingFocus;

public sealed class SetEditingFocusCommandHandler(ISetEditingFocusStore store, IPresenceBroadcaster broadcaster) : ISetEditingFocusCommandHandler
{
    public async Task<SetEditingFocusResult> Handle(SetEditingFocusCommand command, CancellationToken cancellationToken)
    {
        var participant = await store.SetEditing(command.ConnectionId, command.ElementId, cancellationToken);
        if (participant is null || participant.BoardId != command.BoardId)
        {
            return SetEditingFocusResult.Failed(Failures.Conflict("not-joined", "This connection has not joined the board.", fix: "Join the board first."));
        }

        broadcaster.EditingFocusChanged(participant);
        return SetEditingFocusResult.Succeeded();
    }
}
