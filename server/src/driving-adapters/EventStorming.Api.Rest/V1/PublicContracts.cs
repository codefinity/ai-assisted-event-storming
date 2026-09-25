using System.ComponentModel;
using System.Text.Json.Serialization;
using EventStorming.Api.Rest.Http;
using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.Api.Rest.V1;

// The public API's resources (v1). Stable: fields are only ever added within v1. Request bodies
// refuse fields they do not know, so a misspelt field is reported instead of silently ignored.

public sealed record PublicBoard(
    Guid Id,
    string Name,
    [property: Description("big-picture, process-modelling or software-design.")] string Level,
    int ElementCount,
    [property: Description("Increases with every change to the board's content.")] long Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ActorResponse UpdatedBy,
    [property: Description("Set when the board is archived (read-only). Restore it with POST /boards/{boardId}/restore.")] DateTimeOffset? ArchivedAt)
{
    public static PublicBoard From(Board board) => new(
        board.Id, board.Name, BoardLevels.Name(board.Level), board.ElementCount, board.Revision,
        board.CreatedAt, board.UpdatedAt, ActorResponse.From(board.UpdatedBy), board.ArchivedAt);
}

public sealed record PublicElement(
    Guid Id,
    string Type,
    string Text,
    PositionDto Position,
    SizeDto Size,
    bool Pivotal,
    string? Color,
    [property: Description("Increases with every change. Send it back as expectedVersion to update only if nobody changed the element meanwhile.")] long Version,
    DateTimeOffset CreatedAt,
    ActorResponse CreatedBy,
    DateTimeOffset UpdatedAt,
    ActorResponse UpdatedBy)
{
    public static PublicElement From(Element element) => new(
        element.Id, element.Type, element.Text, new PositionDto(element.X, element.Y), new SizeDto(element.Width, element.Height),
        element.Pivotal, element.Color, element.Version, element.CreatedAt, ActorResponse.From(element.CreatedBy),
        element.UpdatedAt, ActorResponse.From(element.UpdatedBy));
}

public sealed record PublicConnection(Guid Id, Guid From, Guid To, string? Label, long Version, DateTimeOffset CreatedAt)
{
    public static PublicConnection Of(Connection connection) =>
        new(connection.Id, connection.From, connection.To, connection.Label, connection.Version, connection.CreatedAt);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreatePublicBoardRequest(
    [property: Description("The board's name.")] string? Name,
    [property: Description("big-picture, process-modelling or software-design.")] string? Level);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RenamePublicBoardRequest(string? Name);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateElementRequest(
    [property: Description("An element type id, e.g. \"domain-event\". See GET /api/v1/element-types.")] string? Type,
    string? Text,
    [property: Description("Leave out to place the element automatically, to the right of the existing content.")] PositionDto? Position,
    SizeDto? Size,
    [property: Description("Id of an existing swimlane element to place this element in.")] string? Swimlane,
    string? Boundary,
    [property: Description("Id of an existing element to stack this one below.")] string? Anchor,
    bool? Pivotal,
    string? Color);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BulkElementsRequest(
    [property: Description("Up to 500 elements, in timeline order. Keys let connections and other elements refer to them.")] IReadOnlyList<DocumentElementDto>? Elements,
    [property: Description("Connections between keys in this request, or ids of existing elements.")] IReadOnlyList<DocumentConnectionDto>? Connections);

public sealed record BulkElementsResponse(IReadOnlyList<PublicElement> Elements, IReadOnlyList<PublicConnection> Connections, IReadOnlyDictionary<string, Guid> Keys);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateElementRequestV1(
    string? Text,
    string? Type,
    PositionDto? Position,
    SizeDto? Size,
    bool? Pivotal,
    [property: Description("#RRGGBB, or \"\" to go back to the type's color.")] string? Color,
    [property: Description("Only update if the element is still at this version; otherwise 409 version-conflict.")] long? ExpectedVersion);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateConnectionRequest(
    [property: Description("Id of the element the arrow starts at.")] Guid? From,
    [property: Description("Id of the element the arrow points to.")] Guid? To,
    string? Label);

public sealed record ApiIndexResponse(string Name, string Version, string Documentation, string LlmGuide, string BoardDocumentSchema, string ElementTypes, string Boards);

internal static class FailurePaths
{
    /// <summary>A single-element request reuses the batch use case; its failures are renamed to the flat body's fields.</summary>
    public static IReadOnlyList<Failure> Unbatch(IReadOnlyList<Failure> failures, string prefix) =>
        failures.Select(failure => failure.Field is { } field && field.StartsWith(prefix, StringComparison.Ordinal)
            ? failure with { Field = field[prefix.Length..] }
            : failure).ToList();
}
