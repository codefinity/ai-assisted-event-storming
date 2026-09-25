using System.ComponentModel;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Schema;
using EventStorming.Api.Rest.Http;
using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Slices.ListElementTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace EventStorming.Api.Rest.V1;

/// <summary>Self-description for machine readers: the Board Document's JSON Schema, and every problem type.</summary>
internal static class DocsEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/schemas/board-document.json", async (IListElementTypesQueryHandler handler, CancellationToken cancellationToken) =>
            {
                var notation = (await handler.Handle(new ListElementTypesQuery(), cancellationToken)).Notation;
                return Results.Text(BoardDocumentSchema.Build(notation).ToJsonString(BoardDocumentSchema.Output), "application/schema+json");
            })
            .AllowAnonymous()
            .WithTags("About")
            .WithName("BoardDocumentSchema").WithSummary("JSON Schema (2020-12) of the Board Document, with the current element types")
            .Produces<JsonObject>(StatusCodes.Status200OK, "application/schema+json");

        api.MapGet("/problems", () => TypedResults.Ok(ProblemCatalog.All))
            .AllowAnonymous()
            .WithTags("About")
            .WithName("ListProblemTypes").WithSummary("Every problem type the API can return, and what to do about it");

        api.MapGet("/problems/{slug}", Results<Ok<ProblemType>, ProblemHttpResult> (string slug, HttpContext http) =>
                ProblemCatalog.All.FirstOrDefault(type => type.Slug == slug) is { } type
                    ? TypedResults.Ok(type)
                    : Problems.Of(ProblemCatalog.NotFound, http, $"There is no problem type '{slug}'.", "not-found", "See GET /api/v1/problems for all of them."))
            .AllowAnonymous()
            .WithTags("About")
            .WithName("GetProblemType").WithSummary("One problem type, explained");
    }
}

/// <summary>
/// Generates the Board Document's JSON Schema from the very DTOs the API binds, so the schema cannot
/// drift from what the server accepts. The element-type and level enums come from the registry.
/// </summary>
public static class BoardDocumentSchema
{
    public const string Id = "https://eventstorming.local/schemas/board-document.v1.json";

    /// <summary>Indented, and without escaping quotes and apostrophes: it is read by people and models, never embedded in HTML.</summary>
    public static readonly JsonSerializerOptions Output = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static JsonObject Build(Notation notation)
    {
        // The API reads numbers written as strings too, but documents should use plain numbers: describe those.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.Strict,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        var exporter = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (context, node) =>
            {
                if (node is not JsonObject schema)
                {
                    return node;
                }

                // Every field is optional on the wire, but "null" in every type makes the schema harder
                // to read; absence is how a field is left out.
                if (schema["type"] is JsonArray types && types.Any(type => type?.GetValue<string>() == "null"))
                {
                    var nonNull = types.Select(type => type!.GetValue<string>()).Where(type => type != "null").ToList();
                    schema["type"] = nonNull.Count == 1 ? nonNull[0] : new JsonArray(nonNull.Select(type => (JsonNode)type).ToArray());
                }

                ICustomAttributeProvider? provider = context.PropertyInfo?.AttributeProvider;
                if (context.PropertyInfo is null && context.TypeInfo.Type.Name.EndsWith("Dto", StringComparison.Ordinal))
                {
                    provider = context.TypeInfo.Type;
                }

                if (provider?.GetCustomAttributes(typeof(DescriptionAttribute), inherit: true).OfType<DescriptionAttribute>().FirstOrDefault() is { } description)
                {
                    schema.Insert(0, "description", description.Description);
                }

                return schema;
            },
        };

        var root = options.GetJsonSchemaAsNode(typeof(BoardDocumentDto), exporter).AsObject();
        root.Insert(0, "$schema", "https://json-schema.org/draft/2020-12/schema");
        root.Insert(1, "$id", Id);
        root.Insert(2, "title", "EventStorming Board Document, version 1");

        // The exporter marks every constructor parameter required. On the wire only these are:
        // an element's type and a connection's ends (plus both halves of a position or size).
        root.Remove("required");

        var properties = root["properties"]!.AsObject();
        var version = properties["version"]!.AsObject();
        properties["version"] = new JsonObject { ["description"] = version["description"]?.GetValue<string>(), ["const"] = 1 };

        properties["board"]!.AsObject().Remove("required");
        var board = properties["board"]!.AsObject()["properties"]!.AsObject();
        board["level"]!.AsObject()["enum"] = new JsonArray(notation.Levels.Select(level => (JsonNode)BoardLevels.Name(level.Level)).ToArray());

        var element = properties["elements"]!.AsObject()["items"]!.AsObject();
        element["required"] = new JsonArray("type");
        element["properties"]!.AsObject()["type"]!.AsObject()["enum"] = new JsonArray(notation.Types.Select(type => (JsonNode)type.Id).ToArray());

        var connection = properties["connections"]!.AsObject()["items"]!.AsObject();
        connection["required"] = new JsonArray("from", "to");

        return root;
    }
}
