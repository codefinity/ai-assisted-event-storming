using System.Globalization;
using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Slices.AddConnection;
using EventStorming.BoardModelling.Slices.AddElements;
using EventStorming.BoardModelling.Slices.ArchiveBoard;
using EventStorming.BoardModelling.Slices.CreateBoard;
using EventStorming.BoardModelling.Slices.DeleteBoard;
using EventStorming.BoardModelling.Slices.DeleteConnections;
using EventStorming.BoardModelling.Slices.DeleteElements;
using EventStorming.BoardModelling.Slices.DuplicateBoard;
using EventStorming.BoardModelling.Slices.ExportBoardDocument;
using EventStorming.BoardModelling.Slices.GetBoard;
using EventStorming.BoardModelling.Slices.GetBoardSnapshot;
using EventStorming.BoardModelling.Slices.GetElement;
using EventStorming.BoardModelling.Slices.ImportBoardDocument;
using EventStorming.BoardModelling.Slices.ListBoards;
using EventStorming.BoardModelling.Slices.ListConnections;
using EventStorming.BoardModelling.Slices.ListElements;
using EventStorming.BoardModelling.Slices.MoveElements;
using EventStorming.BoardModelling.Slices.RenameBoard;
using EventStorming.BoardModelling.Slices.RestoreBoard;
using EventStorming.BoardModelling.Slices.UpdateElement;
using EventStorming.SharedKernel;

namespace EventStorming.Specs.Support.Fakes;

