using System.Globalization;
using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Slices.AddConnection;
using EventStorming.BoardModelling.Slices.AddElements;
using EventStorming.BoardModelling.Slices.DeleteConnections;
using EventStorming.BoardModelling.Slices.DeleteElements;
using EventStorming.BoardModelling.Slices.GetElement;
using EventStorming.BoardModelling.Slices.ListConnections;
using EventStorming.BoardModelling.Slices.ListElements;
using EventStorming.BoardModelling.Slices.MoveElements;
using EventStorming.BoardModelling.Slices.UpdateElement;
using EventStorming.SharedKernel;
using EventStorming.Specs.Support;
using Reqnroll;
using Shouldly;

namespace EventStorming.Specs.StepDefinitions;

[Binding]
public sealed class ElementSteps(World world)
{
    [Given("the board {string} has these elements:")]
    public void GivenTheBoardHasTheseElements(string board, DataTable table)
    {
        var boardId = world.Board(board).Id;
        var by = new ActorRef(ActorKind.Account, Guid.Empty, "Seed");
        foreach (var row in table.Rows)
        {
            var type = world.Registry.Find(row["type"]).ShouldNotBeNull();
            var id = Guid.CreateVersion7();
            world.Keys[row["key"]] = id;
            world.Boards.Elements.Add(new Element(
                id, boardId, type.Id, row["text"],
                Number(row, "x", 0), Number(row, "y", 0), type.DefaultSize.Width, type.DefaultSize.Height,
                false, null, 1, world.Clock.UtcNow, by, world.Clock.UtcNow, by));
        }

        var index = world.Boards.Boards.FindIndex(candidate => candidate.Id == boardId);
        world.Boards.Boards[index] = world.Boards.Boards[index] with { ElementCount = world.Boards.Elements.Count(element => element.BoardId == boardId) };
    }

    [Given("{string} is connected to {string}")]
    public void GivenIsConnectedTo(string from, string to)
    {
        var element = world.ElementNamed(from);
        world.Boards.Connections.Add(new Connection(Guid.CreateVersion7(), element.BoardId, element.Id, world.Element(to), null, 1, world.Clock.UtcNow, element.CreatedBy));
    }

    [When("{word} adds a(n) {string} saying {string} to {string}")]
    public async Task WhenAddsAnElement(string person, string type, string text, string board) =>
        await Add(person, board, [new NewElement(type, text, Key: "added")]);

    [When("{word} adds a(n) {string} saying {string} at {int},{int} to {string}")]
    public async Task WhenAddsAnElementAt(string person, string type, string text, int x, int y, string board) =>
        await Add(person, board, [new NewElement(type, text, Key: "added", Position: new Position(x, y))]);

    [When("{word} adds these elements to {string}:")]
    public async Task WhenAddsTheseElements(string person, string board, DataTable table) =>
        await Add(person, board, table.Rows.Select(row => new NewElement(
            Value(row, "type"),
            Value(row, "text"),
            Key: Value(row, "key"),
            Position: Value(row, "x") is null ? null : new Position(Number(row, "x", 0), Number(row, "y", 0)),
            Swimlane: Reference(Value(row, "swimlane")),
            Anchor: Reference(Value(row, "anchor")),
            Pivotal: Value(row, "pivotal") == "yes")).ToList());

    [When("{word} adds these elements with connections to {string}:")]
    public async Task WhenAddsTheseElementsWithConnections(string person, string board, DataTable table) =>
        await Add(person, board,
            table.Rows.Where(row => row["kind"] == "element").Select(row => new NewElement(row["type"], row["text"], Key: row["key"])).ToList(),
            table.Rows.Where(row => row["kind"] == "connection").Select(row => new NewConnection(Reference(row["from"]), Reference(row["to"]))).ToList());

    private static readonly Guid FirstClientId = Guid.Parse("0a4f1e6b-2c3d-4e5f-8a9b-0c1d2e3f4a5b");
    private static readonly Guid SecondClientId = Guid.Parse("1b5a2f7c-3d4e-4f60-9bac-1d2e3f4a5b6c");

