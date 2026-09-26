using System.ComponentModel;
using System.Text.Json;
using EventStorming.BoardModelling.Slices.ExportBoardDocument;
using EventStorming.BoardModelling.Slices.GetBoardSnapshot;
using EventStorming.BoardModelling.Slices.ListElementTypes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace EventStorming.Mcp.Server;

/// <summary>Reading material for agents: the modelling guide, the notation, and any board by id.</summary>
[McpServerResourceType]
public sealed class ModellingResources(McpAdapterOptions options)
{
    private static readonly Lazy<string> GuideText = new(() =>
    {
        using var stream = typeof(ModellingResources).Assembly.GetManifestResourceStream("llm-guide.md")
            ?? throw new InvalidOperationException("The embedded modelling guide is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    [McpServerResource(UriTemplate = "eventstorming://guide", Name = "modelling-guide", Title = "How to model with EventStorming", MimeType = "text/markdown")]
    [Description("The notation, the Board Document format the drawing tools use, a complete worked example, and the common mistakes. The HTTP endpoints it mentions are the REST equivalents of these tools.")]
    public static string Guide() => GuideText.Value;

    [McpServerResource(UriTemplate = "eventstorming://element-types", Name = "element-types", Title = "Element types and levels", MimeType = "application/json")]
    [Description("The same notation list_element_types returns.")]
    public static async Task<string> ElementTypes(IListElementTypesQueryHandler handler, CancellationToken cancellationToken)
    {
        var notation = (await handler.Handle(new ListElementTypesQuery(), cancellationToken)).Notation;
        return JsonSerializer.Serialize(Views.Of(notation), McpJson.Options);
    }

    [McpServerResource(UriTemplate = "eventstorming://boards/{boardId}", Name = "board", Title = "A board", MimeType = "application/json")]
    [Description("A board's elements and arrows, as get_board returns them.")]
    public async Task<string> Board(
        RequestContext<ReadResourceRequestParams> context,
        IGetBoardSnapshotQueryHandler snapshots,
        IExportBoardDocumentQueryHandler exports,
        string boardId,
        CancellationToken cancellationToken)
    {
        if (ToolResults.Caller(context, options) is not { } actor)
        {
            throw new McpException(ToolResults.NotSignedIn.Message);
        }

        if (!Guid.TryParse(boardId, out var id))
        {
            throw new McpException($"'{boardId}' is not a board id. Board ids are UUIDs; list_boards shows them.");
        }

        var (view, failures) = await BoardReader.Read(actor, id, snapshots, exports, options, cancellationToken);
        return view is null
            ? throw new McpException(string.Join(" ", failures.Select(failure => $"{failure.Message}{(failure.Fix is null ? string.Empty : $" {failure.Fix}")}")))
            : JsonSerializer.Serialize(view, McpJson.Options);
    }
}
