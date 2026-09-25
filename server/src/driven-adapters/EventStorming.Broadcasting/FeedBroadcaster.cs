using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.Broadcasting.Transport;
using EventStorming.Collaboration.Model;
using EventStorming.Collaboration.Shared;
using EventStorming.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace EventStorming.Broadcasting;

/// <summary>
/// Turns what Board Modelling and Collaboration announce into wire messages on the board feed.
/// Changes made through any driving adapter - the web app's hub, the public REST API, a future MCP
/// server - come through here, which is why every open board sees them live.
/// </summary>
internal sealed class FeedBroadcaster(IBoardFeed feed) : IBoardChangeBroadcaster, IPresenceBroadcaster
{
    public void ContentChanged(BoardChangeSet changes) =>
        feed.Publish(new FeedItem(changes.BoardId, ClientMethods.BoardChanged, new BoardChangedMessage(
            changes.BoardId,
            changes.Revision,
            changes.OperationId,
            Wire(changes.Actor),
            changes.Elements.Select(Wire).ToList(),
            changes.RemovedElements.Select(removed => new RemovedWire(removed.Id, removed.Version)).ToList(),
            changes.Connections.Select(connection => new ConnectionWire(connection.Id, connection.From, connection.To, connection.Label, connection.Version)).ToList(),
            changes.RemovedConnections.Select(removed => new RemovedWire(removed.Id, removed.Version)).ToList())));

    public void ContentReplaced(Guid boardId, long revision, ActorRef by) =>
        feed.Publish(new FeedItem(boardId, ClientMethods.BoardReplaced, new BoardReplacedMessage(boardId, revision, Wire(by))));

    public void DetailsChanged(Board board) =>
        feed.Publish(new FeedItem(board.Id, ClientMethods.BoardDetailsChanged, new BoardDetailsMessage(board.Id, board.Name, board.ArchivedAt)));

    public void Joined(Participant participant) =>
        feed.Publish(new FeedItem(participant.BoardId, ClientMethods.ParticipantJoined,
            new ParticipantJoinedMessage(participant.BoardId, Wire(participant)), participant.ConnectionId));

    public void Left(Participant participant) =>
        feed.Publish(new FeedItem(participant.BoardId, ClientMethods.ParticipantLeft,
            new ParticipantLeftMessage(participant.BoardId, participant.ConnectionId, participant.AccountId), participant.ConnectionId));

    public void CursorMoved(Participant participant, double x, double y) =>
        feed.Publish(new FeedItem(participant.BoardId, ClientMethods.CursorMoved,
            new CursorMovedMessage(participant.BoardId, participant.ConnectionId, x, y), participant.ConnectionId));

    public void EditingFocusChanged(Participant participant) =>
        feed.Publish(new FeedItem(participant.BoardId, ClientMethods.EditingFocusChanged,
            new EditingFocusMessage(participant.BoardId, participant.ConnectionId, participant.EditingElementId), participant.ConnectionId));

    public void DragPreviewed(Participant participant, IReadOnlyList<DragMove> moves) =>
        feed.Publish(new FeedItem(participant.BoardId, ClientMethods.DragPreviewed,
            new DragPreviewMessage(participant.BoardId, participant.ConnectionId, moves.Select(move => new DragMoveWire(move.ElementId, move.X, move.Y)).ToList()),
            participant.ConnectionId));

    public static ParticipantWire Wire(Participant participant) =>
        new(participant.ConnectionId, participant.AccountId, participant.DisplayName, participant.Color, participant.EditingElementId);

    private static ActorWire Wire(ActorRef actor) => new(actor.Kind == ActorKind.ApiKey ? "api-key" : "account", actor.Id, actor.Name);

    private static ElementWire Wire(Element element) => new(
        element.Id, element.Type, element.Text, element.X, element.Y, element.Width, element.Height,
        element.Pivotal, element.Color, element.Version, element.UpdatedAt, Wire(element.UpdatedBy));
}

public static class BroadcastingServiceExtensions
{
    public static IServiceCollection AddEventStormingBroadcasting(this IServiceCollection services)
    {
        services.AddEventStormingBoardFeed();
        services.AddSingleton<FeedBroadcaster>();
        services.AddSingleton<IBoardChangeBroadcaster>(provider => provider.GetRequiredService<FeedBroadcaster>());
        services.AddSingleton<IPresenceBroadcaster>(provider => provider.GetRequiredService<FeedBroadcaster>());
        return services;
    }
}