    [When("{word} adds two elements with client ids and an arrow between those ids to {string}")]
    public async Task WhenAddsElementsJoinedByClientIds(string person, string board) =>
        await Add(person, board,
            [
                new NewElement("domain-event", "Order Placed", FirstClientId, Position: new Position(0, 0)),
                new NewElement("domain-event", "Payment Taken", SecondClientId, Position: new Position(240, 0)),
            ],
            [new NewConnection(FirstClientId.ToString(), SecondClientId.ToString())]);

    [Then("the arrow joins the two client ids")]
    public void ThenTheArrowJoinsTheClientIds() =>
        world.Boards.Connections.ShouldContain(connection => connection.From == FirstClientId && connection.To == SecondClientId);

    [When("{word} adds the same element with id {string} twice to {string}")]
    public async Task WhenAddsTheSameElementTwice(string person, string id, string board)
    {
        var element = new NewElement("domain-event", "Order Placed", Guid.Parse(id), Position: new Position(0, 0));
        await Add(person, board, [element]);
        await Add(person, board, [element]);
    }

    [When("{word} adds {int} elements to {string}")]
    public async Task WhenAddsManyElements(string person, int count, string board) =>
        await Add(person, board, Enumerable.Range(0, count).Select(index => new NewElement("domain-event", $"Event {index}", Position: new Position(index * 10, 0))).ToList());

    [Then("the board {string} has {int} element(s)")]
    public void ThenTheBoardHasElements(string board, int count)
    {
        var boardId = world.Board(board).Id;
        world.Boards.Elements.Count(element => element.BoardId == boardId).ShouldBe(count);
        world.Board(board).ElementCount.ShouldBe(count);
    }

    [Then("the element {string} says {string}")]
    public void ThenTheElementSays(string key, string text) => world.ElementNamed(key).Text.ShouldBe(text);

    [Then("the element {string} is a(n) {string}")]
    public void ThenTheElementIsA(string key, string type) => world.ElementNamed(key).Type.ShouldBe(type);

    [Then("the element {string} is at {int},{int}")]
    public void ThenTheElementIsAt(string key, int x, int y)
    {
        var element = world.ElementNamed(key);
        (element.X, element.Y).ShouldBe((x, y));
    }

    [Then("the element {string} is at version {int}")]
    public void ThenTheElementIsAtVersion(string key, int version) => world.ElementNamed(key).Version.ShouldBe(version);

    [Then("the element {string} is pivotal")]
    public void ThenTheElementIsPivotal(string key) => world.ElementNamed(key).Pivotal.ShouldBeTrue();

    [Then("the element {string} is not pivotal")]
    public void ThenTheElementIsNotPivotal(string key) => world.ElementNamed(key).Pivotal.ShouldBeFalse();

    [Then("the element {string} is {int} wide and {int} high")]
    public void ThenTheElementIsSized(string key, int width, int height)
    {
        var element = world.ElementNamed(key);
        (element.Width, element.Height).ShouldBe((width, height));
    }

    [Then("the element {string} is placed to the right of {string}")]
    public void ThenIsToTheRightOf(string key, string other) =>
        world.ElementNamed(key).X.ShouldBeGreaterThanOrEqualTo(world.ElementNamed(other).X + world.ElementNamed(other).Width);

    [Then("the element {string} is placed below {string}")]
    public void ThenIsBelow(string key, string other)
    {
        var element = world.ElementNamed(key);
        var anchor = world.ElementNamed(other);
        element.X.ShouldBe(anchor.X);
        element.Y.ShouldBeGreaterThanOrEqualTo(anchor.Y + anchor.Height);
    }

    [When("{word} changes the text of {string} to {string}")]
    [Given("{word} changes the text of {string} to {string}")]
    public async Task WhenChangesTheText(string person, string key, string text) =>
        await Update(person, key, text: text);

    [When("{word} changes the text of {string} to {string} expecting version {int}")]
    public async Task WhenChangesTheTextExpecting(string person, string key, string text, int version) =>
        await Update(person, key, text: text, expectedVersion: version);

    [When("{word} changes the type of {string} to {string}")]
    public async Task WhenChangesTheType(string person, string key, string type) =>
        await Update(person, key, type: type);

