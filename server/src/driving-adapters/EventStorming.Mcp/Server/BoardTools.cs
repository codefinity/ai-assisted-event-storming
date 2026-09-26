using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Slices.AddConnection;
using EventStorming.BoardModelling.Slices.AddElements;
using EventStorming.BoardModelling.Slices.DeleteConnections;
using EventStorming.BoardModelling.Slices.DeleteElements;
using EventStorming.BoardModelling.Slices.ExportBoardDocument;
using EventStorming.BoardModelling.Slices.GetBoardSnapshot;
using EventStorming.BoardModelling.Slices.ImportBoardDocument;
using EventStorming.BoardModelling.Slices.ListBoards;
using EventStorming.BoardModelling.Slices.ListElementTypes;
using EventStorming.BoardModelling.Slices.MoveElements;
using EventStorming.BoardModelling.Slices.UpdateElement;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace EventStorming.Mcp.Server;

/// <summary>
/// The tools. Each one translates its arguments into a Board Modelling command or query, and the result
/// (or every refusal, with its fix) back. Use case handlers are resolved per call.
/// </summary>
[McpServerToolType]
public sealed partial class BoardTools(McpAdapterOptions options)
{
    private const string Levels = "big-picture, process-modelling or software-design";

    [McpServerTool(Name = "list_element_types", Title = "List the EventStorming notation", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("The notation this server draws with: every element type (what it means, when to use it, how to write it, whether it can be pivotal) and the three levels with the types each level's palette offers. Call this before drawing and use only these type ids.")]
    public async Task<CallToolResult> ListElementTypes(IListElementTypesQueryHandler handler, CancellationToken cancellationToken)
    {
        var notation = (await handler.Handle(new ListElementTypesQuery(), cancellationToken)).Notation;
        return ToolResults.Success($"{notation.Types.Count} element types and {notation.Levels.Count} levels.", Views.Of(notation));
    }

    [McpServerTool(Name = "list_boards", Title = "List boards", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("The team's boards, most recently changed first, each with its id, level, size and a link people can open.")]
    public async Task<CallToolResult> ListBoards(
        RequestContext<CallToolRequestParams> context,
        IListBoardsQueryHandler handler,
        [Description("Also list archived (read-only) boards.")] bool includeArchived = false,
        [Description("The nextCursor of a previous call, for the next page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (ToolResults.Caller(context, options) is not { } actor) return ToolResults.Refused("list_boards", ToolResults.NotSignedIn);
        if (actor.TeamId is not { } teamId) return ToolResults.Refused("list_boards", ToolResults.NeedsTeamKey);

        var result = await handler.Handle(new ListBoardsQuery(actor, teamId, includeArchived, cursor), cancellationToken);
        if (!result.Success) return ToolResults.Refused("list_boards", result.Failures);

        var page = result.Page!;
        var boards = page.Items
            .Select(board => new BoardListing(board.Id, board.Name, BoardLevels.Name(board.Level), board.ElementCount, board.UpdatedAt, board.ArchivedAt is not null, options.BoardUrl(board.Id)))
            .ToList();
        return ToolResults.Success(
            boards.Count == 0 ? "The team has no boards yet." : $"{boards.Count} boards{(page.NextCursor is null ? "." : "; pass nextCursor for more.")}",
            new BoardList(boards, page.NextCursor));
    }

    [McpServerTool(Name = "create_board", Title = "Draw a new board", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("""
        Draws a new EventStorming board in one call. Describe the story; the server lays it out:
        - elements are placed left to right in array order: the array is the timeline, across all swimlanes;
        - an element with an anchor stacks below that element instead of taking a new column;
        - elements naming a swimlane sit in that horizontal band; list swimlanes first, top to bottom;
        - each boundary becomes a box around the elements that name it;
        - pivotal Domain Events get extra space before them;
        - give a position only to pin an element somewhere.
        Give elements keys so anchors, swimlanes, boundaries and connections can refer to them.
        Everything is checked at once: a refusal lists every problem with its fix, and nothing is created.
        Returns the board's id, a link to open it, and the id given to each key.
        """)]
    public async Task<CallToolResult> CreateBoard(
        RequestContext<CallToolRequestParams> context,
        IImportBoardDocumentCommandHandler handler,
        [Description("The board's name, e.g. \"Online food ordering\".")] string name,
        [Description($"The level: {Levels}. It decides the palette and cannot change later.")]
        [AllowedValues("big-picture", "process-modelling", "software-design")] string level,
        [Description("The elements in timeline order. May be empty for an empty board.")] IReadOnlyList<DraftElement>? elements = null,
        [Description("Arrows between elements, by key.")] IReadOnlyList<DraftConnection>? connections = null,
        CancellationToken cancellationToken = default)
    {
        if (ToolResults.Caller(context, options) is not { } actor) return ToolResults.Refused("create_board", ToolResults.NotSignedIn);
        if (actor.TeamId is not { } teamId) return ToolResults.Refused("create_board", ToolResults.NeedsTeamKey);

        var document = new BoardDocument(
            1,
            new DocumentBoard(name, level),
            (elements ?? []).Select(Views.ToDocument).ToList(),
            (connections ?? []).Select(Views.ToDocument).ToList());
        var result = await handler.Handle(new ImportBoardDocumentCommand(actor, document, TeamId: teamId), cancellationToken);
        if (!result.Success) return ToolResults.Refused("create_board", result.Failures, BoardField);

        var imported = result.Imported!;
        var link = Views.Link(imported.Board, options);
        return ToolResults.Success(
            $"Drew \"{link.Name}\" with {Count(imported.ElementCount, "element", "elements")} and {Count(imported.ConnectionCount, "arrow", "arrows")}. Open it: {link.Url}",
            new DrawnBoard(link, imported.ElementCount, imported.ConnectionCount, imported.KeyedIds));
    }

    [McpServerTool(Name = "get_board", Title = "Read a board", ReadOnly = true, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Reads a board: every element (id, type, text, position, size, pivotal, version, and the ids of the swimlane and boundary it sits in) in timeline order, and every arrow with its id. Use the ids to change, move, connect or delete things.")]
    public async Task<CallToolResult> GetBoard(
        RequestContext<CallToolRequestParams> context,
        IGetBoardSnapshotQueryHandler snapshots,
        IExportBoardDocumentQueryHandler exports,
        [Description("The board's id.")] Guid boardId,
        CancellationToken cancellationToken = default)
    {
        if (ToolResults.Caller(context, options) is not { } actor) return ToolResults.Refused("get_board", ToolResults.NotSignedIn);

        var (view, failures) = await BoardReader.Read(actor, boardId, snapshots, exports, options, cancellationToken);
        return view is null
            ? ToolResults.Refused("get_board", failures)
            : ToolResults.Success($"\"{view.Board.Name}\" ({view.Board.Level}): {view.Elements.Count} elements, {view.Connections.Count} arrows.", view);
    }

    [McpServerTool(Name = "add_to_board", Title = "Add to a board", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("""
        Adds elements and arrows to an existing board, laid out like create_board: new elements continue
        the timeline to the right of what is already there, in array order. Anchors, swimlanes,
        boundaries and connections may name keys in this call or ids of elements already on the board
        (see get_board). Up to 500 elements per call. People with the board open see them appear.
        """)]
    public async Task<CallToolResult> AddToBoard(
        RequestContext<CallToolRequestParams> context,
        IAddElementsCommandHandler handler,
        [Description("The board's id.")] Guid boardId,
        [Description("The new elements, in timeline order.")] IReadOnlyList<DraftElement> elements,
        [Description("New arrows, by key or existing id.")] IReadOnlyList<DraftConnection>? connections = null,
        CancellationToken cancellationToken = default)
    {
        if (ToolResults.Caller(context, options) is not { } actor) return ToolResults.Refused("add_to_board", ToolResults.NotSignedIn);

        var result = await handler.Handle(
            new AddElementsCommand(actor, boardId, elements.Select(Views.ToNew).ToList(), (connections ?? []).Select(Views.ToNew).ToList()),
            cancellationToken);
        if (!result.Success) return ToolResults.Refused("add_to_board", result.Failures);

        var added = result.Added!;
        var url = options.BoardUrl(boardId);
        var grew = added.Grown.Count == 0 ? string.Empty : $" {Count(added.Grown.Count, "swimlane or boundary", "swimlanes and boundaries")} grew to hold them.";
        return ToolResults.Success(
            $"Added {Count(added.Created.Count, "element", "elements")} and {Count(added.Changes.Connections.Count, "arrow", "arrows")}.{grew} Open it: {url}",
            new AddedContent(
                url,
                added.KeyedIds,
                added.Created.Select(element => Views.Of(element)).ToList(),
                added.Changes.Connections.Select(Views.Of).ToList(),
                added.Grown.Select(element => Views.Of(element)).ToList()));
    }

    [McpServerTool(Name = "update_element", Title = "Change an element", ReadOnly = false, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Changes one element: only the fields given change. Changing to a type that cannot be pivotal clears pivotal. Pass expectedVersion (from get_board) to change it only if nobody else has since.")]
    public async Task<CallToolResult> UpdateElement(
        RequestContext<CallToolRequestParams> context,
        IUpdateElementCommandHandler handler,
        [Description("The board's id.")] Guid boardId,
        [Description("The element's id.")] Guid elementId,
        [Description("New text.")] string? text = null,
        [Description("New type id, from list_element_types.")] string? type = null,
        [Description("New top-left position.")] PositionInput? position = null,
        [Description("New size.")] SizeInput? size = null,
        [Description("Mark or unmark as a pivotal Domain Event.")] bool? pivotal = null,
        [Description("#RRGGBB, or an empty string to go back to the type's color.")] string? color = null,
        [Description("Only change the element if it is still at this version.")] long? expectedVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (ToolResults.Caller(context, options) is not { } actor) return ToolResults.Refused("update_element", ToolResults.NotSignedIn);

        var result = await handler.Handle(
            new UpdateElementCommand(
                actor, boardId, elementId, text, type,
                position is null ? null : new Position(position.X, position.Y),
                size is null ? null : new Size(size.Width, size.Height),
                pivotal, color, expectedVersion),
            cancellationToken);
        if (!result.Success) return ToolResults.Refused("update_element", result.Failures);

        var changed = result.Changes!.Elements.Select(element => Views.Of(element)).ToList();
        return ToolResults.Success("Changed.", new ChangedContent(options.BoardUrl(boardId), changed));
    }

    [McpServerTool(Name = "move_elements", Title = "Move elements", ReadOnly = false, Idempotent = true, Destructive = false, OpenWorld = false)]
    [Description("Moves elements to new positions in one change, e.g. to tidy a timeline. Positions are top-left corners in board coordinates; x grows to the right (later in time), y downwards.")]
    public async Task<CallToolResult> MoveElements(
        RequestContext<CallToolRequestParams> context,
        IMoveElementsCommandHandler handler,
        [Description("The board's id.")] Guid boardId,
        [Description("Where each element goes.")] IReadOnlyList<ElementMoveInput> moves,
        CancellationToken cancellationToken = default)
    {
        if (ToolResults.Caller(context, options) is not { } actor) return ToolResults.Refused("move_elements", ToolResults.NotSignedIn);

        var result = await handler.Handle(
            new MoveElementsCommand(actor, boardId, moves.Select(move => new ElementMove(move.ElementId, new Position(move.X, move.Y))).ToList()),
            cancellationToken);
        if (!result.Success) return ToolResults.Refused("move_elements", result.Failures, field => MovePosition().Replace(field, "$1"));

        var moved = result.Changes!.Elements.Select(element => Views.Of(element)).ToList();
        return ToolResults.Success(
            moved.Count == moves.Count ? $"Moved {moved.Count} elements." : $"Moved {moved.Count} of {moves.Count}; the others are not on the board.",
            new ChangedContent(options.BoardUrl(boardId), moved));
    }

    [McpServerTool(Name = "delete_elements", Title = "Delete elements", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Deletes elements from a board, with every arrow that touches them.")]
    public async Task<CallToolResult> DeleteElements(
        RequestContext<CallToolRequestParams> context,
        IDeleteElementsCommandHandler handler,
        [Description("The board's id.")] Guid boardId,
        [Description("The ids of the elements to delete.")] IReadOnlyList<Guid> elementIds,
        CancellationToken cancellationToken = default)
    {
        if (ToolResults.Caller(context, options) is not { } actor) return ToolResults.Refused("delete_elements", ToolResults.NotSignedIn);

        var result = await handler.Handle(new DeleteElementsCommand(actor, boardId, elementIds), cancellationToken);
        if (!result.Success) return ToolResults.Refused("delete_elements", result.Failures);

        var changes = result.Changes!;
        return ToolResults.Success(
            $"Deleted {changes.RemovedElements.Count} elements and {changes.RemovedConnections.Count} arrows.",
            new RemovedContent(options.BoardUrl(boardId), changes.RemovedElements.Select(removed => removed.Id).ToList(), changes.RemovedConnections.Select(removed => removed.Id).ToList()));
    }

    [McpServerTool(Name = "connect_elements", Title = "Draw an arrow", ReadOnly = false, Idempotent = false, Destructive = false, OpenWorld = false)]
    [Description("Draws an arrow from one element to another already on the board. One arrow per direction between two elements.")]
    public async Task<CallToolResult> ConnectElements(
        RequestContext<CallToolRequestParams> context,
        IAddConnectionCommandHandler handler,
        [Description("The board's id.")] Guid boardId,
        [Description("Id of the element the arrow starts at.")] Guid from,
        [Description("Id of the element the arrow points to.")] Guid to,
        [Description("Optional label on the arrow.")] string? label = null,
        CancellationToken cancellationToken = default)
    {
        if (ToolResults.Caller(context, options) is not { } actor) return ToolResults.Refused("connect_elements", ToolResults.NotSignedIn);

        var result = await handler.Handle(new AddConnectionCommand(actor, boardId, from, to, label), cancellationToken);
        if (!result.Success) return ToolResults.Refused("connect_elements", result.Failures);

        var connection = result.Changes!.Connections.FirstOrDefault();
        return connection is null
            ? ToolResults.Success("That arrow is already on the board.", new { from, to })
            : ToolResults.Success("Connected.", Views.Of(connection));
    }

    [McpServerTool(Name = "delete_connections", Title = "Delete arrows", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Deletes arrows, by their ids (see get_board).")]
    public async Task<CallToolResult> DeleteConnections(
        RequestContext<CallToolRequestParams> context,
        IDeleteConnectionsCommandHandler handler,
        [Description("The board's id.")] Guid boardId,
        [Description("The ids of the arrows to delete.")] IReadOnlyList<Guid> connectionIds,
        CancellationToken cancellationToken = default)
    {
        if (ToolResults.Caller(context, options) is not { } actor) return ToolResults.Refused("delete_connections", ToolResults.NotSignedIn);

        var result = await handler.Handle(new DeleteConnectionsCommand(actor, boardId, connectionIds), cancellationToken);
        if (!result.Success) return ToolResults.Refused("delete_connections", result.Failures);

        var removed = result.Changes!.RemovedConnections.Select(connection => connection.Id).ToList();
        return ToolResults.Success($"Deleted {removed.Count} arrows.", new RemovedContent(options.BoardUrl(boardId), [], removed));
    }

    [McpServerTool(Name = "replace_board_content", Title = "Redraw a board", ReadOnly = false, Idempotent = true, Destructive = true, OpenWorld = false)]
    [Description("Replaces everything on a board with a new diagram, laid out like create_board, in one change. Use it to redraw a board from scratch; to add or edit a little, prefer add_to_board and update_element. The level stays the same.")]
    public async Task<CallToolResult> ReplaceBoardContent(
        RequestContext<CallToolRequestParams> context,
        IImportBoardDocumentCommandHandler handler,
        [Description("The board's id.")] Guid boardId,
        [Description("The new elements, in timeline order.")] IReadOnlyList<DraftElement> elements,
        [Description("Arrows between elements, by key.")] IReadOnlyList<DraftConnection>? connections = null,
        [Description("A new name for the board, if it should change.")] string? name = null,
        CancellationToken cancellationToken = default)
    {
        if (ToolResults.Caller(context, options) is not { } actor) return ToolResults.Refused("replace_board_content", ToolResults.NotSignedIn);

        var document = new BoardDocument(
            1,
            name is null ? null : new DocumentBoard(name),
            elements.Select(Views.ToDocument).ToList(),
            (connections ?? []).Select(Views.ToDocument).ToList());
        var result = await handler.Handle(new ImportBoardDocumentCommand(actor, document, BoardId: boardId), cancellationToken);
        if (!result.Success) return ToolResults.Refused("replace_board_content", result.Failures, BoardField);

        var imported = result.Imported!;
        var link = Views.Link(imported.Board, options);
        return ToolResults.Success(
            $"Redrew \"{link.Name}\" with {Count(imported.ElementCount, "element", "elements")} and {Count(imported.ConnectionCount, "arrow", "arrows")}. Open it: {link.Url}",
            new DrawnBoard(link, imported.ElementCount, imported.ConnectionCount, imported.KeyedIds));
    }

    private static string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    /// <summary>The document's board header is the tool's own name and level arguments.</summary>
    private static string BoardField(string field) => field.StartsWith("board.", StringComparison.Ordinal) ? field["board.".Length..] : field;

    [GeneratedRegex(@"^(moves\[\d+\])\.position")]
    private static partial Regex MovePosition();
}
