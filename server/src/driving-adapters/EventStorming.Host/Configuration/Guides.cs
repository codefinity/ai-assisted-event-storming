using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http.HttpResults;

namespace EventStorming.Host.Configuration;

/// <summary>
/// Serves the Markdown guides (docs/api.md, docs/llm-guide.md) from the running API, so an agent that
/// only knows the API's address can find them. They are embedded at build time, never read from disk.
/// </summary>
internal static class Guides
{
    private static readonly string[] Names = ["api.md", "llm-guide.md"];
    private static readonly ConcurrentDictionary<string, string?> Cache = new(StringComparer.Ordinal);

    public static void MapGuides(this IEndpointRouteBuilder endpoints)
    {
        foreach (var name in Names)
        {
            endpoints.MapGet($"/docs/{name}", Results<ContentHttpResult, NotFound> () =>
                    Read(name) is { } text ? TypedResults.Text(text, "text/markdown; charset=utf-8") : TypedResults.NotFound())
                .AllowAnonymous()
                .ExcludeFromDescription();
        }
    }

    private static string? Read(string name) => Cache.GetOrAdd(name, static resource =>
    {
        using var stream = typeof(Guides).Assembly.GetManifestResourceStream($"guides/{resource}");
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
}