    [When("{word} marks {string} as pivotal")]
    [Given("{word} marks {string} as pivotal")]
    public async Task WhenMarksAsPivotal(string person, string key) =>
        await Update(person, key, pivotal: true);

    [When("{word} resizes {string} to {int} by {int}")]
    public async Task WhenResizes(string person, string key, int width, int height) =>
        await Update(person, key, size: new Size(width, height));

    [When("{word} sends an update for {string} that changes nothing")]
    public async Task WhenSendsAnEmptyUpdate(string person, string key) => await Update(person, key);

    [When("{word} moves {string} to {int},{int}")]
    public async Task WhenMoves(string person, string key, int x, int y) =>
        world.LastResult = await world.MoveElements.Handle(
            new MoveElementsCommand(world.Person(person), world.ElementNamed(key).BoardId, [new ElementMove(world.Element(key), new Position(x, y))], "op-1"),
            CancellationToken.None);

    [When("{word} moves these elements on {string}:")]
    public async Task WhenMovesTheseElements(string person, string board, DataTable table) =>
        world.LastResult = await world.MoveElements.Handle(
            new MoveElementsCommand(
                world.Person(person),
                world.Board(board).Id,
                table.Rows.Select(row => new ElementMove(
                    world.Keys.TryGetValue(row["key"], out var id) ? id : Guid.NewGuid(),
                    new Position(Number(row, "x", 0), Number(row, "y", 0)))).ToList()),
            CancellationToken.None);

    [When("{word} deletes {string}")]
    [Given("{word} deletes {string}")]
    public async Task WhenDeletes(string person, string key) =>
        world.LastResult = await world.DeleteElements.Handle(
            new DeleteElementsCommand(world.Person(person), world.ElementNamed(key).BoardId, [world.Element(key)]), CancellationToken.None);

    [When("{word} deletes {string} again")]
    public async Task WhenDeletesAgain(string person, string key) =>
        world.LastResult = await world.DeleteElements.Handle(
            new DeleteElementsCommand(world.Person(person), world.Boards.Boards[0].Id, [world.Element(key)]), CancellationToken.None);

    [Then("{string} no longer exists")]
    public void ThenNoLongerExists(string key) => world.Boards.Elements.ShouldNotContain(element => element.Id == world.Element(key));

    [When("{word} connects {string} to {string}")]
    public async Task WhenConnects(string person, string from, string to) =>
        world.LastResult = await world.AddConnection.Handle(
            new AddConnectionCommand(world.Person(person), world.ElementNamed(from).BoardId, world.Element(from), world.Element(to)), CancellationToken.None);

    [Then("{string} is connected to {string}")]
    public void ThenIsConnected(string from, string to) =>
        world.Boards.Connections.ShouldContain(connection => connection.From == world.Element(from) && connection.To == world.Element(to));

    [Then("{string} has no connections")]
    public void ThenHasNoConnections(string key) =>
        world.Boards.Connections.ShouldNotContain(connection => connection.From == world.Element(key) || connection.To == world.Element(key));

    [When("{word} deletes the connection from {string} to {string}")]
    public async Task WhenDeletesTheConnection(string person, string from, string to)
    {
        var connection = world.Boards.Connections.Single(candidate => candidate.From == world.Element(from) && candidate.To == world.Element(to));
        world.LastResult = await world.DeleteConnections.Handle(
            new DeleteConnectionsCommand(world.Person(person), connection.BoardId, [connection.Id]), CancellationToken.None);
    }

    [When("{word} lists the elements of {string} {int} at a time")]
    public async Task WhenListsTheElementsPaged(string person, string board, int limit)
    {
        var total = 0;
        var pages = 0;
        string? cursor = null;
        do
        {
            var result = await world.ListElements.Handle(new ListElementsQuery(world.Person(person), world.Board(board).Id, null, cursor, limit), CancellationToken.None);
            world.LastResult = result;
            result.Success.ShouldBeTrue();
            total += result.Page!.Items.Count;
            pages++;
            cursor = result.Page.NextCursor;
        }
        while (cursor is not null);

        PagedTotals = (pages, total);
    }

