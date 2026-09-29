using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.BoardModelling.Slices.AddConnection;
using EventStorming.BoardModelling.Slices.AddElements;
using EventStorming.BoardModelling.Slices.ArchiveBoard;
using EventStorming.BoardModelling.Slices.CreateBoard;
using EventStorming.BoardModelling.Slices.DeleteBoard;
using EventStorming.BoardModelling.Slices.DeleteElements;
using EventStorming.BoardModelling.Slices.DuplicateBoard;
using EventStorming.BoardModelling.Slices.GetBoardSnapshot;
using EventStorming.BoardModelling.Slices.ImportBoardDocument;
using EventStorming.BoardModelling.Slices.ListBoards;
using EventStorming.BoardModelling.Slices.ListElements;
using EventStorming.BoardModelling.Slices.MoveElements;
using EventStorming.BoardModelling.Slices.UpdateElement;
using EventStorming.SharedKernel;
using EventStorming.Teams.Model;
using EventStorming.Teams.Slices.CreateTeam;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace EventStorming.Persistence.MongoDb.IntegrationTests;

public sealed class BoardStoreTests(MongoContainer mongo)
{
    private static readonly string[] Structures = ["swimlane", "boundary"];

    private static readonly DateTimeOffset Now = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);
    private static readonly ActorRef Ana = new(ActorKind.Account, Guid.NewGuid(), "Ana");

    [Fact]
    public async Task Inserting_elements_is_idempotent_by_id_and_bumps_the_revision_once()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var board = await NewBoard(scope);
        var store = scope.GetRequiredService<IAddElementsStore>();

        var placed = Sticky(board.Id, "Order Placed", 0, 0);
        var paid = Sticky(board.Id, "Payment Taken", 200, 0);
        var arrow = new Connection(Guid.NewGuid(), board.Id, placed.Id, paid.Id, null, 1, Now, Ana);

        var first = await store.Insert(board.Id, [placed, paid], [arrow], [], Ana, Now, CancellationToken.None);
        var again = await store.Insert(board.Id, [placed with { Text = "Changed" }, paid], [arrow], [], Ana, Now, CancellationToken.None);

        first.Revision.ShouldBe(1);
        again.Revision.ShouldBe(1, "nothing new was inserted");
        again.Elements.Single(element => element.Id == placed.Id).Text.ShouldBe("Order Placed");
        again.Connections.ShouldHaveSingleItem();

        var stats = await store.Stats(board.Id, Structures, CancellationToken.None);
        stats.ElementCount.ShouldBe(2);
        stats.ConnectionCount.ShouldBe(1);
        (stats.Left, stats.Right, stats.Bottom).ShouldBe((0d, 360d, 100d));
    }

    [Fact]
    public async Task Structures_grow_in_the_same_insert_and_the_timeline_ends_at_the_last_sticky()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var board = await NewBoard(scope);
        var store = scope.GetRequiredService<IAddElementsStore>();
        var lane = Sticky(board.Id, "Customer", 0, 0) with { Type = "swimlane", Width = 1600, Height = 240 };
        var placed = Sticky(board.Id, "Order Placed", 200, 40);
        await store.Insert(board.Id, [lane, placed], [], [], Ana, Now, CancellationToken.None);

        var stats = await store.Stats(board.Id, Structures, CancellationToken.None);
        (stats.Right, stats.ItemRight).ShouldBe((1600d, 360d));

        var paid = Sticky(board.Id, "Payment Taken", 1700, 40);
        var inserted = await store.Insert(board.Id, [paid], [], [lane with { Width = 1900 }], Ana, Now, CancellationToken.None);

        inserted.Revision.ShouldBe(2);
        var grown = inserted.Elements.Single(element => element.Id == lane.Id);
        (grown.Width, grown.Version).ShouldBe((1900d, 2L));
        inserted.Elements.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_second_arrow_between_the_same_elements_is_dropped_rather_than_aborting_the_insert()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var board = await NewBoard(scope);
        var store = scope.GetRequiredService<IAddElementsStore>();
        var placed = Sticky(board.Id, "Order Placed", 0, 0);
        var paid = Sticky(board.Id, "Payment Taken", 200, 0);
        await store.Insert(board.Id, [placed, paid], [new Connection(Guid.NewGuid(), board.Id, placed.Id, paid.Id, null, 1, Now, Ana)], [], Ana, Now, CancellationToken.None);

        var inserted = await store.Insert(board.Id, [Sticky(board.Id, "Meal Cooked", 400, 0)],
            [new Connection(Guid.NewGuid(), board.Id, placed.Id, paid.Id, "again", 1, Now, Ana)], [], Ana, Now, CancellationToken.None);

        inserted.Elements.ShouldHaveSingleItem();
        (await scope.GetRequiredService<IAddElementsStore>().Stats(board.Id, Structures, CancellationToken.None)).ConnectionCount.ShouldBe(1);
    }

    [Fact]
    public async Task An_update_writes_only_the_fields_it_carries_and_honours_the_expected_version()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var board = await NewBoard(scope);
        var placed = Sticky(board.Id, "Order Placed", 10, 20);
        await scope.GetRequiredService<IAddElementsStore>().Insert(board.Id, [placed], [], [], Ana, Now, CancellationToken.None);

        var moves = scope.GetRequiredService<IMoveElementsStore>();
        var updates = scope.GetRequiredService<IUpdateElementStore>();

        // Two people change different fields "at once": neither overwrites the other.
        await moves.Move(board.Id, [new ElementMove(placed.Id, new Position(500, 600))], Ana, Now, CancellationToken.None);
        var updated = (await updates.Update(board.Id, placed.Id, new ElementPatch("Order Submitted", null, null, null, null, null, null, null, false), null, Ana, Now, CancellationToken.None)).ShouldNotBeNull();

        updated.Element.Text.ShouldBe("Order Submitted");
        (updated.Element.X, updated.Element.Y).ShouldBe((500d, 600d));
        updated.Element.Version.ShouldBe(3);
        updated.Revision.ShouldBe(3);

        (await updates.Update(board.Id, placed.Id, new ElementPatch("Stale", null, null, null, null, null, null, null, false), 2, Ana, Now, CancellationToken.None)).ShouldBeNull();
        (await updates.Update(board.Id, placed.Id, new ElementPatch(null, null, null, null, null, null, null, "#FF0000", false), 3, Ana, Now, CancellationToken.None))!.Element.Color.ShouldBe("#FF0000");
        (await updates.Update(board.Id, placed.Id, new ElementPatch(null, null, null, null, null, null, null, null, true), null, Ana, Now, CancellationToken.None))!.Element.Color.ShouldBeNull();
    }

    [Fact]
    public async Task Deleting_a_board_removes_its_elements_and_connections_and_leaves_other_boards_alone()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var doomed = await NewBoard(scope);
        var kept = await NewBoard(scope);
        var adds = scope.GetRequiredService<IAddElementsStore>();
        foreach (var board in new[] { doomed, kept })
        {
            var placed = Sticky(board.Id, "Order Placed", 0, 0);
            var paid = Sticky(board.Id, "Payment Taken", 200, 0);
            await adds.Insert(board.Id, [placed, paid], [new Connection(Guid.NewGuid(), board.Id, placed.Id, paid.Id, null, 1, Now, Ana)], [], Ana, Now, CancellationToken.None);
        }

        var store = scope.GetRequiredService<IDeleteBoardStore>();
        (await store.Delete(doomed.Id, CancellationToken.None)).ShouldBeTrue();
        (await store.Delete(doomed.Id, CancellationToken.None)).ShouldBeFalse();

        var snapshot = scope.GetRequiredService<IGetBoardSnapshotStore>();
        (await snapshot.FindBoard(doomed.Id, CancellationToken.None)).ShouldBeNull();
        (await snapshot.Elements(doomed.Id, CancellationToken.None)).ShouldBeEmpty();
        (await snapshot.Connections(doomed.Id, CancellationToken.None)).ShouldBeEmpty();
        (await snapshot.FindBoard(kept.Id, CancellationToken.None)).ShouldNotBeNull();
        (await snapshot.Elements(kept.Id, CancellationToken.None)).Count.ShouldBe(2);
        (await snapshot.Connections(kept.Id, CancellationToken.None)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Deleting_elements_removes_their_connections_in_the_same_transaction()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var board = await NewBoard(scope);
        var placed = Sticky(board.Id, "Order Placed", 0, 0);
        var paid = Sticky(board.Id, "Payment Taken", 200, 0);
        await scope.GetRequiredService<IAddElementsStore>().Insert(board.Id, [placed, paid],
            [new Connection(Guid.NewGuid(), board.Id, placed.Id, paid.Id, null, 1, Now, Ana)], [], Ana, Now, CancellationToken.None);

        var deleted = await scope.GetRequiredService<IDeleteElementsStore>().Delete(board.Id, [placed.Id, Guid.NewGuid()], Ana, Now, CancellationToken.None);

        deleted.Elements.ShouldHaveSingleItem().Id.ShouldBe(placed.Id);
        deleted.Connections.Count.ShouldBe(1);
        var snapshot = scope.GetRequiredService<IGetBoardSnapshotStore>();
        (await snapshot.Connections(board.Id, CancellationToken.None)).ShouldBeEmpty();
        (await snapshot.FindBoard(board.Id, CancellationToken.None))!.ElementCount.ShouldBe(1);
    }

    [Fact]
    public async Task Replacing_the_content_swaps_everything_atomically()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var board = await NewBoard(scope);
        await scope.GetRequiredService<IAddElementsStore>().Insert(board.Id, [Sticky(board.Id, "Old", 0, 0)], [], [], Ana, Now, CancellationToken.None);

        var replaced = await scope.GetRequiredService<IImportBoardDocumentStore>()
            .ReplaceContents(board.Id, "Renamed", [Sticky(board.Id, "New A", 0, 0), Sticky(board.Id, "New B", 200, 0)], [], Ana, Now, CancellationToken.None);

        replaced.ShouldNotBeNull();
        replaced.Name.ShouldBe("Renamed");
        replaced.ElementCount.ShouldBe(2);
        replaced.Revision.ShouldBe(2);
        (await scope.GetRequiredService<IGetBoardSnapshotStore>().Elements(board.Id, CancellationToken.None)).Select(element => element.Text).ShouldBe(["New A", "New B"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_duplicate_gets_new_ids_with_its_connections_remapped()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var board = await NewBoard(scope);
        var placed = Sticky(board.Id, "Order Placed", 0, 0);
        var paid = Sticky(board.Id, "Payment Taken", 200, 0);
        await scope.GetRequiredService<IAddElementsStore>().Insert(board.Id, [placed, paid],
            [new Connection(Guid.NewGuid(), board.Id, placed.Id, paid.Id, null, 1, Now, Ana)], [], Ana, Now, CancellationToken.None);

        var copy = await scope.GetRequiredService<IDuplicateBoardStore>().Duplicate(board.Id, board with { Id = Guid.NewGuid(), Name = "Copy" }, CancellationToken.None);

        var snapshot = scope.GetRequiredService<IGetBoardSnapshotStore>();
        var elements = await snapshot.Elements(copy.Id, CancellationToken.None);
        var connection = (await snapshot.Connections(copy.Id, CancellationToken.None)).ShouldHaveSingleItem();
        elements.Count.ShouldBe(2);
        elements.Select(element => element.Id).ShouldNotContain(placed.Id);
        elements.Select(element => element.Id).ShouldContain(connection.From);
        elements.Select(element => element.Id).ShouldContain(connection.To);
    }

    [Fact]
    public async Task Boards_page_newest_first_with_cursors_that_cannot_be_forged()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var teamId = Guid.NewGuid();
        var create = scope.GetRequiredService<ICreateBoardStore>();
        for (var index = 0; index < 5; index++)
        {
            await create.Insert(NewBoardRecord(teamId, $"Board {index}", Now.AddMinutes(index)), CancellationToken.None);
        }

        var list = scope.GetRequiredService<IListBoardsStore>();
        var first = (await list.Page(teamId, false, null, 2, CancellationToken.None)).ShouldNotBeNull();
        var second = (await list.Page(teamId, false, first.NextCursor, 2, CancellationToken.None)).ShouldNotBeNull();
        var third = (await list.Page(teamId, false, second.NextCursor, 2, CancellationToken.None)).ShouldNotBeNull();

        first.Items.Select(board => board.Name).ShouldBe(["Board 4", "Board 3"]);
        second.Items.Select(board => board.Name).ShouldBe(["Board 2", "Board 1"]);
        third.Items.Select(board => board.Name).ShouldBe(["Board 0"]);
        third.NextCursor.ShouldBeNull();
        (await list.Page(teamId, false, "not-a-cursor", 2, CancellationToken.None)).ShouldBeNull();

        await scope.GetRequiredService<IArchiveBoardStore>().Archive(first.Items[0].Id, Ana, Now, CancellationToken.None);
        (await list.Page(teamId, false, null, 10, CancellationToken.None))!.Items.Count.ShouldBe(4);
        (await list.Page(teamId, true, null, 10, CancellationToken.None))!.Items.Count.ShouldBe(5);
    }

    [Fact]
    public async Task Elements_page_by_type()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var board = await NewBoard(scope);
        await scope.GetRequiredService<IAddElementsStore>().Insert(board.Id,
            [Sticky(board.Id, "A", 0, 0), Sticky(board.Id, "B", 0, 0), Sticky(board.Id, "C", 0, 0) with { Type = "hot-spot" }], [], [], Ana, Now, CancellationToken.None);

        var list = scope.GetRequiredService<IListElementsStore>();
        var page = (await list.Page(board.Id, "domain-event", null, 1, CancellationToken.None)).ShouldNotBeNull();
        var rest = (await list.Page(board.Id, "domain-event", page.NextCursor, 10, CancellationToken.None)).ShouldNotBeNull();
        page.Items.Count.ShouldBe(1);
        rest.Items.Count.ShouldBe(1);
        rest.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task The_acl_turns_team_roles_and_key_scopes_into_board_permissions()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var owner = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        var team = new Team(Guid.NewGuid(), "Team", Now, owner, [new Membership(owner, TeamRole.Owner, Now), new Membership(viewer, TeamRole.Viewer, Now)], 1);
        await scope.GetRequiredService<ICreateTeamStore>().Insert(team, CancellationToken.None);
        var board = NewBoardRecord(team.Id, "Board", Now);
        await scope.GetRequiredService<ICreateBoardStore>().Insert(board, CancellationToken.None);
        var access = scope.GetRequiredService<IBoardAccess>();

        (await access.ForBoard(board.Id, Actor.Account(owner, "Owner"), CancellationToken.None)).Permission.ShouldBe(BoardPermission.Edit);
        (await access.ForBoard(board.Id, Actor.Account(viewer, "Viewer"), CancellationToken.None)).Permission.ShouldBe(BoardPermission.View);
        (await access.ForBoard(board.Id, Actor.Account(Guid.NewGuid(), "Stranger"), CancellationToken.None)).Permission.ShouldBe(BoardPermission.None);
        (await access.ForBoard(board.Id, Actor.ApiKey(Guid.NewGuid(), "Key", team.Id, ["read"]), CancellationToken.None)).Permission.ShouldBe(BoardPermission.View);
        (await access.ForBoard(board.Id, Actor.ApiKey(Guid.NewGuid(), "Key", team.Id, ["read", "write"]), CancellationToken.None)).Permission.ShouldBe(BoardPermission.Edit);
        (await access.ForBoard(board.Id, Actor.ApiKey(Guid.NewGuid(), "Key", Guid.NewGuid(), ["read", "write"]), CancellationToken.None)).Permission.ShouldBe(BoardPermission.None);
        (await access.ForBoard(Guid.NewGuid(), Actor.Account(owner, "Owner"), CancellationToken.None)).ShouldBe(BoardAccess.NoAccess);

        await scope.GetRequiredService<IArchiveBoardStore>().Archive(board.Id, Ana, Now, CancellationToken.None);
        (await access.ForBoard(board.Id, Actor.Account(owner, "Owner"), CancellationToken.None)).Archived.ShouldBeTrue();
    }

    private static async Task<Board> NewBoard(IServiceProvider scope)
    {
        var board = NewBoardRecord(Guid.NewGuid(), "Ordering", Now);
        await scope.GetRequiredService<ICreateBoardStore>().Insert(board, CancellationToken.None);
        return board;
    }

    private static Board NewBoardRecord(Guid teamId, string name, DateTimeOffset at) =>
        new(Guid.NewGuid(), teamId, name, BoardLevel.BigPicture, 0, 0, at, Ana, at, Ana, null);

    private static Element Sticky(Guid boardId, string text, double x, double y) =>
        new(Guid.NewGuid(), boardId, "domain-event", text, x, y, 160, 100, false, null, 1, Now, Ana, Now, Ana);
}
