using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Slices.AddElements;
using EventStorming.BoardModelling.Slices.ImportBoardDocument;
using EventStorming.BoardModelling.Slices.ListElementTypes;

namespace EventStorming.Mcp.Server;

/// <summary>Translation between the tools' shapes and the core's.</summary>
internal static class Views
{
    public static BoardLink Link(Board board, McpAdapterOptions options) =>
        new(board.Id, board.Name, BoardLevels.Name(board.Level), options.BoardUrl(board.Id));

    public static ElementView Of(Element element, Guid? swimlane = null, Guid? boundary = null) => new(
        element.Id, element.Type, element.Text, element.X, element.Y, element.Width, element.Height,
        element.Pivotal, element.Color, element.Version, swimlane, boundary);

    public static ConnectionView Of(Connection connection) => new(connection.Id, connection.From, connection.To, connection.Label);

    public static NotationView Of(Notation notation) => new(
        notation.Levels
            .Select(level => new LevelView(
                BoardLevels.Name(level.Level),
                level.Name,
                level.Description,
                notation.Types.Where(type => type.Levels.Contains(level.Level)).Select(type => type.Id).ToList()))
            .ToList(),
        notation.Types
            .Select(type => new ElementTypeView(
                type.Id,
                type.Name,
                type.Category == ElementCategory.Sticky ? "sticky" : "structure",
                type.Levels.Select(BoardLevels.Name).ToList(),
                type.CanBePivotal,
                type.LayoutRole.ToString().ToLowerInvariant(),
                type.MaxTextLength,
                type.Description,
                type.WhenToUse,
                type.WritingRule,
                type.Examples))
            .ToList());

    public static DocumentElement ToDocument(DraftElement draft) => new(
        draft.Type,
        draft.Text,
        draft.Key,
        draft.Position is { } position ? new Position(position.X, position.Y) : null,
        draft.Size is { } size ? new Size(size.Width, size.Height) : null,
        draft.Swimlane,
        draft.Boundary,
        draft.Anchor,
        draft.Pivotal,
        draft.Color);

    public static DocumentConnection ToDocument(DraftConnection draft) => new(draft.From, draft.To, draft.Label);

    public static NewElement ToNew(DraftElement draft) => new(
        draft.Type,
        draft.Text,
        Key: draft.Key,
        Position: draft.Position is { } position ? new Position(position.X, position.Y) : null,
        Size: draft.Size is { } size ? new Size(size.Width, size.Height) : null,
        Swimlane: draft.Swimlane,
        Boundary: draft.Boundary,
        Anchor: draft.Anchor,
        Pivotal: draft.Pivotal ?? false,
        Color: draft.Color);

    public static NewConnection ToNew(DraftConnection draft) => new(draft.From, draft.To, draft.Label);
}
