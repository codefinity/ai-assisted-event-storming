using System.Text;
using System.Text.Json;
using EventStorming.SharedKernel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace EventStorming.Mcp.Server;

/// <summary>
/// Tool results for a model to read. A refusal is a normal result with <c>isError</c> set, listing every
/// problem with the field it concerns and how to fix it, so the model can correct itself and retry.
/// </summary>
internal static class ToolResults
{
    public static CallToolResult Success<T>(string summary, T value)
    {
        var json = JsonSerializer.SerializeToElement(value, McpJson.Options);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = $"{summary}\n{json.GetRawText()}" }],
            StructuredContent = json,
        };
    }

    /// <param name="renameField">Maps the core's field path to the tool's argument, where they differ.</param>
    public static CallToolResult Refused(string tool, IReadOnlyList<Failure> failures, Func<string, string>? renameField = null)
    {
        var problems = failures
            .Select(failure => new
            {
                field = failure.Field is { } field && renameField is not null ? renameField(field) : failure.Field,
                code = failure.Code,
                kind = failure.Kind.ToString().ToLowerInvariant(),
                message = failure.Message,
                fix = failure.Fix,
            })
            .ToList();

        var text = new StringBuilder();
        text.Append(problems.Count == 1
            ? $"{tool} was refused. Nothing was changed. Fix this and call {tool} again:"
            : $"{tool} was refused with {problems.Count} problems. Nothing was changed. Fix them all and call {tool} again:");
        foreach (var problem in problems)
        {
            text.Append("\n- ");
            if (problem.field is not null)
            {
                text.Append(problem.field).Append(": ");
            }

            text.Append(problem.message).Append(" [").Append(problem.code).Append(']');
            if (problem.fix is not null)
            {
                text.Append(" Fix: ").Append(problem.fix);
            }
        }

        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = text.ToString() }],
            StructuredContent = JsonSerializer.SerializeToElement(new { problems }, McpJson.Options),
        };
    }

    public static CallToolResult Refused(string tool, Failure failure) => Refused(tool, [failure]);

    public static readonly Failure NotSignedIn = new(
        FailureKind.Unauthenticated, "unauthenticated", "The request carries no API key.",
        Fix: "Connect with the header 'Authorization: Bearer es_…' holding a team API key.");

    public static readonly Failure NeedsTeamKey = new(
        FailureKind.Forbidden, "team-api-key-required", "Creating a board needs a team API key.",
        Fix: "Connect with an API key created by a team Owner (team settings, API keys) with the 'write' scope.");

    public static Actor? Caller<T>(RequestContext<T> context, McpAdapterOptions options) =>
        context.User is { Identity.IsAuthenticated: true } user ? options.ActorFrom(user) : null;
}
