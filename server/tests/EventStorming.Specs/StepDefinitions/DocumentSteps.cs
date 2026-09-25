using System.Text.Json;
using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Slices.ExportBoardDocument;
using EventStorming.BoardModelling.Slices.ImportBoardDocument;
using EventStorming.Specs.Support;
using Reqnroll;
using Shouldly;

namespace EventStorming.Specs.StepDefinitions;

/// <summary>Import, export and the auto-layout that places whatever a document leaves unpositioned.</summary>
[Binding]
public sealed class DocumentSteps(World world)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private ExportedDocument? exported;

    [When("{word} imports this document into {string}:")]
    public async Task WhenImportsANewBoard(string person, string team, string document) =>
        await Import(new ImportBoardDocumentCommand(world.Person(person), Parse(document), TeamId: world.Team(team).Id));

    [When("{word} replaces the content of {string} with:")]
    [Given("{word} has replaced the content of {string} with:")]
    public async Task WhenReplacesTheContent(string person, string board, string document) =>
        await Import(new ImportBoardDocumentCommand(world.Person(person), Parse(document), BoardId: world.Board(board).Id));

    [Then("the board {string} reads left to right:")]
    public void ThenTheBoardReadsLeftToRight(string board, DataTable table)
    {
        var boardId = world.Board(board).Id;
        var order = world.Boards.Elements
            .Where(element => element.BoardId == boardId && element.Type == "domain-event")
            .OrderBy(element => element.X)
            .Select(element => element.Text)
            .ToList();
        order.ShouldBe(table.Rows.Select(row => row["event"]));
    }

    [Then("{string} is left of {string}")]
    public void ThenIsLeftOf(string key, string other) =>
        (world.ElementNamed(key).X + world.ElementNamed(key).Width).ShouldBeLessThanOrEqualTo(world.ElementNamed(other).X);

    [Then("{string} is in the same column as {string}, below it")]
    public void ThenIsBelowInColumn(string key, string other)
    {
        var element = world.ElementNamed(key);
        var anchor = world.ElementNamed(other);
        (element.X + (element.Width / 2)).ShouldBe(anchor.X + (anchor.Width / 2), 1);
        element.Y.ShouldBeGreaterThanOrEqualTo(anchor.Y + anchor.Height);
    }

    [Then("{string} is inside {string}")]
    public void ThenIsInside(string key, string area)
    {
        var element = world.ElementNamed(key);
        var container = world.ElementNamed(area);
        element.X.ShouldBeGreaterThanOrEqualTo(container.X);
        element.Y.ShouldBeGreaterThanOrEqualTo(container.Y);
        (element.X + element.Width).ShouldBeLessThanOrEqualTo(container.X + container.Width);
        (element.Y + element.Height).ShouldBeLessThanOrEqualTo(container.Y + container.Height);
    }

    [Then("{string} is not inside {string}")]
    public void ThenIsNotInside(string key, string area)
    {
        var element = world.ElementNamed(key);
        var container = world.ElementNamed(area);
        var inside = element.X >= container.X && element.Y >= container.Y
                     && element.X + element.Width <= container.X + container.Width
                     && element.Y + element.Height <= container.Y + container.Height;
        inside.ShouldBeFalse();
    }

    [Then("{string} is above {string}")]
    public void ThenIsAbove(string key, string other) =>
        (world.ElementNamed(key).Y + world.ElementNamed(key).Height).ShouldBeLessThanOrEqualTo(world.ElementNamed(other).Y);

    [Then("the gap before {string} is wider than the gap before {string}")]
    public void ThenTheGapIsWider(string pivotal, string ordinary)
    {
        GapBefore(pivotal).ShouldBeGreaterThan(GapBefore(ordinary));
    }

    [Then("{string} keeps its position {int},{int}")]
    public void ThenKeepsItsPosition(string key, int x, int y)
    {
        var element = world.ElementNamed(key);
        (element.X, element.Y).ShouldBe((x, y));
    }

    [Then("the whole content change is announced for {string}")]
    public void ThenTheReplacementIsAnnounced(string board) =>
        world.Broadcaster.Replaced.ShouldContain(replaced => replaced.BoardId == world.Board(board).Id);

    [When("{word} exports {string}")]
    public async Task WhenExports(string person, string board)
    {
        var result = await world.ExportBoardDocument.Handle(new ExportBoardDocumentQuery(world.Person(person), world.Board(board).Id), CancellationToken.None);
        world.LastResult = result;
        exported = result.Document;
    }

    [Then("the export has {int} elements and {int} connection(s)")]
    public void ThenTheExportHas(int elements, int connections)
    {
        exported.ShouldNotBeNull();
        exported.Elements.Count.ShouldBe(elements);
        exported.Connections.Count.ShouldBe(connections);
    }

    [Then("the export says {string} sits in the swimlane {string}")]
    public void ThenTheExportSaysSitsIn(string key, string lane)
    {
        var element = exported.ShouldNotBeNull().Elements.Single(candidate => candidate.Key == world.Element(key).ToString());
        element.Swimlane.ShouldBe(world.Element(lane).ToString());
    }

    [When("{word} imports that export into {string}")]
    public async Task WhenImportsThatExport(string person, string team)
    {
        var document = new BoardDocument(
            exported!.Version,
            new DocumentBoard(exported.Board.Name + " (copy)", BoardLevels.Name(exported.Board.Level)),
            exported.Elements.Select(element => new DocumentElement(
                element.Type, element.Text, element.Key, element.Position, element.Size, element.Swimlane, element.Boundary, null, element.Pivotal, element.Color)).ToList(),
            exported.Connections.Select(connection => new DocumentConnection(connection.From, connection.To, connection.Label)).ToList());
        await Import(new ImportBoardDocumentCommand(world.Person(person), document, TeamId: world.Team(team).Id));
    }

    [Then("the copy has every element where the original had it")]
    public void ThenTheCopyMatches()
    {
        var original = exported.ShouldNotBeNull();
        var copy = world.Board(original.Board.Name + " (copy)");
        var copied = world.Boards.Elements.Where(element => element.BoardId == copy.Id).ToList();
        copied.Count.ShouldBe(original.Elements.Count);
        foreach (var element in original.Elements)
        {
            copied.ShouldContain(candidate => candidate.Text == element.Text && candidate.X == element.Position.X && candidate.Y == element.Position.Y);
        }
    }

    private async Task Import(ImportBoardDocumentCommand command)
    {
        var result = await world.ImportBoardDocument.Handle(command, CancellationToken.None);
        world.LastResult = result;
        if (result.Success)
        {
            foreach (var (key, id) in result.Imported!.KeyedIds)
            {
                world.Keys[key] = id;
            }
        }
    }

    private double GapBefore(string key)
    {
        var element = world.ElementNamed(key);
        var previous = world.Boards.Elements
            .Where(candidate => candidate.BoardId == element.BoardId && candidate.Type == "domain-event" && candidate.X < element.X && Math.Abs(candidate.Y - element.Y) < 1)
            .OrderByDescending(candidate => candidate.X)
            .First();
        return element.X - (previous.X + previous.Width);
    }

    private static BoardDocument Parse(string document) =>
        JsonSerializer.Deserialize<BoardDocument>(document, Json) ?? throw new InvalidOperationException("Empty document.");
}
