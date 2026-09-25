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
using EventStorming.Persistence.MongoDb.Infrastructure;
using EventStorming.SharedKernel;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EventStorming.Persistence.MongoDb.BoardModelling;

internal sealed class AddElementsStore(MongoDatabase mongo) : IAddElementsStore
{
    public async Task<BoardContentStats> Stats(Guid boardId, CancellationToken cancellationToken)
    {
        var elementCount = await BoardCollections.BoardsIn(mongo).Find(board => board.Id == boardId).Project(board => board.ElementCount).FirstOrDefaultAsync(cancellationToken);
        var connectionCount = (int)await BoardCollections.ConnectionsIn(mongo).CountDocumentsAsync(connection => connection.BoardId == boardId, cancellationToken: cancellationToken);

        var bounds = await BoardCollections.ElementsIn(mongo)
            .Aggregate()
            .Match(element => element.BoardId == boardId)
            .Group(new BsonDocument
            {
                { "_id", BsonNull.Value },
                { "left", new BsonDocument("$min", "$x") },
                { "top", new BsonDocument("$min", "$y") },
                { "right", new BsonDocument("$max", new BsonDocument("$add", new BsonArray { "$x", "$width" })) },
                { "bottom", new BsonDocument("$max", new BsonDocument("$add", new BsonArray { "$y", "$height" })) },
            })
            .FirstOrDefaultAsync(cancellationToken);

        return bounds is null
            ? new BoardContentStats(elementCount, connectionCount, null, null, null, null)
            : new BoardContentStats(elementCount, connectionCount, bounds["left"].ToDouble(), bounds["top"].ToDouble(), bounds["right"].ToDouble(), bounds["bottom"].ToDouble());
    }

    public Task<IReadOnlyList<Element>> FindElements(Guid boardId, IReadOnlyCollection<Guid> elementIds, CancellationToken cancellationToken) =>
        BoardCollections.ElementsById(mongo, boardId, elementIds, cancellationToken);

    public Task<InsertedContent> Insert(Guid boardId, IReadOnlyList<Element> elements, IReadOnlyList<Connection> connections, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken) =>
        mongo.InTransaction(async (session, token) =>
        {
            var inserted = 0;
            if (elements.Count > 0)
            {
                // $setOnInsert keyed by id: an element that already exists (a retried request, an undo
                // replaying an add) is left exactly as it is.
                var result = await BoardCollections.ElementsIn(mongo).BulkWriteAsync(
                    session,
                    elements.Select(element => new UpdateOneModel<MongoElement>(
                        Builders<MongoElement>.Filter.Eq(candidate => candidate.Id, element.Id),
                        InsertOnly(MongoElement.From(element)))
                    { IsUpsert = true }),
                    cancellationToken: token);
                inserted = result.Upserts.Count;
            }

            var connectionsToInsert = await WithoutExisting(session, boardId, connections, token);
            if (connectionsToInsert.Count > 0)
            {
                await BoardCollections.ConnectionsIn(mongo).BulkWriteAsync(
                    session,
                    connectionsToInsert.Select(connection => new UpdateOneModel<MongoConnection>(
                        Builders<MongoConnection>.Filter.Eq(candidate => candidate.Id, connection.Id),
                        InsertOnly(MongoConnection.FromModel(connection)))
                    { IsUpsert = true }),
                    cancellationToken: token);
            }

            var revision = inserted > 0 || connectionsToInsert.Count > 0
                ? await BoardCollections.BumpRevision(mongo, boardId, by, at, inserted, token, session)
                : await BoardCollections.CurrentRevision(mongo, boardId, token);

            var elementIds = elements.Select(element => element.Id).ToList();
            var connectionIds = connections.Select(connection => connection.Id).ToList();
            var storedElements = await BoardCollections.ElementsById(mongo, boardId, elementIds, token, session);
            var storedConnections = await BoardCollections.ConnectionsIn(mongo)
                .Find(session, connection => connection.BoardId == boardId && connectionIds.Contains(connection.Id))
                .ToListAsync(token);

            return new InsertedContent(revision, storedElements, storedConnections.Select(connection => connection.ToModel()).ToList());
        }, cancellationToken);