    [When("{word} lists the {string} elements of {string}")]
    public async Task WhenListsElementsOfType(string person, string type, string board) =>
        world.LastResult = await world.ListElements.Handle(new ListElementsQuery(world.Person(person), world.Board(board).Id, type), CancellationToken.None);

    [Then("{int} pages hold {int} elements in total")]
    public void ThenPagesHold(int pages, int total) => PagedTotals.ShouldBe((pages, total));

    [Then("{int} element(s) is/are listed")]
    public void ThenElementsAreListed(int count) => world.Last<ListElementsResult>().Page!.Items.Count.ShouldBe(count);

    [When("{word} looks up the element {string}")]
    public async Task WhenLooksUpTheElement(string person, string key) =>
        world.LastResult = await world.GetElement.Handle(new GetElementQuery(world.Person(person), world.ElementNamed(key).BoardId, world.Element(key)), CancellationToken.None);

    [When("{word} lists the connections of {string}")]
    public async Task WhenListsTheConnections(string person, string board) =>
        world.LastResult = await world.ListConnections.Handle(new ListConnectionsQuery(world.Person(person), world.Board(board).Id), CancellationToken.None);

    [Then("{int} connection(s) is/are listed")]
    public void ThenConnectionsAreListed(int count) => world.Last<ListConnectionsResult>().Page!.Items.Count.ShouldBe(count);

    [Then("the change is announced to everyone on the board with operation {string}")]
    public void ThenTheChangeIsAnnouncedWithOperation(string operationId) =>
        world.Broadcaster.Changes.ShouldHaveSingleItem().OperationId.ShouldBe(operationId);

    [Then("the change is announced to everyone on the board")]
    public void ThenTheChangeIsAnnounced() => world.Broadcaster.Changes.ShouldNotBeEmpty();

    [Then("the announcement carries the element {string} at version {int}")]
    public void ThenTheAnnouncementCarries(string key, int version) =>
        world.Broadcaster.Changes.Last().Elements.ShouldContain(element => element.Id == world.Element(key) && element.Version == version);

    [Then("the announcement removes {string} and {int} connection(s)")]
    public void ThenTheAnnouncementRemoves(string key, int connections)
    {
        var change = world.Broadcaster.Changes.Last();
        change.RemovedElements.ShouldContain(removed => removed.Id == world.Element(key));
        change.RemovedConnections.Count.ShouldBe(connections);
    }

    [Then("nothing is announced")]
    public void ThenNothingIsAnnounced() => world.Broadcaster.Changes.ShouldBeEmpty();

    [Then("the board {string} is at revision {int}")]
    public void ThenTheBoardIsAtRevision(string board, int revision) => world.Board(board).Revision.ShouldBe(revision);

    private (int Pages, int Total) PagedTotals { get; set; }

    private async Task Add(string person, string board, IReadOnlyList<NewElement> elements, IReadOnlyList<NewConnection>? connections = null)
    {
        var result = await world.AddElements.Handle(new AddElementsCommand(world.Person(person), world.Board(board).Id, elements, connections, "op-1"), CancellationToken.None);
        world.LastResult = result;
        if (result.Success)
        {
            foreach (var (key, id) in result.Added!.KeyedIds)
            {
                world.Keys[key] = id;
            }
        }
    }

    private async Task Update(string person, string key, string? text = null, string? type = null, bool? pivotal = null, Size? size = null, long? expectedVersion = null) =>
        world.LastResult = await world.UpdateElement.Handle(
            new UpdateElementCommand(world.Person(person), world.ElementNamed(key).BoardId, world.Element(key), text, type, null, size, pivotal, null, expectedVersion, "op-1"),
            CancellationToken.None);

    /// <summary>A reference to a key already stored in the scenario becomes that element's id; any other value stays a request key.</summary>
    private string? Reference(string? value) =>
        value is null ? null : world.Keys.TryGetValue(value, out var id) && world.Boards.Elements.Any(element => element.Id == id) ? id.ToString() : value;

    private static string? Value(DataTableRow row, string column) =>
        row.TryGetValue(column, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    private static double Number(DataTableRow row, string column, double fallback) =>
        Value(row, column) is { } value ? double.Parse(value, CultureInfo.InvariantCulture) : fallback;
}
