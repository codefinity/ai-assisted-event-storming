using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Slices.ArchiveBoard;
using EventStorming.BoardModelling.Slices.CreateBoard;
using EventStorming.BoardModelling.Slices.DuplicateBoard;
using EventStorming.BoardModelling.Slices.ExportBoardDocument;
using EventStorming.BoardModelling.Slices.GetBoard;
using EventStorming.BoardModelling.Slices.GetBoardSnapshot;
using EventStorming.BoardModelling.Slices.ImportBoardDocument;
using EventStorming.BoardModelling.Slices.ListBoards;
using EventStorming.BoardModelling.Slices.RenameBoard;
using EventStorming.BoardModelling.Slices.RestoreBoard;
using EventStorming.Persistence.MongoDb.Infrastructure;
using EventStorming.SharedKernel;
using MongoDB.Driver;

namespace EventStorming.Persistence.MongoDb.BoardModelling;

internal sealed class CreateBoardStore(MongoDatabase mongo) : ICreateBoardStore
{
    public Task Insert(Board board, CancellationToken cancellationToken) =>
        BoardCollections.BoardsIn(mongo).InsertOneAsync(MongoBoard.From(board), cancellationToken: cancellationToken);
}

internal sealed class ListBoardsStore(MongoDatabase mongo) : IListBoardsStore
{
    public async Task<Page<Board>?> Page(Guid teamId, bool includeArchived, string? cursor, int limit, CancellationToken cancellationToken)
    {
        var filters = Builders<MongoBoard>.Filter;
        var filter = filters.Eq(board => board.TeamId, teamId);
        if (!includeArchived)
        {
            filter &= filters.Eq(board => board.ArchivedAt, null);
        }

        if (cursor is not null)
        {
            // Keyset: strictly after (updatedAt, id) of the last board on the previous page.
            var parts = Cursors.Decode(cursor, 2);
            var updatedAt = parts is null ? null : Cursors.FromTicks(parts[0]);
            if (parts is null || updatedAt is null || !Guid.TryParse(parts[1], out var lastId))
            {
                return null;
            }

            filter &= filters.Or(
                filters.Lt(board => board.UpdatedAt, updatedAt.Value),
                filters.And(filters.Eq(board => board.UpdatedAt, updatedAt.Value), filters.Lt(board => board.Id, lastId)));
        }

        var boards = await BoardCollections.BoardsIn(mongo)
            .Find(filter)
            .SortByDescending(board => board.UpdatedAt)
            .ThenByDescending(board => board.Id)
            .Limit(limit + 1)
            .ToListAsync(cancellationToken);

        var page = boards.Take(limit).Select(board => board.ToModel()).ToList();
        var next = boards.Count > limit ? Cursors.Encode(Cursors.Ticks(page[^1].UpdatedAt), page[^1].Id.ToString()) : null;
        return new Page<Board>(page, next);
    }
}

internal sealed class GetBoardStore(MongoDatabase mongo) : IGetBoardStore
{
    public Task<Board?> Find(Guid boardId, CancellationToken cancellationToken) => BoardCollections.FindBoard(mongo, boardId, cancellationToken);
}

internal sealed class GetBoardSnapshotStore(MongoDatabase mongo) : IGetBoardSnapshotStore, IExportBoardDocumentStore
{
    public Task<Board?> FindBoard(Guid boardId, CancellationToken cancellationToken) => BoardCollections.FindBoard(mongo, boardId, cancellationToken);

    public Task<IReadOnlyList<Element>> Elements(Guid boardId, CancellationToken cancellationToken) => BoardCollections.ElementsOf(mongo, boardId, cancellationToken);

    public Task<IReadOnlyList<Connection>> Connections(Guid boardId, CancellationToken cancellationToken) => BoardCollections.ConnectionsOf(mongo, boardId, cancellationToken);
}

internal sealed class RenameBoardStore(MongoDatabase mongo) : IRenameBoardStore
{
    public async Task<Board?> Rename(Guid boardId, string name, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var board = await BoardCollections.BoardsIn(mongo).FindOneAndUpdateAsync(
            candidate => candidate.Id == boardId,
            Builders<MongoBoard>.Update
                .Set(candidate => candidate.Name, name)
                .Set(candidate => candidate.UpdatedAt, at)
                .Set(candidate => candidate.UpdatedBy, MongoActorRef.From(by)),
            new FindOneAndUpdateOptions<MongoBoard> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return board?.ToModel();
    }
}

internal sealed class DuplicateBoardStore(MongoDatabase mongo) : IDuplicateBoardStore
{
    public Task<Board?> FindBoard(Guid boardId, CancellationToken cancellationToken) => BoardCollections.FindBoard(mongo, boardId, cancellationToken);

