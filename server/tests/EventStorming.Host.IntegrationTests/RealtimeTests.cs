using System.Net.Http.Json;
using System.Text.Json;
using EventStorming.Host.IntegrationTests.Support;
using Microsoft.AspNetCore.SignalR.Client;
using Shouldly;
using Xunit;

namespace EventStorming.Host.IntegrationTests;

/// <summary>Two people on one board, over the real hub: each sees the other's changes, cursor and presence - and changes made through the public API.</summary>
public sealed class RealtimeTests(MongoServer mongo) : IAsyncLifetime
{
    private ApiHost host = null!;

    public ValueTask InitializeAsync()
    {
        host = new ApiHost(mongo.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await host.DisposeAsync();

    [Fact]
    public async Task Two_people_see_each_others_edits_presence_and_cursors()
    {
        var (ana, bo, board) = await TwoPeopleOnOneBoard();
        await using var anaHub = host.Hub(ana);
        await using var boHub = host.Hub(bo);
        var anaInbox = new Inbox().Listen(anaHub, "boardChanged", "participantJoined", "cursorMoved");
        var boInbox = new Inbox().Listen(boHub, "boardChanged", "participantJoined", "cursorMoved", "participantLeft");
        await anaHub.StartAsync();
        await boHub.StartAsync();

        (await anaHub.InvokeAsync<JsonElement>("JoinBoard", board)).GetProperty("ok").GetBoolean().ShouldBeTrue();
        var boJoined = await boHub.InvokeAsync<JsonElement>("JoinBoard", board);
        boJoined.GetProperty("participants").GetArrayLength().ShouldBe(2);
        (await anaInbox.WaitFor("participantJoined")).GetProperty("participant").GetProperty("displayName").GetString().ShouldBe("Bo");

        var elementId = Guid.NewGuid();
        var added = await anaHub.InvokeAsync<JsonElement>("AddElements", new
        {
            boardId = board,
            operationId = "op-ana-1",
            elements = new[] { new { id = elementId, type = "domain-event", text = "Order Placed", x = 100.0, y = 40.0 } },
        });
        added.GetProperty("ok").GetBoolean().ShouldBeTrue(added.ToString());

        var seenByBo = await boInbox.WaitFor("boardChanged");
        seenByBo.GetProperty("operationId").GetString().ShouldBe("op-ana-1");
        seenByBo.GetProperty("elements")[0].GetProperty("text").GetString().ShouldBe("Order Placed");
        (await anaInbox.WaitFor("boardChanged")).GetProperty("elements")[0].GetProperty("version").GetInt64().ShouldBe(1);

        var moved = await boHub.InvokeAsync<JsonElement>("MoveElements", new
        {
            boardId = board,
            operationId = "op-bo-1",
            moves = new[] { new { elementId, x = 300.0, y = 200.0 } },
        });
        moved.GetProperty("ok").GetBoolean().ShouldBeTrue(moved.ToString());
        var seenByAna = await anaInbox.WaitFor("boardChanged", message => message.GetProperty("operationId").GetString() == "op-bo-1");
        seenByAna.GetProperty("elements")[0].GetProperty("x").GetDouble().ShouldBe(300);
        seenByAna.GetProperty("elements")[0].GetProperty("version").GetInt64().ShouldBe(2);

        await anaHub.InvokeAsync("MoveCursor", board, 12.0, 34.0);
        var cursor = await boInbox.WaitFor("cursorMoved");
        cursor.GetProperty("x").GetDouble().ShouldBe(12);
        anaInbox.Got("cursorMoved").ShouldBeFalse("a cursor is not echoed to its owner");

        await anaHub.StopAsync();
        await boInbox.WaitFor("participantLeft");
    }

    [Fact]
    public async Task A_change_made_through_the_public_api_appears_on_open_boards()
    {
        var (ana, _, board) = await TwoPeopleOnOneBoard();
        var key = await ana.CreateApiKey(Team, "read", "write");
        await using var hub = host.Hub(ana);
        var inbox = new Inbox().Listen(hub, "boardChanged");
        await hub.StartAsync();
        await hub.InvokeAsync<JsonElement>("JoinBoard", board);

        var response = await host.ApiKeyClient(key).PostAsJsonAsync($"/api/v1/boards/{board}/elements", new { type = "hot-spot", text = "Who owns refunds?" });
        response.EnsureSuccessStatusCode();

        var change = await inbox.WaitFor("boardChanged");
        change.GetProperty("actor").GetProperty("kind").GetString().ShouldBe("api-key");
        change.GetProperty("elements")[0].GetProperty("type").GetString().ShouldBe("hot-spot");
    }

    [Fact]
    public async Task A_viewer_can_watch_but_every_edit_is_refused_with_a_reason()
    {
        var (_, _, board) = await TwoPeopleOnOneBoard();
        var cy = await host.SignUp("Cy");
        await Invite(cy, "viewer");
        await using var hub = host.Hub(cy);
        await hub.StartAsync();

        (await hub.InvokeAsync<JsonElement>("JoinBoard", board)).GetProperty("ok").GetBoolean().ShouldBeTrue();
        var refused = await hub.InvokeAsync<JsonElement>("AddElements", new
        {
            boardId = board,
            operationId = "op-cy",
            elements = new[] { new { type = "domain-event", text = "Mine" } },
        });
        refused.GetProperty("ok").GetBoolean().ShouldBeFalse();
        refused.GetProperty("failures")[0].GetProperty("code").GetString().ShouldBe("forbidden");
    }

    [Fact]
    public async Task Outsiders_cannot_join()
    {
        var (_, _, board) = await TwoPeopleOnOneBoard();
        var stranger = await host.SignUp("Stranger");
        await using var hub = host.Hub(stranger);
        await hub.StartAsync();

        var joined = await hub.InvokeAsync<JsonElement>("JoinBoard", board);
        joined.GetProperty("ok").GetBoolean().ShouldBeFalse();
        joined.GetProperty("failures")[0].GetProperty("code").GetString().ShouldBe("not-found");
    }

    private Guid Team { get; set; }

    private Person Owner { get; set; } = null!;

    private async Task<(Person Ana, Person Bo, Guid Board)> TwoPeopleOnOneBoard()
    {
        Owner = await host.SignUp("Ana");
        Team = await Owner.CreateTeam("Food delivery");
        var board = await Owner.CreateBoard(Team, "Ordering");
        var bo = await host.SignUp("Bo");
        await Invite(bo, "editor");
        return (Owner, bo, board);
    }

    private async Task Invite(Person person, string role)
    {
        var token = (await Owner.Post($"/api/app/teams/{Team}/invitations", new { role })).GetProperty("token").GetString()!;
        await person.Post($"/api/app/invitations/{token}/accept", new { }, System.Net.HttpStatusCode.OK);
    }
}
