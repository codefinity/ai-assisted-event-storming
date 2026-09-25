using System.Security.Claims;
using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Slices.AddConnection;
using EventStorming.BoardModelling.Slices.AddElements;
using EventStorming.BoardModelling.Slices.DeleteConnections;
using EventStorming.BoardModelling.Slices.DeleteElements;
using EventStorming.BoardModelling.Slices.MoveElements;
using EventStorming.BoardModelling.Slices.UpdateElement;
using EventStorming.Broadcasting.Transport;
using EventStorming.Collaboration.Model;
using EventStorming.Collaboration.Slices.JoinBoard;
using EventStorming.Collaboration.Slices.LeaveBoard;
using EventStorming.Collaboration.Slices.MoveCursor;
using EventStorming.Collaboration.Slices.SetEditingFocus;
using EventStorming.Collaboration.Slices.ShareDragPreview;
using EventStorming.SharedKernel;
using Microsoft.AspNetCore.SignalR;

namespace EventStorming.Realtime.SignalR;

/// <summary>
/// The web app's live connection to a board. Joining and presence go to the Collaboration context;
/// every edit goes to the same Board Modelling use case the public REST API calls. What each use case
/// commits comes back to everyone on the board through the feed relay, not through these return values.
/// Use case handlers are injected per call, so each invocation gets its own scope.
/// </summary>
public sealed class BoardHub(IServiceProvider services) : Hub
{
    private const string BoardItem = "board";
    private const string CursorItem = "last-cursor";

    /// <summary>Cursor updates faster than this are dropped: 30 per second is plenty to look smooth.</summary>
    private static readonly TimeSpan CursorInterval = TimeSpan.FromMilliseconds(33);

    public static string Group(Guid boardId) => $"board:{boardId:N}";

    public async Task<JoinBoardResponse> JoinBoard(Guid boardId, IJoinBoardCommandHandler handler)
    {
        var result = await handler.Handle(new JoinBoardCommand(Actor(), boardId, Context.ConnectionId), Context.ConnectionAborted);
        if (!result.Success)
        {
            return new JoinBoardResponse(false, null, [], result.Failures.Select(FailureWire.From).ToList());
        }

        if (Context.Items.TryGetValue(BoardItem, out var previous) && previous is Guid previousBoard && previousBoard != boardId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(previousBoard));
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, Group(boardId));
        Context.Items[BoardItem] = boardId;

