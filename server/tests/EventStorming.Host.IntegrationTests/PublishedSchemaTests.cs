using System.Text.Json.Nodes;
using EventStorming.Host.IntegrationTests.Support;
using Shouldly;
using Xunit;

namespace EventStorming.Host.IntegrationTests;

/// <summary>
/// The Board Document schema is checked in (docs/schemas) for readers who never run the API. This keeps
/// that copy identical to what the API serves. After a deliberate change, regenerate it with
/// UPDATE_PUBLISHED_SCHEMA=1 and review the diff.
/// </summary>
public sealed class PublishedSchemaTests(MongoServer mongo) : IAsyncLifetime
{
    private const string SchemaFile = "board-document.v1.schema.json";

    private ApiHost host = null!;

    public ValueTask InitializeAsync()
    {
        host = new ApiHost(mongo.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await host.DisposeAsync();

    [Fact]
    public async Task The_checked_in_schema_is_the_one_the_API_serves()
    {
        var served = await host.CreateClient().GetStringAsync("/api/v1/schemas/board-document.json");
        var path = Path.Combine(DocsSchemas(), SchemaFile);

        if (Environment.GetEnvironmentVariable("UPDATE_PUBLISHED_SCHEMA") == "1")
        {
            await File.WriteAllTextAsync(path, served.ReplaceLineEndings("\n") + "\n");
        }

        File.Exists(path).ShouldBeTrue($"{path} is missing. Run the tests once with UPDATE_PUBLISHED_SCHEMA=1 to write it.");
        var published = JsonNode.Parse(await File.ReadAllTextAsync(path));
        JsonNode.DeepEquals(published, JsonNode.Parse(served)).ShouldBeTrue(
            $"docs/schemas/{SchemaFile} no longer matches the API. If the change is intended, run the tests with UPDATE_PUBLISHED_SCHEMA=1 and commit the new file.");
    }

    private static string DocsSchemas()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "schemas");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("docs/schemas was not found above the test output folder.");
    }
}
