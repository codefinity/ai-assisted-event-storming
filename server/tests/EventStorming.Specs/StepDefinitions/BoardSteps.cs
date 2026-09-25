using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Slices.ArchiveBoard;
using EventStorming.BoardModelling.Slices.CreateBoard;
using EventStorming.BoardModelling.Slices.DuplicateBoard;
using EventStorming.BoardModelling.Slices.GetBoard;
using EventStorming.BoardModelling.Slices.GetBoardSnapshot;
using EventStorming.BoardModelling.Slices.ListBoards;
using EventStorming.BoardModelling.Slices.ListElementTypes;
using EventStorming.BoardModelling.Slices.RenameBoard;
using EventStorming.BoardModelling.Slices.RestoreBoard;
using EventStorming.SharedKernel;
using EventStorming.Specs.Support;
using Reqnroll;
using Shouldly;

namespace EventStorming.Specs.StepDefinitions;

[Binding]
public sealed class BoardSteps(World world)
{
    [Given("{string} has a {string} board called {string}")]
    public void GivenTheTeamHasABoard(string team, string level, string name)
    {
        var owner = world.Team(team).Members.First().AccountId;
        var by = new ActorRef(ActorKind.Account, owner, "Owner");
        world.Clock.Advance(TimeSpan.FromMinutes(1));
        world.Boards.Boards.Add(new Board(Guid.CreateVersion7(), world.Team(team).Id, name, BoardLevels.Parse(level), 0, 0, world.Clock.UtcNow, by, world.Clock.UtcNow, by, null));
    }

    [Given("the board {string} is archived")]
    public void GivenTheBoardIsArchived(string name)
    {
        var index = world.Boards.Boards.FindIndex(board => board.Name == name);
        world.Boards.Boards[index] = world.Boards.Boards[index] with { ArchivedAt = world.Clock.UtcNow };
    }

    [When("{word} creates a {string} board called {string} in {string}")]
    public async Task WhenCreatesABoard(string person, string level, string name, string team) =>
        world.LastResult = await world.CreateBoard.Handle(new CreateBoardCommand(world.Person(person), world.Team(team).Id, name, level), CancellationToken.None);

    [Then("{string} has a board called {string} at the {string} level")]
    public void ThenTheTeamHasABoardAtLevel(string team, string name, string level)
    {
        var board = world.Boards.Boards.Single(candidate => candidate.Name == name);
        board.TeamId.ShouldBe(world.Team(team).Id);
        BoardLevels.Name(board.Level).ShouldBe(level);
    }

    [Then("{string} has a board called {string} with {int} elements")]
    public void ThenTheTeamHasABoardWithElements(string team, string name, int count)
    {
        var board = world.Boards.Boards.Single(candidate => candidate.Name == name && candidate.TeamId == world.Team(team).Id);
        world.Boards.Elements.Count(element => element.BoardId == board.Id).ShouldBe(count);
        board.ElementCount.ShouldBe(count);
    }

    [When("{word} lists the boards of {string}")]
    public async Task WhenListsTheBoards(string person, string team) =>
        world.LastResult = await world.ListBoards.Handle(new ListBoardsQuery(world.Person(person), world.Team(team).Id), CancellationToken.None);

    [When("{word} lists the boards of {string} including archived ones")]
    public async Task WhenListsAllTheBoards(string person, string team) =>
        world.LastResult = await world.ListBoards.Handle(new ListBoardsQuery(world.Person(person), world.Team(team).Id, IncludeArchived: true), CancellationToken.None);

    [When("{word} lists the boards of {string} {int} at a time")]
    public async Task WhenListsTheBoardsPaged(string person, string team, int limit) =>
        world.LastResult = await world.ListBoards.Handle(new ListBoardsQuery(world.Person(person), world.Team(team).Id, Limit: limit), CancellationToken.None);

    [When("{word} lists the boards of {string} from the cursor {string}")]
    public async Task WhenListsTheBoardsFromCursor(string person, string team, string cursor) =>
        world.LastResult = await world.ListBoards.Handle(new ListBoardsQuery(world.Person(person), world.Team(team).Id, Cursor: cursor), CancellationToken.None);

