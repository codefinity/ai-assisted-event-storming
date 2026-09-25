using EventStorming.Collaboration.Model;
using EventStorming.Collaboration.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.Collaboration.Slices.JoinBoard;

public sealed class JoinBoardCommandHandler(
    IBoardViewAccess access,
    IJoinBoardStore store,
    IPresenceBroadcaster broadcaster,
    IClock clock) : IJoinBoardCommandHandler
{
    public async Task<JoinBoardResult> Handle(JoinBoardCommand command, CancellationToken cancellationToken)
    {
        if (command.Actor.Kind != ActorKind.Account)
        {
            return JoinBoardResult.Failed(Failures.Forbidden("Only a signed-in person can join a board live."));
        }

        if (!await access.CanView(command.BoardId, command.Actor, cancellationToken))
        {
            return JoinBoardResult.Failed(Failures.NotFound("The board"));
        }

        if (await store.Leave(command.ConnectionId, cancellationToken) is { } previous)
        {
            broadcaster.Left(previous);
        }

        var you = new Participant(
            command.ConnectionId,
            command.BoardId,
            command.Actor.Id,
            command.Actor.DisplayName,
            ParticipantColors.For(command.Actor.Id),
            clock.UtcNow);

        await store.Add(you, cancellationToken);
        broadcaster.Joined(you);

        var everyone = await store.OnBoard(command.BoardId, cancellationToken);
        return JoinBoardResult.Succeeded(new JoinedBoard(you, everyone));
    }
}