        var joined = result.Joined!;
        return new JoinBoardResponse(true, Wire(joined.You), joined.Participants.Select(Wire).ToList(), []);
    }

    public async Task LeaveBoard(ILeaveBoardCommandHandler handler)
    {
        await handler.Handle(new LeaveBoardCommand(Context.ConnectionId), Context.ConnectionAborted);
        if (Context.Items.Remove(BoardItem, out var board) && board is Guid boardId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(boardId));
        }
    }

    public async Task<OperationResult> AddElements(AddElementsRequest request, IAddElementsCommandHandler handler)
    {
        var result = await handler.Handle(
            new AddElementsCommand(
                Actor(),
                request.BoardId,
                (request.Elements ?? []).Select(element => new NewElement(
                    element.Type,
                    element.Text,
                    element.Id,
                    Position: element.X is { } x && element.Y is { } y ? new Position(x, y) : null,
                    Size: element.Width is { } width && element.Height is { } height ? new Size(width, height) : null,
                    Pivotal: element.Pivotal ?? false,
                    Color: element.Color)).ToList(),
                (request.Connections ?? []).Select(connection => new NewConnection(connection.From, connection.To, connection.Label, connection.Id)).ToList(),
                request.OperationId),
            Context.ConnectionAborted);
        return OperationResult.From(result, request.OperationId, result.Added?.Changes.Revision ?? 0);
    }

    public async Task<OperationResult> UpdateElement(UpdateElementRequest request, IUpdateElementCommandHandler handler)
    {
        var result = await handler.Handle(
            new UpdateElementCommand(
                Actor(),
                request.BoardId,
                request.ElementId,
                request.Text,
                request.Type,
                request.X is { } x && request.Y is { } y ? new Position(x, y) : null,
                request.Width is { } width && request.Height is { } height ? new Size(width, height) : null,
                request.Pivotal,
                request.Color,
                OperationId: request.OperationId),
            Context.ConnectionAborted);
        return OperationResult.From(result, request.OperationId, result.Changes?.Revision ?? 0);
    }

    public async Task<OperationResult> MoveElements(MoveElementsRequest request, IMoveElementsCommandHandler handler)
    {
        var result = await handler.Handle(
            new MoveElementsCommand(
                Actor(),
                request.BoardId,
                (request.Moves ?? []).Select(move => new ElementMove(move.ElementId, new Position(move.X, move.Y))).ToList(),
                request.OperationId),
            Context.ConnectionAborted);
        return OperationResult.From(result, request.OperationId, result.Changes?.Revision ?? 0);
    }

    public async Task<OperationResult> DeleteElements(DeleteElementsRequest request, IDeleteElementsCommandHandler handler)
    {
        var result = await handler.Handle(
            new DeleteElementsCommand(Actor(), request.BoardId, request.ElementIds ?? [], request.OperationId),
            Context.ConnectionAborted);
        return OperationResult.From(result, request.OperationId, result.Changes?.Revision ?? 0);
    }

    public async Task<OperationResult> AddConnection(AddConnectionRequest request, IAddConnectionCommandHandler handler)
    {
        var result = await handler.Handle(
            new AddConnectionCommand(Actor(), request.BoardId, request.From, request.To, request.Label, request.Id, request.OperationId),
            Context.ConnectionAborted);
        return OperationResult.From(result, request.OperationId, result.Changes?.Revision ?? 0);
    }

    public async Task<OperationResult> DeleteConnections(DeleteConnectionsRequest request, IDeleteConnectionsCommandHandler handler)
    {
        var result = await handler.Handle(
            new DeleteConnectionsCommand(Actor(), request.BoardId, request.ConnectionIds ?? [], request.OperationId),
            Context.ConnectionAborted);
        return OperationResult.From(result, request.OperationId, result.Changes?.Revision ?? 0);
    }

    public async Task MoveCursor(Guid boardId, double x, double y, IMoveCursorCommandHandler handler)
    {
        var now = DateTimeOffset.UtcNow;
        if (Context.Items.TryGetValue(CursorItem, out var last) && last is DateTimeOffset previous && now - previous < CursorInterval)
        {
            return;
        }

        Context.Items[CursorItem] = now;
        await handler.Handle(new MoveCursorCommand(Context.ConnectionId, boardId, x, y), Context.ConnectionAborted);
    }

    public Task SetEditingFocus(Guid boardId, Guid? elementId, ISetEditingFocusCommandHandler handler) =>
        handler.Handle(new SetEditingFocusCommand(Context.ConnectionId, boardId, elementId), Context.ConnectionAborted);

    public Task ShareDragPreview(Guid boardId, IReadOnlyList<DragMoveWire>? moves, IShareDragPreviewCommandHandler handler) =>
        handler.Handle(
            new ShareDragPreviewCommand(Context.ConnectionId, boardId, (moves ?? []).Select(move => new DragMove(move.ElementId, move.X, move.Y)).ToList()),
            Context.ConnectionAborted);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Disconnect callbacks cannot take injected parameters, so the handler comes from this call's scope.
        var handler = (ILeaveBoardCommandHandler)services.GetService(typeof(ILeaveBoardCommandHandler))!;
        await handler.Handle(new LeaveBoardCommand(Context.ConnectionId), CancellationToken.None);

        await base.OnDisconnectedAsync(exception);
    }

    private Actor Actor()
    {
        var user = Context.User ?? throw new HubException("Not signed in.");
        var subject = user.FindFirstValue("sub") ?? throw new HubException("Not signed in.");
        return SharedKernel.Actor.Account(Guid.Parse(subject), user.FindFirstValue("name") ?? string.Empty);
    }

    private static ParticipantWire Wire(Participant participant) =>
        new(participant.ConnectionId, participant.AccountId, participant.DisplayName, participant.Color, participant.EditingElementId);
}
