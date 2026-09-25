using EventStorming.Collaboration.Model;
using EventStorming.Collaboration.Slices.JoinBoard;
using EventStorming.Collaboration.Slices.LeaveBoard;
using EventStorming.Collaboration.Slices.MoveCursor;
using EventStorming.Collaboration.Slices.SetEditingFocus;
using EventStorming.Collaboration.Slices.ShareDragPreview;
using EventStorming.Specs.Support;
using Reqnroll;
using Shouldly;

namespace EventStorming.Specs.StepDefinitions;

[Binding]
public sealed class CollaborationSteps(World world)
{
    [When("{word} joins {string} on connection {string}")]
    [Given("{word} has joined {string} on connection {string}")]
    public async Task WhenJoins(string person, string board, string connection) =>
        world.LastResult = await world.JoinBoard.Handle(new JoinBoardCommand(world.Person(person), world.Board(board).Id, connection), CancellationToken.None);

    [Then("{int} people are on {string}")]
    [Then("{int} person is on {string}")]
    public void ThenPeopleAreOn(int count, string board) =>
        world.Presence.Participants.Values.Count(participant => participant.BoardId == world.Board(board).Id).ShouldBe(count);

    [Then("{word} sees {int} participant(s), including themselves")]
    public void ThenSeesParticipants(string person, int count)
    {
        var joined = world.Last<JoinBoardResult>().Joined.ShouldNotBeNull();
        joined.Participants.Count.ShouldBe(count);
        joined.You.DisplayName.ShouldBe(person);
        joined.Participants.ShouldContain(participant => participant.ConnectionId == joined.You.ConnectionId);
    }

    [Then("{word} always gets the same color")]
    public void ThenAlwaysGetsTheSameColor(string person) =>
        world.Last<JoinBoardResult>().Joined!.You.Color.ShouldBe(ParticipantColors.For(world.Person(person).Id));

    [Then("the others are told that {word} {word}")]
    public void ThenTheOthersAreTold(string person, string what) =>
        world.Broadcaster.Presence.ShouldContain(item => item.What == what && item.Participant.DisplayName == person);

    [When("connection {string} disconnects")]
    public async Task WhenDisconnects(string connection) =>
        world.LastResult = await world.LeaveBoard.Handle(new LeaveBoardCommand(connection), CancellationToken.None);

    [When("connection {string} moves its cursor to {int},{int} on {string}")]
    public async Task WhenMovesItsCursor(string connection, int x, int y, string board) =>
        world.LastResult = await world.MoveCursor.Handle(new MoveCursorCommand(connection, world.Board(board).Id, x, y), CancellationToken.None);

    [When("connection {string} starts editing {string} on {string}")]
    public async Task WhenStartsEditing(string connection, string key, string board) =>
        world.LastResult = await world.SetEditingFocus.Handle(new SetEditingFocusCommand(connection, world.Board(board).Id, world.Element(key)), CancellationToken.None);

    [Then("connection {string} is shown editing {string}")]
    public void ThenIsShownEditing(string connection, string key) =>
        world.Presence.Participants[connection].EditingElementId.ShouldBe(world.Element(key));

    [When("connection {string} drags {string} to {int},{int} on {string}")]
    public async Task WhenDrags(string connection, string key, int x, int y, string board) =>
        world.LastResult = await world.ShareDragPreview.Handle(
            new ShareDragPreviewCommand(connection, world.Board(board).Id, [new DragMove(world.Element(key), x, y)]), CancellationToken.None);
}
