using System.Net;
using System.Text;
using System.Text.Json;
using EventStorming.Host.IntegrationTests.Support;
using Microsoft.AspNetCore.SignalR.Client;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace EventStorming.Host.IntegrationTests;

/// <summary>An agent draws boards over MCP, through the official MCP client, and people see the result live.</summary>
public sealed class McpServerTests(MongoServer mongo) : IAsyncLifetime
{
    private const string FoodOrdering = """
        {
          "name": "Online food ordering",
          "level": "big-picture",
          "elements": [
            { "key": "customer", "type": "swimlane", "text": "Customer" },
            { "key": "placed", "type": "domain-event", "text": "Order Placed", "swimlane": "customer", "pivotal": true },
            { "key": "late", "type": "hot-spot", "text": "What if the kitchen is busy?", "anchor": "placed" },
            { "key": "paid", "type": "domain-event", "text": "Payment Taken", "swimlane": "customer" }
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
    public async Task Agents_find_the_drawing_tools_described_for_a_model()
    {
        await using var agent = await host.Agent(await Key("read", "write"));

        var tools = (await agent.ListToolsAsync()).ToDictionary(tool => tool.Name, tool => tool.ProtocolTool);
        tools.Keys.ShouldBe(
            ["add_to_board", "connect_elements", "create_board", "delete_connections", "delete_elements", "get_board", "list_boards", "list_element_types", "move_elements", "replace_board_content", "update_element"],
            ignoreOrder: true);
        tools["get_board"].Annotations!.ReadOnlyHint.ShouldBe(true);
        tools["create_board"].Annotations!.DestructiveHint.ShouldBe(false);
        tools["delete_elements"].Annotations!.DestructiveHint.ShouldBe(true);

        var schema = tools["create_board"].InputSchema;
        schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ShouldBe(["name", "level"], ignoreOrder: true);
        schema.GetProperty("properties").GetProperty("level").GetProperty("enum").GetArrayLength().ShouldBe(3);
        var element = schema.GetProperty("properties").GetProperty("elements").GetProperty("items");
        element.GetProperty("required").EnumerateArray().Select(value => value.GetString()).ShouldContain("type");
        element.GetProperty("properties").GetProperty("position").ToString().ShouldContain("\"number\"");

        agent.ServerInstructions!.ShouldContain("list_element_types");
    }

    [Fact]
    public async Task An_agent_draws_a_whole_board_in_one_call_laid_out_on_the_timeline()
    {
        await using var agent = await host.Agent(await Key("read", "write"));

        var drawn = await Call(agent, "create_board", FoodOrdering);
        var board = drawn.GetProperty("board");
        board.GetProperty("url").GetString()!.ShouldStartWith($"{ApiHost.WebOrigin}/boards/");
        drawn.GetProperty("elementCount").GetInt32().ShouldBe(4);
        var keys = drawn.GetProperty("keys");

        var read = await Call(agent, "get_board", $$"""{ "boardId": "{{board.GetProperty("id").GetGuid()}}" }""");
        var elements = read.GetProperty("elements").EnumerateArray().ToDictionary(item => item.GetProperty("id").GetGuid());
        var placed = elements[keys.GetProperty("placed").GetGuid()];
        var late = elements[keys.GetProperty("late").GetGuid()];
        var paid = elements[keys.GetProperty("paid").GetGuid()];

        placed.GetProperty("swimlane").GetGuid().ShouldBe(keys.GetProperty("customer").GetGuid());
        placed.GetProperty("pivotal").GetBoolean().ShouldBeTrue();
        late.GetProperty("x").GetDouble().ShouldBe(placed.GetProperty("x").GetDouble());
        late.GetProperty("y").GetDouble().ShouldBeGreaterThan(placed.GetProperty("y").GetDouble());
        paid.GetProperty("x").GetDouble().ShouldBeGreaterThan(placed.GetProperty("x").GetDouble());
        read.GetProperty("connections").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task What_an_agent_adds_appears_live_on_a_board_people_have_open()
    {
        var ana = await host.SignUp("Ana");
        var team = await ana.CreateTeam("Food delivery");
        var board = await ana.CreateBoard(team, "Ordering");
        await using var hub = host.Hub(ana);
        var inbox = new Inbox().Listen(hub, "boardChanged");
        await hub.StartAsync();
        (await hub.InvokeAsync<JsonElement>("JoinBoard", board)).GetProperty("ok").GetBoolean().ShouldBeTrue();

        await using var agent = await host.Agent(await ana.CreateApiKey(team, "read", "write"));
        var added = await Call(agent, "add_to_board", $$"""
            {
              "boardId": "{{board}}",
              "elements": [
                { "key": "placed", "type": "domain-event", "text": "Order Placed" },
                { "key": "paid", "type": "domain-event", "text": "Payment Taken" }
              ],
              "connections": [ { "from": "placed", "to": "paid" } ]
            }
            """);
        added.GetProperty("elements").GetArrayLength().ShouldBe(2);

        var seen = await inbox.WaitFor("boardChanged");
        seen.GetProperty("actor").GetProperty("kind").GetString().ShouldBe("api-key");
        seen.GetProperty("elements").EnumerateArray().Select(item => item.GetProperty("text").GetString()).ShouldBe(["Order Placed", "Payment Taken"], ignoreOrder: true);
        seen.GetProperty("connections").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Adding_to_a_board_continues_inside_its_swimlanes_which_grow_to_hold_the_new_stickies()
    {
        await using var agent = await host.Agent(await Key("read", "write"));
        var drawn = await Call(agent, "create_board", FoodOrdering);
        var boardId = drawn.GetProperty("board").GetProperty("id").GetGuid();
        var laneId = drawn.GetProperty("keys").GetProperty("customer").GetGuid();
        var elements = string.Join(",", Enumerable.Range(1, 12).Select(index => $$"""{ "type": "domain-event", "text": "Step {{index}} Done", "swimlane": "{{laneId}}" }"""));

        var added = await agent.CallToolAsync("add_to_board", Arguments($$"""{ "boardId": "{{boardId}}", "elements": [ {{elements}} ] }"""));

        added.IsError.ShouldNotBe(true, Text(added));
        Text(added).ShouldStartWith("Added 12 elements and 0 arrows. 1 swimlane or boundary grew to hold them.");
        var content = added.StructuredContent!.Value;
        content.GetProperty("elements").GetArrayLength().ShouldBe(12);
        var lane = content.GetProperty("resized").EnumerateArray().ShouldHaveSingleItem();
        lane.GetProperty("id").GetGuid().ShouldBe(laneId);
        var laneRight = lane.GetProperty("x").GetDouble() + lane.GetProperty("width").GetDouble();
        content.GetProperty("elements").EnumerateArray()
            .ShouldAllBe(item => item.GetProperty("x").GetDouble() + item.GetProperty("width").GetDouble() <= laneRight);
    }

    [Fact]
    public async Task A_refusal_lists_every_problem_with_its_fix_so_the_agent_can_correct_itself()
    {
        await using var agent = await host.Agent(await Key("read", "write"));

        var refused = await agent.CallToolAsync("create_board", Arguments("""
            {
              "name": "Checkout",
              "level": "big-picture",
              "elements": [
                { "key": "placed", "type": "domain-event", "text": "Order Placed" },
                { "key": "q", "type": "hotspot", "text": "Too slow?", "anchor": "plced" }
              ]
            }
            """));

        refused.IsError.ShouldBe(true);
        var text = Text(refused);
        text.ShouldContain("Nothing was changed");
        text.ShouldContain("elements[1].type");
        text.ShouldContain("hot-spot");
        text.ShouldContain("Did you mean 'placed'?");
        (await Call(agent, "list_boards", "{}")).GetProperty("boards").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task An_agent_edits_moves_connects_and_deletes_what_it_drew()
    {
        await using var agent = await host.Agent(await Key("read", "write"));
        var drawn = await Call(agent, "create_board", """
            { "name": "Edits", "level": "process-modelling",
              "elements": [ { "key": "a", "type": "domain-event", "text": "Order Placd" }, { "key": "b", "type": "command", "text": "Pay" } ] }
            """);
        var boardId = drawn.GetProperty("board").GetProperty("id").GetGuid();
        var a = drawn.GetProperty("keys").GetProperty("a").GetGuid();
        var b = drawn.GetProperty("keys").GetProperty("b").GetGuid();

        (await Call(agent, "update_element", $$"""{ "boardId": "{{boardId}}", "elementId": "{{a}}", "text": "Order Placed" }"""))
            .GetProperty("elements")[0].GetProperty("text").GetString().ShouldBe("Order Placed");
        (await Call(agent, "move_elements", $$"""{ "boardId": "{{boardId}}", "moves": [ { "elementId": "{{b}}", "x": 900, "y": 300 } ] }"""))
            .GetProperty("elements")[0].GetProperty("x").GetDouble().ShouldBe(900);
        var arrow = await Call(agent, "connect_elements", $$"""{ "boardId": "{{boardId}}", "from": "{{b}}", "to": "{{a}}", "label": "then" }""");

        var stale = await agent.CallToolAsync("update_element", Arguments($$"""{ "boardId": "{{boardId}}", "elementId": "{{a}}", "text": "Late edit", "expectedVersion": 1 }"""));
        stale.IsError.ShouldBe(true);
        Text(stale).ShouldContain("version-conflict");

        await Call(agent, "delete_connections", $$"""{ "boardId": "{{boardId}}", "connectionIds": [ "{{arrow.GetProperty("id").GetGuid()}}" ] }""");
        await Call(agent, "delete_elements", $$"""{ "boardId": "{{boardId}}", "elementIds": [ "{{b}}" ] }""");

        var read = await Call(agent, "get_board", $$"""{ "boardId": "{{boardId}}" }""");
        read.GetProperty("elements").EnumerateArray().Select(item => item.GetProperty("text").GetString()).ShouldBe(["Order Placed"]);
        read.GetProperty("connections").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task A_read_only_key_can_read_but_not_draw()
    {
        await using var agent = await host.Agent(await Key("read"));

        (await Call(agent, "list_element_types", "{}")).GetProperty("types").GetArrayLength().ShouldBeGreaterThan(5);
        var refused = await agent.CallToolAsync("create_board", Arguments("""{ "name": "Nope", "level": "big-picture" }"""));
        refused.IsError.ShouldBe(true);
        Text(refused).ShouldContain("[forbidden]");
    }

    [Fact]
    public async Task The_endpoint_refuses_callers_without_a_key()
    {
        var response = await host.CreateClient().PostAsync("/mcp", new StringContent(
            """{ "jsonrpc": "2.0", "id": 1, "method": "tools/list", "params": {} }""", Encoding.UTF8, "application/json"));
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_guide_the_boards_and_the_prompts_are_there_for_agents()
    {
        await using var agent = await host.Agent(await Key("read", "write"));

        var guide = await agent.ReadResourceAsync("eventstorming://guide");
        ((TextResourceContents)guide.Contents[0]).Text.ShouldContain("Board Document");

        var boardId = (await Call(agent, "create_board", FoodOrdering)).GetProperty("board").GetProperty("id").GetGuid();
        var board = await agent.ReadResourceAsync($"eventstorming://boards/{boardId}");
        JsonDocument.Parse(((TextResourceContents)board.Contents[0]).Text).RootElement.GetProperty("elements").GetArrayLength().ShouldBe(4);

        (await agent.ListPromptsAsync()).Select(prompt => prompt.Name).ShouldBe(["big_picture", "process_modelling"], ignoreOrder: true);
        var prompt = await agent.GetPromptAsync("big_picture", new Dictionary<string, object?> { ["domain"] = "online food ordering" });
        var instructions = ((TextContentBlock)prompt.Messages[0].Content).Text;
        instructions.ShouldContain("online food ordering");
        instructions.ShouldContain("create_board");
    }

    private async Task<string> Key(params string[] scopes)
    {
        var owner = await host.SignUp("Ana");
        return await owner.CreateApiKey(await owner.CreateTeam("Food delivery"), scopes);
    }

    /// <summary>Calls a tool that should succeed and returns its structured result.</summary>
    private static async Task<JsonElement> Call(McpClient agent, string tool, string arguments)
    {
        var result = await agent.CallToolAsync(tool, Arguments(arguments));
        result.IsError.ShouldNotBe(true, Text(result));
        return result.StructuredContent!.Value;
    }

    private static Dictionary<string, object?> Arguments(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value.Clone());

    private static string Text(CallToolResult result) => string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
