using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EventStorming.Host.IntegrationTests.Support;
using Shouldly;
using Xunit;

namespace EventStorming.Host.IntegrationTests;

public sealed class PublicApiTests(MongoServer mongo) : IAsyncLifetime
{
    private const string FoodOrdering = """
        {
          "version": 1,
          "board": { "name": "Online food ordering", "level": "big-picture" },
          "elements": [
            { "key": "customer", "type": "swimlane", "text": "Customer" },
            { "key": "placed", "type": "domain-event", "text": "Order Placed", "swimlane": "customer", "pivotal": true },
            { "key": "paid", "type": "domain-event", "text": "Payment Taken", "swimlane": "customer" },
            { "key": "late", "type": "hot-spot", "text": "What if the kitchen is busy?", "anchor": "paid" }
          ],
          "connections": [ { "from": "placed", "to": "paid" } ]
        }
        """;

    private ApiHost host = null!;

    public ValueTask InitializeAsync()
    {
        host = new ApiHost(mongo.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await host.DisposeAsync();

    [Fact]
    public async Task A_document_becomes_a_laid_out_board_that_exports_back_to_the_same_format()
    {
        var (_, key) = await TeamWithKey("read", "write");
        var api = host.ApiKeyClient(key);

        var created = await api.PostAsync("/api/v1/boards/import", Json(FoodOrdering));
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        created.Headers.Location!.ToString().ShouldStartWith("/api/v1/boards/");
        var imported = await created.Content.ReadFromJsonAsync<JsonElement>();
        imported.GetProperty("elementCount").GetInt32().ShouldBe(4);
        var boardId = imported.GetProperty("board").GetProperty("id").GetGuid();

        var document = await api.GetFromJsonAsync<JsonElement>($"/api/v1/boards/{boardId}/document");
        var elements = document.GetProperty("elements").EnumerateArray().ToList();
        elements.Count.ShouldBe(4);
        elements.ShouldAllBe(element => element.GetProperty("position").ValueKind == JsonValueKind.Object);
        var placed = elements.Single(element => element.GetProperty("text").GetString() == "Order Placed");
        placed.GetProperty("pivotal").GetBoolean().ShouldBeTrue();
        placed.GetProperty("swimlane").GetString().ShouldNotBeNull();

        // The export is itself a valid document.
        var reimported = await api.PostAsync("/api/v1/boards/import", Json(document.GetRawText().Replace("Online food ordering", "Copy", StringComparison.Ordinal)));
        reimported.StatusCode.ShouldBe(HttpStatusCode.Created, await reimported.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Mistakes_come_back_all_at_once_with_pointers_and_fixes_an_llm_can_act_on()
    {
        var (_, key) = await TeamWithKey("read", "write");
        var response = await host.ApiKeyClient(key).PostAsync("/api/v1/boards/import", Json("""
            {
              "board": { "name": "Oops", "level": "big-picture" },
              "elements": [
                { "key": "placed", "type": "event", "text": "Order Placed" },
                { "key": "q", "type": "hot-spot", "text": "Why?", "anchor": "plced" }
              ]
            }
            """));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = problem.GetProperty("errors").EnumerateArray().ToDictionary(error => error.GetProperty("pointer").GetString()!);
        errors["#/elements/0/type"].GetProperty("fix").GetString()!.ShouldContain("domain-event");
        errors["#/elements/1/anchor"].GetProperty("fix").GetString()!.ShouldContain("Did you mean 'placed'?");
    }

    [Fact]
    public async Task A_misspelt_field_is_reported_instead_of_ignored()
    {
        var (_, key) = await TeamWithKey("read", "write");
        var response = await host.ApiKeyClient(key).PostAsync("/api/v1/boards/import",
            Json("""{ "board": { "name": "x", "level": "big-picture" }, "elements": [ { "type": "domain-event", "txt": "Order Placed" } ] }"""));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("pointer").GetString().ShouldBe("#/elements/0/txt");
        problem.GetProperty("detail").GetString()!.ShouldContain("'txt' is not a field here");
    }

    [Theory]
    [InlineData("/api/v1/boards", """{ "name": "x", "level": "big-picture", "colour": "red" }""", "#/colour")]
    [InlineData("/api/v1/boards/{board}/elements", """{ "type": "domain-event", "text": "Order Placed", "position": { "x": 1, "y": 2, "z": 3 } }""", "#/position/z")]
    [InlineData("/api/v1/boards/{board}/elements/bulk", """{ "elements": [], "links": [] }""", "#/links")]
    public async Task Every_request_body_reports_a_field_it_does_not_know(string path, string body, string pointer)
    {
        var (_, key) = await TeamWithKey("read", "write");
        var api = host.ApiKeyClient(key);
        var board = await (await api.PostAsync("/api/v1/boards", Json("""{ "name": "Target", "level": "big-picture" }"""))).Content.ReadFromJsonAsync<JsonElement>();

        var response = await api.PostAsync(path.Replace("{board}", board.GetProperty("id").GetString(), StringComparison.Ordinal), Json(body));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("pointer").GetString().ShouldBe(pointer);
    }

    [Fact]
    public async Task A_retried_write_with_the_same_idempotency_key_is_replayed_not_repeated()
    {
        var (_, key) = await TeamWithKey("read", "write");
        var api = host.ApiKeyClient(key);

        async Task<HttpResponseMessage> Create(string name)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/boards") { Content = JsonContent.Create(new { name, level = "big-picture" }) };
            request.Headers.Add("Idempotency-Key", "create-ordering");
            return await api.SendAsync(request);
        }

        var first = await Create("Ordering");
        var second = await Create("Ordering");
        var different = await Create("Something else");

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
        (await second.Content.ReadAsStringAsync()).ShouldBe(await first.Content.ReadAsStringAsync());
        different.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var boards = await api.GetFromJsonAsync<JsonElement>("/api/v1/boards");
        boards.GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Scopes_are_enforced_and_revoked_keys_stop_at_once()
    {
        var (owner, readKey) = await TeamWithKey("read");
        var reader = host.ApiKeyClient(readKey);

        (await reader.GetAsync("/api/v1/boards")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var write = await reader.PostAsJsonAsync("/api/v1/boards", new { name = "Nope", level = "big-picture" });
        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await write.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().ShouldBe("insufficient-scope");

        var keys = await owner.Get($"/api/app/teams/{owner.Team}/api-keys");
        var keyId = keys[0].GetProperty("id").GetGuid();
        (await owner.Person.Client.DeleteAsync($"/api/app/teams/{owner.Team}/api-keys/{keyId}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var revoked = await reader.GetAsync("/api/v1/boards");
        revoked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await revoked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().ShouldBe("api-key-revoked");
    }

    [Fact]
    public async Task Elements_can_be_added_one_or_many_edited_with_a_version_check_and_deleted()
    {
        var (_, key) = await TeamWithKey("read", "write");
        var api = host.ApiKeyClient(key);
        var board = (await (await api.PostAsJsonAsync("/api/v1/boards", new { name = "Ordering", level = "process-modelling" })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var single = await api.PostAsJsonAsync($"/api/v1/boards/{board}/elements", new { type = "command", text = "Place Order" });
        single.StatusCode.ShouldBe(HttpStatusCode.Created);
        var command = await single.Content.ReadFromJsonAsync<JsonElement>();

        var bulk = await api.PostAsJsonAsync($"/api/v1/boards/{board}/elements/bulk", new
        {
            elements = new object[]
            {
                new { key = "placed", type = "domain-event", text = "Order Placed" },
                new { key = "policy", type = "policy", text = "Whenever an order is placed, notify the kitchen" },
            },
            connections = new object[]
            {
                new { from = command.GetProperty("id").GetGuid().ToString(), to = "placed" },
                new { from = "placed", to = "policy" },
            },
        });
        bulk.StatusCode.ShouldBe(HttpStatusCode.Created, await bulk.Content.ReadAsStringAsync());
        (await bulk.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("connections").GetArrayLength().ShouldBe(2);

        var elementId = command.GetProperty("id").GetGuid();
        var update = await api.PatchAsJsonAsync($"/api/v1/boards/{board}/elements/{elementId}", new { text = "Place an Order", expectedVersion = 1 });
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stale = await api.PatchAsJsonAsync($"/api/v1/boards/{board}/elements/{elementId}", new { text = "Again", expectedVersion = 1 });
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().ShouldBe("version-conflict");

        (await api.DeleteAsync($"/api/v1/boards/{board}/elements/{elementId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var connections = await api.GetFromJsonAsync<JsonElement>($"/api/v1/boards/{board}/connections");
        connections.GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task The_schema_and_the_problem_catalogue_are_public()
    {
        var anonymous = host.CreateClient();
        var schema = await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/schemas/board-document.json");
        var element = schema.GetProperty("properties").GetProperty("elements").GetProperty("items");
        element.GetProperty("properties").GetProperty("type").GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ShouldContain("domain-event");
        element.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ShouldBe(["type"]);

        // Coordinates are plain JSON numbers, and the version is exactly 1.
        var position = element.GetProperty("properties").GetProperty("position");
        position.GetProperty("properties").GetProperty("x").GetProperty("type").GetString().ShouldBe("number");
        schema.GetProperty("properties").GetProperty("version").GetProperty("const").GetInt32().ShouldBe(1);
        schema.TryGetProperty("required", out _).ShouldBeFalse();

        var problem = await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/problems/validation-failed");
        problem.GetProperty("status").GetInt32().ShouldBe(422);

        // The index points agents at the guide; the guide is really there.
        var index = await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/");
        var guide = await anonymous.GetAsync(index.GetProperty("llmGuide").GetString());
        guide.StatusCode.ShouldBe(HttpStatusCode.OK);
        guide.Content.Headers.ContentType!.MediaType.ShouldBe("text/markdown");
        (await guide.Content.ReadAsStringAsync()).ShouldContain("Board Document");
        (await anonymous.GetAsync("/docs/api.md")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Each_key_has_its_own_rate_limit()
    {
        await using var limited = new ApiHost(mongo.ConnectionString, publicApiPermitsPerMinute: 3);
        var owner = await limited.SignUp("Ana");
        var team = await owner.CreateTeam("Team");
        var api = limited.ApiKeyClient(await owner.CreateApiKey(team, "read"));

        for (var request = 0; request < 3; request++)
        {
            (await api.GetAsync("/api/v1/boards")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var limitedResponse = await api.GetAsync("/api/v1/boards");
        limitedResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        limitedResponse.Headers.RetryAfter.ShouldNotBeNull();
        (await limitedResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString().ShouldBe("/api/v1/problems/rate-limited");
    }

    private async Task<(TeamOwner Owner, string Key)> TeamWithKey(params string[] scopes)
    {
        var person = await host.SignUp("Ana");
        var team = await person.CreateTeam("Food delivery");
        return (new TeamOwner(person, team), await person.CreateApiKey(team, scopes));
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private sealed record TeamOwner(Person Person, Guid Team)
    {
        public Task<JsonElement> Get(string path) => Person.Get(path);
    }
}