/// <summary>
/// Every Board Modelling store port over three lists, keeping the promises the MongoDB stores make:
/// versions and revisions increase on every change, inserts by id are idempotent, deleting an element
/// deletes its connections.
/// </summary>
public sealed class InMemoryBoards :
    ICreateBoardStore,
    IListBoardsStore,
    IGetBoardStore,
    IGetBoardSnapshotStore,
    IExportBoardDocumentStore,
    IRenameBoardStore,
    IDuplicateBoardStore,
    IArchiveBoardStore,
    IRestoreBoardStore,
    IDeleteBoardStore,
    IImportBoardDocumentStore,
    IAddElementsStore,
    IUpdateElementStore,
    IGetElementStore,
    IMoveElementsStore,
    IDeleteElementsStore,
    IAddConnectionStore,
    IDeleteConnectionsStore,
    IListElementsStore,
    IListConnectionsStore
{
    public List<Board> Boards { get; } = [];

    public List<Element> Elements { get; } = [];

    public List<Connection> Connections { get; } = [];

    public Board Named(string name) => Boards.Single(board => board.Name == name);

    public Task Insert(Board board, CancellationToken cancellationToken)
    {
        Boards.Add(board);
        return Task.CompletedTask;
    }

    public Task<Page<Board>?> Page(Guid teamId, bool includeArchived, string? cursor, int limit, CancellationToken cancellationToken)
    {
        var ordered = Boards
            .Where(board => board.TeamId == teamId && (includeArchived || board.ArchivedAt is null))
            .OrderByDescending(board => board.UpdatedAt).ThenByDescending(board => board.Id)
            .ToList();
        return Task.FromResult(Paged(ordered, cursor, limit));
    }

    public Task<Board?> Find(Guid boardId, CancellationToken cancellationToken) => FindBoard(boardId, cancellationToken);

    public Task<Board?> FindBoard(Guid boardId, CancellationToken cancellationToken) =>
        Task.FromResult(Boards.FirstOrDefault(board => board.Id == boardId));

    Task<IReadOnlyList<Element>> IGetBoardSnapshotStore.Elements(Guid boardId, CancellationToken cancellationToken) => ElementsOf(boardId);

    Task<IReadOnlyList<Element>> IExportBoardDocumentStore.Elements(Guid boardId, CancellationToken cancellationToken) => ElementsOf(boardId);

    Task<IReadOnlyList<Connection>> IGetBoardSnapshotStore.Connections(Guid boardId, CancellationToken cancellationToken) => ConnectionsOf(boardId);

    Task<IReadOnlyList<Connection>> IExportBoardDocumentStore.Connections(Guid boardId, CancellationToken cancellationToken) => ConnectionsOf(boardId);

    public Task<Board?> Rename(Guid boardId, string name, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken) =>
        Task.FromResult(Replace(boardId, board => board with { Name = name, UpdatedAt = at, UpdatedBy = by }));

    public Task<Board> Duplicate(Guid sourceBoardId, Board copy, CancellationToken cancellationToken)
    {
        var ids = Elements.Where(element => element.BoardId == sourceBoardId).ToDictionary(element => element.Id, _ => Guid.NewGuid());
        foreach (var element in Elements.Where(element => element.BoardId == sourceBoardId).ToList())
        {
            Elements.Add(element with { Id = ids[element.Id], BoardId = copy.Id, Version = 1 });
        }

        foreach (var connection in Connections.Where(connection => connection.BoardId == sourceBoardId).ToList())
        {
            Connections.Add(connection with { Id = Guid.NewGuid(), BoardId = copy.Id, From = ids[connection.From], To = ids[connection.To], Version = 1 });
        }

        var board = copy with { ElementCount = ids.Count };
        Boards.Add(board);
        return Task.FromResult(board);
    }

    public Task<Board?> Archive(Guid boardId, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken) =>
        Task.FromResult(Replace(boardId, board => board.ArchivedAt is null ? board with { ArchivedAt = at, UpdatedAt = at, UpdatedBy = by } : board));

    public Task<Board?> Restore(Guid boardId, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken) =>
        Task.FromResult(Replace(boardId, board => board.ArchivedAt is null ? board : board with { ArchivedAt = null, UpdatedAt = at, UpdatedBy = by }));

    public Task<bool> Delete(Guid boardId, CancellationToken cancellationToken)
    {
        Elements.RemoveAll(element => element.BoardId == boardId);
        Connections.RemoveAll(connection => connection.BoardId == boardId);
        return Task.FromResult(Boards.RemoveAll(board => board.Id == boardId) > 0);
    }

    public Task InsertNew(Board board, IReadOnlyList<Element> elements, IReadOnlyList<Connection> connections, CancellationToken cancellationToken)
    {
        Boards.Add(board);
        Elements.AddRange(elements);
        Connections.AddRange(connections);
        return Task.CompletedTask;
    }

    public Task<Board?> ReplaceContents(Guid boardId, string? newName, IReadOnlyList<Element> elements, IReadOnlyList<Connection> connections, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken)
    {
        Elements.RemoveAll(element => element.BoardId == boardId);
        Connections.RemoveAll(connection => connection.BoardId == boardId);
        Elements.AddRange(elements);
        Connections.AddRange(connections);
        return Task.FromResult(Replace(boardId, board => board with
        {
            Name = newName ?? board.Name,
            Revision = board.Revision + 1,
            ElementCount = elements.Count,
            UpdatedAt = at,
            UpdatedBy = by,
        }));
    }

    public Task<BoardContentStats> Stats(Guid boardId, IReadOnlyCollection<string> structureTypes, CancellationToken cancellationToken)
    {
        var elements = Elements.Where(element => element.BoardId == boardId).ToList();
        var items = elements.Where(element => !structureTypes.Contains(element.Type)).ToList();
        var connectionCount = Connections.Count(connection => connection.BoardId == boardId);
        return Task.FromResult(elements.Count == 0
            ? new BoardContentStats(0, connectionCount, null, null, null, null)
            : new BoardContentStats(
                elements.Count,
                connectionCount,
                elements.Min(element => element.X),
                elements.Min(element => element.Y),
                elements.Max(element => element.X + element.Width),
                elements.Max(element => element.Y + element.Height),
                items.Count == 0 ? null : items.Max(element => element.X + element.Width)));
    }

    public Task<IReadOnlyList<Element>> FindElements(Guid boardId, IReadOnlyCollection<Guid> elementIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Element>>(Elements.Where(element => element.BoardId == boardId && elementIds.Contains(element.Id)).ToList());

    public Task<InsertedContent> Insert(Guid boardId, IReadOnlyList<Element> elements, IReadOnlyList<Connection> connections, IReadOnlyList<Element> grown, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken)
    {
        foreach (var resized in grown)
        {
            var index = Elements.FindIndex(element => element.Id == resized.Id && element.BoardId == boardId);
            if (index >= 0)
            {
                Elements[index] = Elements[index] with
                {
                    X = resized.X,
                    Y = resized.Y,
                    Width = resized.Width,
                    Height = resized.Height,
                    Version = Elements[index].Version + 1,
                    UpdatedAt = at,
                    UpdatedBy = by,
                };
            }
        }

        var added = 0;
        foreach (var element in elements.Where(element => Elements.All(existing => existing.Id != element.Id)))
        {
            Elements.Add(element);
            added++;
        }

        var addedConnections = 0;
        foreach (var connection in connections.Where(connection => Connections.All(existing => existing.Id != connection.Id
                     && (existing.BoardId != boardId || existing.From != connection.From || existing.To != connection.To))))
        {
            Connections.Add(connection);
            addedConnections++;
        }

        var revision = added + addedConnections + grown.Count > 0 ? Bump(boardId, by, at, added) : Revision(boardId);
        var ids = elements.Concat(grown).Select(element => element.Id).ToHashSet();
        var connectionIds = connections.Select(connection => connection.Id).ToHashSet();
        return Task.FromResult(new InsertedContent(
            revision,
            Elements.Where(element => element.BoardId == boardId && ids.Contains(element.Id)).ToList(),
            Connections.Where(connection => connection.BoardId == boardId && connectionIds.Contains(connection.Id)).ToList()));
    }

    public Task<Element?> Find(Guid boardId, Guid elementId, CancellationToken cancellationToken) =>
        Task.FromResult(Elements.FirstOrDefault(element => element.BoardId == boardId && element.Id == elementId));

    public Task<UpdatedElement?> Update(Guid boardId, Guid elementId, ElementPatch patch, long? expectedVersion, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var index = Elements.FindIndex(element => element.BoardId == boardId && element.Id == elementId);
        if (index < 0 || (expectedVersion is { } version && Elements[index].Version != version))
        {
            return Task.FromResult<UpdatedElement?>(null);
        }

        var current = Elements[index];
        Elements[index] = current with
        {
            Text = patch.Text ?? current.Text,
            Type = patch.Type ?? current.Type,
            X = patch.X ?? current.X,
            Y = patch.Y ?? current.Y,
            Width = patch.Width ?? current.Width,
            Height = patch.Height ?? current.Height,
            Pivotal = patch.Pivotal ?? current.Pivotal,
            Color = patch.ClearColor ? null : patch.Color ?? current.Color,
            Version = current.Version + 1,
            UpdatedAt = at,
            UpdatedBy = by,
        };
        return Task.FromResult<UpdatedElement?>(new UpdatedElement(Bump(boardId, by, at, 0), Elements[index]));
    }

    public Task<MovedElements> Move(Guid boardId, IReadOnlyList<ElementMove> moves, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var moved = new List<Element>();
        foreach (var move in moves)
        {
            var index = Elements.FindIndex(element => element.BoardId == boardId && element.Id == move.ElementId);
            if (index >= 0)
            {
                Elements[index] = Elements[index] with { X = move.Position.X, Y = move.Position.Y, Version = Elements[index].Version + 1, UpdatedAt = at, UpdatedBy = by };
                moved.Add(Elements[index]);
            }
        }

        return Task.FromResult(new MovedElements(moved.Count > 0 ? Bump(boardId, by, at, 0) : Revision(boardId), moved));
    }

    public Task<DeletedContent> Delete(Guid boardId, IReadOnlyList<Guid> elementIds, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var removed = Elements.Where(element => element.BoardId == boardId && elementIds.Contains(element.Id)).Select(element => new Removed(element.Id, element.Version)).ToList();
        var ids = removed.Select(item => item.Id).ToHashSet();
        var removedConnections = Connections
            .Where(connection => connection.BoardId == boardId && (ids.Contains(connection.From) || ids.Contains(connection.To)))
            .Select(connection => new Removed(connection.Id, connection.Version))
            .ToList();

        Elements.RemoveAll(element => ids.Contains(element.Id));
        Connections.RemoveAll(connection => removedConnections.Any(item => item.Id == connection.Id));
        return Task.FromResult(new DeletedContent(removed.Count > 0 ? Bump(boardId, by, at, -removed.Count) : Revision(boardId), removed, removedConnections));
    }

    public Task<ConnectionEndpoints> Endpoints(Guid boardId, Guid from, Guid to, CancellationToken cancellationToken) =>
        Task.FromResult(new ConnectionEndpoints(
            Elements.Any(element => element.BoardId == boardId && element.Id == from),
            Elements.Any(element => element.BoardId == boardId && element.Id == to),
            Connections.Count(connection => connection.BoardId == boardId),
            Connections.FirstOrDefault(connection => connection.BoardId == boardId && connection.From == from && connection.To == to)));

    public Task<AddedConnection> Insert(Connection connection, CancellationToken cancellationToken)
    {
        Connections.Add(connection);
        return Task.FromResult(new AddedConnection(Bump(connection.BoardId, connection.CreatedBy, connection.CreatedAt, 0), connection, AlreadyExisted: false));
    }

    Task<DeletedConnections> IDeleteConnectionsStore.Delete(Guid boardId, IReadOnlyList<Guid> connectionIds, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var removed = Connections.Where(connection => connection.BoardId == boardId && connectionIds.Contains(connection.Id)).Select(connection => new Removed(connection.Id, connection.Version)).ToList();
        Connections.RemoveAll(connection => removed.Any(item => item.Id == connection.Id));
        return Task.FromResult(new DeletedConnections(removed.Count > 0 ? Bump(boardId, by, at, 0) : Revision(boardId), removed));
    }

    public Task<Page<Element>?> Page(Guid boardId, string? type, string? cursor, int limit, CancellationToken cancellationToken) =>
        Task.FromResult(Paged(Elements.Where(element => element.BoardId == boardId && (type is null || element.Type == type)).OrderBy(element => element.Id).ToList(), cursor, limit));

    public Task<Page<Connection>?> Page(Guid boardId, string? cursor, int limit, CancellationToken cancellationToken) =>
        Task.FromResult(Paged(Connections.Where(connection => connection.BoardId == boardId).OrderBy(connection => connection.Id).ToList(), cursor, limit));

    private static Page<T>? Paged<T>(IReadOnlyList<T> ordered, string? cursor, int limit)
    {
        var offset = 0;
        if (cursor is not null && (!cursor.StartsWith("offset:", StringComparison.Ordinal) || !int.TryParse(cursor[7..], NumberStyles.None, CultureInfo.InvariantCulture, out offset)))
        {
            return null;
        }

        var items = ordered.Skip(offset).Take(limit).ToList();
        return new Page<T>(items, offset + limit < ordered.Count ? $"offset:{offset + limit}" : null);
    }

    private Task<IReadOnlyList<Element>> ElementsOf(Guid boardId) =>
        Task.FromResult<IReadOnlyList<Element>>(Elements.Where(element => element.BoardId == boardId).ToList());

    private Task<IReadOnlyList<Connection>> ConnectionsOf(Guid boardId) =>
        Task.FromResult<IReadOnlyList<Connection>>(Connections.Where(connection => connection.BoardId == boardId).ToList());

    private Board? Replace(Guid boardId, Func<Board, Board> change)
    {
        var index = Boards.FindIndex(board => board.Id == boardId);
        if (index < 0)
        {
            return null;
        }

        Boards[index] = change(Boards[index]);
        return Boards[index];
    }

    private long Bump(Guid boardId, ActorRef by, DateTimeOffset at, int elementDelta) =>
        Replace(boardId, board => board with { Revision = board.Revision + 1, ElementCount = board.ElementCount + elementDelta, UpdatedAt = at, UpdatedBy = by })?.Revision ?? 0;

    private long Revision(Guid boardId) => Boards.FirstOrDefault(board => board.Id == boardId)?.Revision ?? 0;
}