    /// <summary>An upsert that writes the whole document if it is new and touches nothing if it already exists.</summary>
    private static UpdateDefinition<T> InsertOnly<T>(T document)
    {
        var fields = document.ToBsonDocument();
        fields.Remove("_id");
        return new BsonDocumentUpdateDefinition<T>(new BsonDocument("$setOnInsert", fields));
    }

    /// <summary>
    /// Drops connections joining two elements that are already joined: a write that broke the unique
    /// (board, from, to) index would abort the whole transaction.
    /// </summary>
    private async Task<IReadOnlyList<Connection>> WithoutExisting(IClientSessionHandle session, Guid boardId, IReadOnlyList<Connection> connections, CancellationToken cancellationToken)
    {
        if (connections.Count == 0)
        {
            return connections;
        }

        var fromIds = connections.Select(connection => connection.From).Distinct().ToList();
        var existing = await BoardCollections.ConnectionsIn(mongo)
            .Find(session, connection => connection.BoardId == boardId && fromIds.Contains(connection.From))
            .Project(connection => new { connection.Id, connection.From, connection.To })
            .ToListAsync(cancellationToken);

        var taken = existing.Select(connection => (connection.From, connection.To)).ToHashSet();
        var sameIds = existing.Select(connection => connection.Id).ToHashSet();
        return connections.Where(connection => !sameIds.Contains(connection.Id) && taken.Add((connection.From, connection.To))).ToList();
    }
}

internal sealed class UpdateElementStore(MongoDatabase mongo) : IUpdateElementStore, IGetElementStore
{
    public async Task<Element?> Find(Guid boardId, Guid elementId, CancellationToken cancellationToken)
    {
        var element = await BoardCollections.ElementsIn(mongo).Find(candidate => candidate.Id == elementId && candidate.BoardId == boardId).FirstOrDefaultAsync(cancellationToken);
        return element?.ToModel();
    }