    [Then("the boards listed are, most recent first:")]
    public void ThenTheBoardsListedAre(DataTable table) =>
        world.Last<ListBoardsResult>().Page.ShouldNotBeNull().Items.Select(board => board.Name).ShouldBe(table.Rows.Select(row => row["board"]));

    [Then("the listing has a next page")]
    public void ThenTheListingHasANextPage() => world.Last<ListBoardsResult>().Page!.NextCursor.ShouldNotBeNull();

    [Then("they may {word} the team's boards")]
    public void ThenTheyMayTheTeamsBoards(string permission) =>
        world.Last<ListBoardsResult>().Permission.ToString().ToLowerInvariant().ShouldBe(permission);

    [When("{word} opens the board {string}")]
    public async Task WhenOpensTheBoard(string person, string name) =>
        world.LastResult = await world.GetBoardSnapshot.Handle(new GetBoardSnapshotQuery(world.Person(person), world.Board(name).Id), CancellationToken.None);

    [When("{word} looks up the board {string}")]
    public async Task WhenLooksUpTheBoard(string person, string name) =>
        world.LastResult = await world.GetBoard.Handle(new GetBoardQuery(world.Person(person), world.Board(name).Id), CancellationToken.None);

    [Then("they can {word} the board")]
    public void ThenTheyCanTheBoard(string permission)
    {
        var granted = world.LastResult switch
        {
            GetBoardSnapshotResult snapshot => snapshot.Snapshot!.Permission,
            GetBoardResult board => board.Permission,
            _ => throw new InvalidOperationException("No board was opened."),
        };
        granted.ToString().ToLowerInvariant().ShouldBe(permission);
    }

    [Then("the snapshot has {int} elements and {int} connection(s)")]
    public void ThenTheSnapshotHas(int elements, int connections)
    {
        var snapshot = world.Last<GetBoardSnapshotResult>().Snapshot.ShouldNotBeNull();
        snapshot.Elements.Count.ShouldBe(elements);
        snapshot.Connections.Count.ShouldBe(connections);
    }

    [When("{word} renames the board {string} to {string}")]
    public async Task WhenRenamesTheBoard(string person, string name, string newName) =>
        world.LastResult = await world.RenameBoard.Handle(new RenameBoardCommand(world.Person(person), world.Board(name).Id, newName), CancellationToken.None);

    [When("{word} duplicates the board {string}")]
    public async Task WhenDuplicatesTheBoard(string person, string name) =>
        world.LastResult = await world.DuplicateBoard.Handle(new DuplicateBoardCommand(world.Person(person), world.Board(name).Id, null), CancellationToken.None);

    [When("{word} archives the board {string}")]
    public async Task WhenArchivesTheBoard(string person, string name) =>
        world.LastResult = await world.ArchiveBoard.Handle(new ArchiveBoardCommand(world.Person(person), world.Board(name).Id), CancellationToken.None);

    [When("{word} restores the board {string}")]
    public async Task WhenRestoresTheBoard(string person, string name) =>
        world.LastResult = await world.RestoreBoard.Handle(new RestoreBoardCommand(world.Person(person), world.Board(name).Id), CancellationToken.None);

    [Then("the board {string} is archived")]
    public void ThenTheBoardIsArchived(string name) => world.Board(name).ArchivedAt.ShouldNotBeNull();

    [Then("the board {string} is not archived")]
    public void ThenTheBoardIsNotArchived(string name) => world.Board(name).ArchivedAt.ShouldBeNull();

    [Then("the new name is announced to everyone on the board")]
    [Then("the change of state is announced to everyone on the board")]
    public void ThenTheDetailsAreAnnounced() => world.Broadcaster.Details.ShouldNotBeEmpty();

    [When("{word} asks for the element types")]
    public async Task WhenAsksForTheElementTypes(string person) =>
        world.LastResult = await world.ListElementTypes.Handle(new ListElementTypesQuery(), CancellationToken.None);

    [Then("the notation lists {int} element types and {int} levels")]
    public void ThenTheNotationLists(int types, int levels)
    {
        var notation = world.Last<ListElementTypesResult>().Notation;
        notation.Types.Count.ShouldBe(types);
        notation.Levels.Count.ShouldBe(levels);
    }
}
