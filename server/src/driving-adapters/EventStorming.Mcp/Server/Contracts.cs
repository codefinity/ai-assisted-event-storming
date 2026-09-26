using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;

namespace EventStorming.Mcp.Server;

// What the tools take and return. The element and connection shapes are the Board Document's (see
// docs/llm-guide.md), so an agent that knows the public API already knows these.

internal static class McpJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        RespectNullableAnnotations = true,
        // Plain numbers in the schema: models should not be told a coordinate may be a string.
        NumberHandling = JsonNumberHandling.Strict,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Input schemas that say what may really be null: a non-nullable property, or an item of a list, is
    /// described without "null", so a model is never invited to send one.
    /// </summary>
    public static readonly AIJsonSchemaCreateOptions Schema = new()
    {
        TransformSchemaNode = (context, node) =>
        {
            var neverNull = context.PropertyInfo is { } property
                ? !property.IsGetNullable
                : context.Path.Length > 0 && context.Path[^1] == "items";
            if (neverNull && node is JsonObject schema && schema["type"] is JsonArray types)
            {
                var kept = types.Select(type => type!.GetValue<string>()).Where(type => type != "null").ToList();
                schema["type"] = kept.Count == 1 ? kept[0] : new JsonArray(kept.Select(type => (JsonNode)type).ToArray());
            }

            return node;
        },
    };
}

public sealed record PositionInput(
    [property: Description("Board x of the top-left corner. Time runs left to right.")] double X,
    [property: Description("Board y of the top-left corner. y grows downwards.")] double Y);

public sealed record SizeInput(double Width, double Height);

public sealed record DraftElement(
    [property: Description("An element type id from list_element_types, e.g. \"domain-event\", \"hot-spot\", \"swimlane\".")] string Type,
    [property: Description("What the sticky says. Domain Events in the past tense (\"Order Placed\"), Commands in the imperative (\"Place Order\"). For a swimlane or boundary, its label.")] string? Text = null,
    [property: Description("Your own short name for this element, unique in the request (e.g. \"order-placed\"), so other elements and connections can refer to it.")] string? Key = null,
    [property: Description("Leave out to have the element laid out automatically on the timeline; the array order is the time order.")] PositionInput? Position = null,
    [property: Description("Leave out for the type's default size.")] SizeInput? Size = null,
    [property: Description("Key (or existing id) of a swimlane element this element sits in.")] string? Swimlane = null,
    [property: Description("Key (or existing id) of a boundary element (a bounded context) this element belongs to.")] string? Boundary = null,
    [property: Description("Key (or existing id) of an element to stack this one below, in the same column, e.g. a Hot Spot under the event it questions.")] string? Anchor = null,
    [property: Description("true marks a pivotal Domain Event: a turning point between phases. Only for domain-event.")] bool? Pivotal = null,
    [property: Description("Optional #RRGGBB override of the type's color. Rarely needed.")] string? Color = null);

public sealed record DraftConnection(
    [property: Description("Key of an element in this request, or the id of an element already on the board.")] string From,
    [property: Description("Key of an element in this request, or the id of an element already on the board.")] string To,
    [property: Description("Optional label on the arrow.")] string? Label = null);

public sealed record ElementMoveInput(
    [property: Description("The element's id.")] Guid ElementId,
    [property: Description("New board x of its top-left corner.")] double X,
    [property: Description("New board y of its top-left corner.")] double Y);

public sealed record BoardLink(Guid Id, string Name, string Level, string Url);

public sealed record BoardListing(Guid Id, string Name, string Level, int ElementCount, DateTimeOffset UpdatedAt, bool Archived, string Url);

public sealed record BoardList(IReadOnlyList<BoardListing> Boards, string? NextCursor);

/// <param name="Swimlane">The id of the swimlane the element sits in, if any.</param>
/// <param name="Boundary">The id of the boundary the element sits in, if any.</param>
public sealed record ElementView(
    Guid Id,
    string Type,
    string Text,
    double X,
    double Y,
    double Width,
    double Height,
    bool Pivotal,
    string? Color,
    long Version,
    Guid? Swimlane = null,
    Guid? Boundary = null);

public sealed record ConnectionView(Guid Id, Guid From, Guid To, string? Label);

public sealed record BoardView(BoardLink Board, long Revision, bool CanEdit, bool Archived, IReadOnlyList<ElementView> Elements, IReadOnlyList<ConnectionView> Connections);

public sealed record DrawnBoard(BoardLink Board, int ElementCount, int ConnectionCount, IReadOnlyDictionary<string, Guid> Keys);

/// <param name="Resized">Swimlanes and boundaries already on the board that grew to hold the new elements.</param>
public sealed record AddedContent(string Url, IReadOnlyDictionary<string, Guid> Keys, IReadOnlyList<ElementView> Elements, IReadOnlyList<ConnectionView> Connections, IReadOnlyList<ElementView> Resized);

public sealed record ChangedContent(string Url, IReadOnlyList<ElementView> Elements);

public sealed record RemovedContent(string Url, IReadOnlyList<Guid> RemovedElements, IReadOnlyList<Guid> RemovedConnections);

public sealed record LevelView(string Id, string Name, string Description, IReadOnlyList<string> Palette);

public sealed record ElementTypeView(
    string Id,
    string Name,
    string Category,
    IReadOnlyList<string> Levels,
    bool CanBePivotal,
    string LayoutRole,
    int MaxTextLength,
    string Description,
    string WhenToUse,
    string? WritingRule,
    IReadOnlyList<string> Examples);

public sealed record NotationView(IReadOnlyList<LevelView> Levels, IReadOnlyList<ElementTypeView> Types);