    public async Task<UpdatedElement?> Update(Guid boardId, Guid elementId, ElementPatch patch, long? expectedVersion, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var filters = Builders<MongoElement>.Filter;
        var filter = filters.Eq(element => element.Id, elementId) & filters.Eq(element => element.BoardId, boardId);
        if (expectedVersion is { } version)
        {
            filter &= filters.Eq(element => element.Version, version);
        }

        // Only the fields in the patch are written, so a concurrent change to another field survives.
        var updates = new List<UpdateDefinition<MongoElement>>
        {
            Builders<MongoElement>.Update.Inc(element => element.Version, 1),
            Builders<MongoElement>.Update.Set(element => element.UpdatedAt, at),
            Builders<MongoElement>.Update.Set(element => element.UpdatedBy, MongoActorRef.From(by)),
        };
        if (patch.Text is not null)
        {
            updates.Add(Builders<MongoElement>.Update.Set(element => element.Text, patch.Text));
        }

        if (patch.Type is not null)
        {
            updates.Add(Builders<MongoElement>.Update.Set(element => element.Type, patch.Type));
        }

        if (patch.X is { } x)
        {
            updates.Add(Builders<MongoElement>.Update.Set(element => element.X, x));
        }

        if (patch.Y is { } y)
        {
            updates.Add(Builders<MongoElement>.Update.Set(element => element.Y, y));
        }

        if (patch.Width is { } width)
        {
            updates.Add(Builders<MongoElement>.Update.Set(element => element.Width, width));
        }

        if (patch.Height is { } height)
        {
            updates.Add(Builders<MongoElement>.Update.Set(element => element.Height, height));
        }

        if (patch.Pivotal is { } pivotal)
        {
            updates.Add(Builders<MongoElement>.Update.Set(element => element.Pivotal, pivotal));
        }

        if (patch.ClearColor)
        {
            updates.Add(Builders<MongoElement>.Update.Set(element => element.Color, null));
        }
        else if (patch.Color is not null)
        {
            updates.Add(Builders<MongoElement>.Update.Set(element => element.Color, patch.Color));
        }

        var updated = await BoardCollections.ElementsIn(mongo).FindOneAndUpdateAsync(
            filter,
            Builders<MongoElement>.Update.Combine(updates),
            new FindOneAndUpdateOptions<MongoElement> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (updated is null)
        {
            return null;
        }

        var revision = await BoardCollections.BumpRevision(mongo, boardId, by, at, 0, cancellationToken);
        return new UpdatedElement(revision, updated.ToModel());
    }
}

internal sealed class MoveElementsStore(MongoDatabase mongo) : IMoveElementsStore
{
    public async Task<MovedElements> Move(Guid boardId, IReadOnlyList<ElementMove> moves, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var stamp = MongoActorRef.From(by);
        var result = await BoardCollections.ElementsIn(mongo).BulkWriteAsync(
            moves.Select(move => new UpdateOneModel<MongoElement>(
                Builders<MongoElement>.Filter.Where(element => element.Id == move.ElementId && element.BoardId == boardId),
                Builders<MongoElement>.Update
                    .Set(element => element.X, move.Position.X)
                    .Set(element => element.Y, move.Position.Y)
                    .Inc(element => element.Version, 1)
                    .Set(element => element.UpdatedAt, at)
                    .Set(element => element.UpdatedBy, stamp))),
            new BulkWriteOptions { IsOrdered = false },
            cancellationToken);

        if (result.MatchedCount == 0)
        {
            return new MovedElements(await BoardCollections.CurrentRevision(mongo, boardId, cancellationToken), []);
        }

        var revision = await BoardCollections.BumpRevision(mongo, boardId, by, at, 0, cancellationToken);
        var moved = await BoardCollections.ElementsById(mongo, boardId, moves.Select(move => move.ElementId).ToList(), cancellationToken);
        return new MovedElements(revision, moved);
    }
}

internal sealed class DeleteElementsStore(MongoDatabase mongo) : IDeleteElementsStore
{
    public Task<DeletedContent> Delete(Guid boardId, IReadOnlyList<Guid> elementIds, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken) =>
        mongo.InTransaction(async (session, token) =>
        {
            var elements = BoardCollections.ElementsIn(mongo);
            var connections = BoardCollections.ConnectionsIn(mongo);

            var removedElements = await elements
                .Find(session, element => element.BoardId == boardId && elementIds.Contains(element.Id))
                .Project(element => new Removed(element.Id, element.Version))
                .ToListAsync(token);
            if (removedElements.Count == 0)
            {
                return new DeletedContent(await BoardCollections.CurrentRevision(mongo, boardId, token), [], []);
            }

            var ids = removedElements.Select(removed => removed.Id).ToList();
            var removedConnections = await connections
                .Find(session, connection => connection.BoardId == boardId && (ids.Contains(connection.From) || ids.Contains(connection.To)))
                .Project(connection => new Removed(connection.Id, connection.Version))
                .ToListAsync(token);

            await elements.DeleteManyAsync(session, element => element.BoardId == boardId && ids.Contains(element.Id), cancellationToken: token);
            if (removedConnections.Count > 0)
            {
                var connectionIds = removedConnections.Select(removed => removed.Id).ToList();
                await connections.DeleteManyAsync(session, connection => connectionIds.Contains(connection.Id), cancellationToken: token);
            }

            var revision = await BoardCollections.BumpRevision(mongo, boardId, by, at, -removedElements.Count, token, session);
            return new DeletedContent(revision, removedElements, removedConnections);
        }, cancellationToken);
}

internal sealed class AddConnectionStore(MongoDatabase mongo) : IAddConnectionStore
{
    public async Task<ConnectionEndpoints> Endpoints(Guid boardId, Guid from, Guid to, CancellationToken cancellationToken)
    {
        var found = await BoardCollections.ElementsIn(mongo)
            .Find(element => element.BoardId == boardId && (element.Id == from || element.Id == to))
            .Project(element => element.Id)
            .ToListAsync(cancellationToken);
        var connections = BoardCollections.ConnectionsIn(mongo);
        var count = (int)await connections.CountDocumentsAsync(connection => connection.BoardId == boardId, cancellationToken: cancellationToken);
        var existing = await connections.Find(connection => connection.BoardId == boardId && connection.From == from && connection.To == to).FirstOrDefaultAsync(cancellationToken);
        return new ConnectionEndpoints(found.Contains(from), found.Contains(to), count, existing?.ToModel());
    }