    public Task<Board> Duplicate(Guid sourceBoardId, Board copy, CancellationToken cancellationToken) =>
        mongo.InTransaction(async (session, token) =>
        {
            var elements = await BoardCollections.ElementsIn(mongo).Find(session, element => element.BoardId == sourceBoardId).ToListAsync(token);
            var connections = await BoardCollections.ConnectionsIn(mongo).Find(session, connection => connection.BoardId == sourceBoardId).ToListAsync(token);

            var newIds = elements.ToDictionary(element => element.Id, _ => Guid.CreateVersion7());
            var by = MongoActorRef.From(copy.CreatedBy);
            var board = copy with { ElementCount = elements.Count };

            await BoardCollections.BoardsIn(mongo).InsertOneAsync(session, MongoBoard.From(board), cancellationToken: token);

            if (elements.Count > 0)
            {
                await BoardCollections.ElementsIn(mongo).InsertManyAsync(
                    session,
                    elements.Select(element => new MongoElement
                    {
                        Id = newIds[element.Id],
                        BoardId = board.Id,
                        Type = element.Type,
                        Text = element.Text,
                        X = element.X,
                        Y = element.Y,
                        Width = element.Width,
                        Height = element.Height,
                        Pivotal = element.Pivotal,
                        Color = element.Color,
                        Version = 1,
                        CreatedAt = copy.CreatedAt,
                        CreatedBy = by,
                        UpdatedAt = copy.CreatedAt,
                        UpdatedBy = by,
                    }),
                    cancellationToken: token);
            }

            var copiedConnections = connections
                .Where(connection => newIds.ContainsKey(connection.From) && newIds.ContainsKey(connection.To))
                .Select(connection => new MongoConnection
                {
                    Id = Guid.CreateVersion7(),
                    BoardId = board.Id,
                    From = newIds[connection.From],
                    To = newIds[connection.To],
                    Label = connection.Label,
                    Version = 1,
                    CreatedAt = copy.CreatedAt,
                    CreatedBy = by,
                })
                .ToList();

            if (copiedConnections.Count > 0)
            {
                await BoardCollections.ConnectionsIn(mongo).InsertManyAsync(session, copiedConnections, cancellationToken: token);
            }

            return board;
        }, cancellationToken);
}

internal sealed class ArchiveBoardStore(MongoDatabase mongo) : IArchiveBoardStore, IRestoreBoardStore
{
    public Task<Board?> Archive(Guid boardId, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken) =>
        SetArchived(boardId, at, by, at, cancellationToken);

    public Task<Board?> Restore(Guid boardId, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken) =>
        SetArchived(boardId, null, by, at, cancellationToken);

    private async Task<Board?> SetArchived(Guid boardId, DateTimeOffset? archivedAt, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var boards = BoardCollections.BoardsIn(mongo);
        var filter = archivedAt is null
            ? Builders<MongoBoard>.Filter.Where(board => board.Id == boardId && board.ArchivedAt != null)
            : Builders<MongoBoard>.Filter.Where(board => board.Id == boardId && board.ArchivedAt == null);

        var changed = await boards.FindOneAndUpdateAsync(
            filter,
            Builders<MongoBoard>.Update
                .Set(board => board.ArchivedAt, archivedAt)
                .Set(board => board.UpdatedAt, at)
                .Set(board => board.UpdatedBy, MongoActorRef.From(by)),
            new FindOneAndUpdateOptions<MongoBoard> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return changed?.ToModel() ?? await BoardCollections.FindBoard(mongo, boardId, cancellationToken);
    }
}

internal sealed class ImportBoardDocumentStore(MongoDatabase mongo) : IImportBoardDocumentStore
{
    public Task<Board?> FindBoard(Guid boardId, CancellationToken cancellationToken) => BoardCollections.FindBoard(mongo, boardId, cancellationToken);

    public Task InsertNew(Board board, IReadOnlyList<Element> elements, IReadOnlyList<Connection> connections, CancellationToken cancellationToken) =>
        mongo.InTransaction(async (session, token) =>
        {
            await BoardCollections.BoardsIn(mongo).InsertOneAsync(session, MongoBoard.From(board), cancellationToken: token);
            await InsertContent(session, elements, connections, token);
            return true;
        }, cancellationToken);

    public Task<Board?> ReplaceContents(Guid boardId, string? newName, IReadOnlyList<Element> elements, IReadOnlyList<Connection> connections, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken) =>
        mongo.InTransaction(async (session, token) =>
        {
            await BoardCollections.ElementsIn(mongo).DeleteManyAsync(session, element => element.BoardId == boardId, cancellationToken: token);
            await BoardCollections.ConnectionsIn(mongo).DeleteManyAsync(session, connection => connection.BoardId == boardId, cancellationToken: token);
            await InsertContent(session, elements, connections, token);

            var update = Builders<MongoBoard>.Update
                .Inc(board => board.Revision, 1)
                .Set(board => board.ElementCount, elements.Count)
                .Set(board => board.UpdatedAt, at)
                .Set(board => board.UpdatedBy, MongoActorRef.From(by));
            if (newName is not null)
            {
                update = update.Set(board => board.Name, newName);
            }

            var board = await BoardCollections.BoardsIn(mongo).FindOneAndUpdateAsync<MongoBoard>(
                session,
                candidate => candidate.Id == boardId,
                update,
                new FindOneAndUpdateOptions<MongoBoard> { ReturnDocument = ReturnDocument.After },
                token);
            return board?.ToModel();
        }, cancellationToken);

    private async Task InsertContent(IClientSessionHandle session, IReadOnlyList<Element> elements, IReadOnlyList<Connection> connections, CancellationToken cancellationToken)
    {
        if (elements.Count > 0)
        {
            await BoardCollections.ElementsIn(mongo).InsertManyAsync(session, elements.Select(MongoElement.From), cancellationToken: cancellationToken);
        }

        if (connections.Count > 0)
        {
            await BoardCollections.ConnectionsIn(mongo).InsertManyAsync(session, connections.Select(MongoConnection.FromModel), cancellationToken: cancellationToken);
        }
    }
}
