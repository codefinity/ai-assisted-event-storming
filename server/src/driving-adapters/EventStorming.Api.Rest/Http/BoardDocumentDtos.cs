using System.ComponentModel;
using System.Text.Json.Serialization;
using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Slices.ExportBoardDocument;
using EventStorming.BoardModelling.Slices.ImportBoardDocument;

namespace EventStorming.Api.Rest.Http;

// The Board Document - the published language of the public API, also used by the web app's import
// and export. Every field is optional on the way in, so a missing field is reported as a validation
// problem that names it rather than as an unreadable body.

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("A whole EventStorming board in one document. Array order of 'elements' is timeline order (left to right). Positions are optional: anything without one is laid out automatically.")]
public sealed record BoardDocumentDto(
    [property: Description("Format version. Always 1.")] int? Version,
    [property: Description("The board's name and level. Required when creating a board; when replacing, the name is optional and the level must match.")] BoardHeaderDto? Board,
    [property: Description("Every element on the board, in timeline order.")] IReadOnlyList<DocumentElementDto>? Elements,
    [property: Description("Optional arrows between elements, referring to element keys.")] IReadOnlyList<DocumentConnectionDto>? Connections)
{
    public BoardDocument ToModel() => new(
        Version,
        Board is null ? null : new DocumentBoard(Board.Name, Board.Level),
        Elements?.Select(element => element is null
            ? new DocumentElement(null)
            : new DocumentElement(
                element.Type,
                element.Text,
                element.Key,
                element.Position?.ToModel(),
                element.Size?.ToModel(),
                element.Swimlane,
                element.Boundary,
                element.Anchor,
                element.Pivotal,
                element.Color)).ToList(),
        Connections?.Select(connection => connection is null
            ? new DocumentConnection(null, null)
            : new DocumentConnection(connection.From, connection.To, connection.Label)).ToList());
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BoardHeaderDto(
    [property: Description("Ignored on import; present in exports.")] Guid? Id,
    [property: Description("The board's name, e.g. \"Online food ordering\".")] string? Name,
    [property: Description("One of: big-picture, process-modelling, software-design.")] string? Level);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DocumentElementDto(
    [property: Description("Your own name for this element, unique within the document, so other elements and connections can refer to it. Example: \"order-placed\".")] string? Key,
    [property: Description("An element type id from GET /api/v1/element-types, e.g. \"domain-event\".")] string? Type,
    [property: Description("What the sticky says. Domain Events in past tense: \"Order Placed\".")] string? Text,
    [property: Description("Top-left corner in board coordinates. Leave out to have the element placed on the timeline automatically.")] PositionDto? Position,
    [property: Description("Leave out to use the type's default size.")] SizeDto? Size,
    [property: Description("Key of a 'swimlane' element this element sits in.")] string? Swimlane,
    [property: Description("Key of a 'boundary' element (a bounded context) this element belongs to.")] string? Boundary,
    [property: Description("Key of another element to stack this one below, in the same column - e.g. a Hot Spot under the event it questions.")] string? Anchor,
    [property: Description("Marks a pivotal Domain Event - a turning point of the story. Only for domain-event.")] bool? Pivotal,
    [property: Description("Optional color override as #RRGGBB. Leave out to use the type's color.")] string? Color);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DocumentConnectionDto(
    [property: Description("Key of the element the arrow starts at.")] string? From,
    [property: Description("Key of the element the arrow points to.")] string? To,
    [property: Description("Optional label on the arrow.")] string? Label);

public sealed record ExportedElementDto(string Key, string Type, string Text, PositionDto Position, SizeDto Size, bool Pivotal, string? Color, string? Swimlane, string? Boundary);

public sealed record ExportedConnectionDto(string From, string To, string? Label);

public sealed record ExportedBoardDto(Guid Id, string Name, string Level);

/// <summary>An exported board: the same shape import accepts, with ids as keys and every position filled in.</summary>
public sealed record ExportedDocumentDto(int Version, ExportedBoardDto Board, IReadOnlyList<ExportedElementDto> Elements, IReadOnlyList<ExportedConnectionDto> Connections)
{
    public static ExportedDocumentDto From(ExportedDocument document) => new(
        document.Version,
        new ExportedBoardDto(document.Board.Id, document.Board.Name, BoardLevels.Name(document.Board.Level)),
        document.Elements.Select(element => new ExportedElementDto(
            element.Key,
            element.Type,
            element.Text,
            new PositionDto(element.Position.X, element.Position.Y),
            new SizeDto(element.Size.Width, element.Size.Height),
            element.Pivotal,
            element.Color,
            element.Swimlane,
            element.Boundary)).ToList(),
        document.Connections.Select(connection => new ExportedConnectionDto(connection.From, connection.To, connection.Label)).ToList());
}

/// <param name="Keys">Each key used in the document, mapped to the id its element was given.</param>
public sealed record ImportedBoardResponse(BoardResponse Board, IReadOnlyDictionary<string, Guid> Keys, int ElementCount, int ConnectionCount)
{
    public static ImportedBoardResponse From(ImportedBoard imported) =>
        new(BoardResponse.From(imported.Board), imported.KeyedIds, imported.ElementCount, imported.ConnectionCount);
}