    public async Task<AddedConnection> Insert(Connection connection, CancellationToken cancellationToken)
    {
        try
        {
            await BoardCollections.ConnectionsIn(mongo).InsertOneAsync(MongoConnection.FromModel(connection), cancellationToken: cancellationToken);
        }
        catch (MongoException exception) when (MongoErrors.IsDuplicateKey(exception))
        {
            var existing = await BoardCollections.ConnectionsIn(mongo)
                .Find(candidate => candidate.Id == connection.Id || (candidate.BoardId == connection.BoardId && candidate.From == connection.From && candidate.To == connection.To))
                .FirstAsync(cancellationToken);
            return new AddedConnection(await BoardCollections.CurrentRevision(mongo, connection.BoardId, cancellationToken), existing.ToModel(), AlreadyExisted: true);
        }

        var revision = await BoardCollections.BumpRevision(mongo, connection.BoardId, connection.CreatedBy, connection.CreatedAt, 0, cancellationToken);
        return new AddedConnection(revision, connection, AlreadyExisted: false);
    }
}

internal sealed class DeleteConnectionsStore(MongoDatabase mongo) : IDeleteConnectionsStore
{
    public async Task<DeletedConnections> Delete(Guid boardId, IReadOnlyList<Guid> connectionIds, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var connections = BoardCollections.ConnectionsIn(mongo);
        var removed = await connections
            .Find(connection => connection.BoardId == boardId && connectionIds.Contains(connection.Id))
            .Project(connection => new Removed(connection.Id, connection.Version))
            .ToListAsync(cancellationToken);
        if (removed.Count == 0)
        {
            return new DeletedConnections(await BoardCollections.CurrentRevision(mongo, boardId, cancellationToken), []);
        }

        var ids = removed.Select(connection => connection.Id).ToList();
        await connections.DeleteManyAsync(connection => connection.BoardId == boardId && ids.Contains(connection.Id), cancellationToken);
        var revision = await BoardCollections.BumpRevision(mongo, boardId, by, at, 0, cancellationToken);
        return new DeletedConnections(revision, removed);
    }
}

internal sealed class ListElementsStore(MongoDatabase mongo) : IListElementsStore
{
    public async Task<Page<Element>?> Page(Guid boardId, string? type, string? cursor, int limit, CancellationToken cancellationToken)
    {
        var filters = Builders<MongoElement>.Filter;
        var filter = filters.Eq(element => element.BoardId, boardId);
        if (type is not null)
        {
            filter &= filters.Eq(element => element.Type, type);
        }

        if (cursor is not null)
        {
            var parts = Cursors.Decode(cursor, 1);
            if (parts is null || !Guid.TryParse(parts[0], out var lastId))
            {
                return null;
            }

            filter &= filters.Gt(element => element.Id, lastId);
        }

        var elements = await BoardCollections.ElementsIn(mongo).Find(filter).SortBy(element => element.Id).Limit(limit + 1).ToListAsync(cancellationToken);
        var page = elements.Take(limit).Select(element => element.ToModel()).ToList();
        return new Page<Element>(page, elements.Count > limit ? Cursors.Encode(page[^1].Id.ToString()) : null);
    }
}

internal sealed class ListConnectionsStore(MongoDatabase mongo) : IListConnectionsStore
{
    public async Task<Page<Connection>?> Page(Guid boardId, string? cursor, int limit, CancellationToken cancellationToken)
    {
        var filters = Builders<MongoConnection>.Filter;
        var filter = filters.Eq(connection => connection.BoardId, boardId);
        if (cursor is not null)
        {
            var parts = Cursors.Decode(cursor, 1);
            if (parts is null || !Guid.TryParse(parts[0], out var lastId))
            {
                return null;
            }

            filter &= filters.Gt(connection => connection.Id, lastId);
        }

        var connections = await BoardCollections.ConnectionsIn(mongo).Find(filter).SortBy(connection => connection.Id).Limit(limit + 1).ToListAsync(cancellationToken);
        var page = connections.Take(limit).Select(connection => connection.ToModel()).ToList();
        return new Page<Connection>(page, connections.Count > limit ? Cursors.Encode(page[^1].Id.ToString()) : null);
    }
}
