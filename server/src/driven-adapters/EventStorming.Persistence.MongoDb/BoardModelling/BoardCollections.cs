using EventStorming.BoardModelling.Model;
using EventStorming.Persistence.MongoDb.Infrastructure;
using EventStorming.SharedKernel;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace EventStorming.Persistence.MongoDb.BoardModelling;

internal sealed class MongoBoard
{
    [BsonId]
    public Guid Id { get; init; }

    public Guid TeamId { get; init; }

    public required string Name { get; init; }

    /// <summary>Stored in its wire form ("big-picture"), so the database reads like the API.</summary>
    public required string Level { get; init; }

    public long Revision { get; init; }

    public int ElementCount { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public required MongoActorRef CreatedBy { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public required MongoActorRef UpdatedBy { get; init; }

    public DateTimeOffset? ArchivedAt { get; init; }

    public Board ToModel() => new(
        Id, TeamId, Name, BoardLevels.Parse(Level), Revision, ElementCount,
        CreatedAt, CreatedBy.ToModel(), UpdatedAt, UpdatedBy.ToModel(), ArchivedAt);

    public static MongoBoard From(Board board) => new()
    {
        Id = board.Id,
        TeamId = board.TeamId,
        Name = board.Name,
        Level = BoardLevels.Name(board.Level),
        Revision = board.Revision,
        ElementCount = board.ElementCount,
        CreatedAt = board.CreatedAt,
        CreatedBy = MongoActorRef.From(board.CreatedBy),
        UpdatedAt = board.UpdatedAt,
        UpdatedBy = MongoActorRef.From(board.UpdatedBy),
        ArchivedAt = board.ArchivedAt,
    };
}

internal sealed class MongoElement
{
    [BsonId]
    public Guid Id { get; init; }

    public Guid BoardId { get; init; }

    public required string Type { get; init; }

    public required string Text { get; init; }

    public double X { get; init; }

    public double Y { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public bool Pivotal { get; init; }

    public string? Color { get; init; }

    public long Version { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public required MongoActorRef CreatedBy { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public required MongoActorRef UpdatedBy { get; init; }

    public Element ToModel() => new(
        Id, BoardId, Type, Text, X, Y, Width, Height, Pivotal, Color, Version,
        CreatedAt, CreatedBy.ToModel(), UpdatedAt, UpdatedBy.ToModel());

    public static MongoElement From(Element element) => new()
    {
        Id = element.Id,
        BoardId = element.BoardId,
        Type = element.Type,
        Text = element.Text,
        X = element.X,
        Y = element.Y,
        Width = element.Width,
        Height = element.Height,
        Pivotal = element.Pivotal,
        Color = element.Color,
        Version = element.Version,
        CreatedAt = element.CreatedAt,
        CreatedBy = MongoActorRef.From(element.CreatedBy),
        UpdatedAt = element.UpdatedAt,
        UpdatedBy = MongoActorRef.From(element.UpdatedBy),
    };
}

internal sealed class MongoConnection
{
    [BsonId]
    public Guid Id { get; init; }

    public Guid BoardId { get; init; }

    public Guid From { get; init; }

    public Guid To { get; init; }

    public string? Label { get; init; }

    public long Version { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public required MongoActorRef CreatedBy { get; init; }

    public Connection ToModel() => new(Id, BoardId, From, To, Label, Version, CreatedAt, CreatedBy.ToModel());

    public static MongoConnection FromModel(Connection connection) => new()
    {
        Id = connection.Id,
        BoardId = connection.BoardId,
        From = connection.From,
        To = connection.To,
        Label = connection.Label,
        Version = connection.Version,
        CreatedAt = connection.CreatedAt,
        CreatedBy = MongoActorRef.From(connection.CreatedBy),
    };
}

internal static class BoardCollections
{
    public const string Boards = "boards";
    public const string Elements = "elements";
    public const string Connections = "connections";

    public static IMongoCollection<MongoBoard> BoardsIn(MongoDatabase mongo) => mongo.Collection<MongoBoard>(Boards);

    public static IMongoCollection<MongoElement> ElementsIn(MongoDatabase mongo) => mongo.Collection<MongoElement>(Elements);

    public static IMongoCollection<MongoConnection> ConnectionsIn(MongoDatabase mongo) => mongo.Collection<MongoConnection>(Connections);

    public static async Task EnsureIndexes(MongoDatabase mongo, CancellationToken cancellationToken)
    {
        await BoardsIn(mongo).Indexes.CreateOneAsync(
            new CreateIndexModel<MongoBoard>(
                Builders<MongoBoard>.IndexKeys
                    .Ascending(board => board.TeamId)
                    .Ascending(board => board.ArchivedAt)
                    .Descending(board => board.UpdatedAt)
                    .Descending(board => board.Id),
                new CreateIndexOptions { Name = "team_dashboard" }),
            cancellationToken: cancellationToken);

        await ElementsIn(mongo).Indexes.CreateManyAsync(
            [
                new CreateIndexModel<MongoElement>(
                    Builders<MongoElement>.IndexKeys.Ascending(element => element.BoardId).Ascending(element => element.Id),
                    new CreateIndexOptions { Name = "board" }),
                new CreateIndexModel<MongoElement>(
                    Builders<MongoElement>.IndexKeys.Ascending(element => element.BoardId).Ascending(element => element.Type).Ascending(element => element.Id),
                    new CreateIndexOptions { Name = "board_type" }),
            ],
            cancellationToken);

        await ConnectionsIn(mongo).Indexes.CreateManyAsync(
            [
                new CreateIndexModel<MongoConnection>(
                    Builders<MongoConnection>.IndexKeys.Ascending(connection => connection.BoardId).Ascending(connection => connection.Id),
                    new CreateIndexOptions { Name = "board" }),
                new CreateIndexModel<MongoConnection>(
                    Builders<MongoConnection>.IndexKeys
                        .Ascending(connection => connection.BoardId)
                        .Ascending(connection => connection.From)
                        .Ascending(connection => connection.To),
                    new CreateIndexOptions { Unique = true, Name = "board_endpoints_unique" }),
                new CreateIndexModel<MongoConnection>(
                    Builders<MongoConnection>.IndexKeys.Ascending(connection => connection.BoardId).Ascending(connection => connection.To),
                    new CreateIndexOptions { Name = "board_to" }),
            ],
            cancellationToken);
    }

    public static async Task<Board?> FindBoard(MongoDatabase mongo, Guid boardId, CancellationToken cancellationToken)
    {
        var board = await BoardsIn(mongo).Find(candidate => candidate.Id == boardId).FirstOrDefaultAsync(cancellationToken);
        return board?.ToModel();
    }

    public static async Task<IReadOnlyList<Element>> ElementsOf(MongoDatabase mongo, Guid boardId, CancellationToken cancellationToken)
    {
        var elements = await ElementsIn(mongo).Find(element => element.BoardId == boardId).ToListAsync(cancellationToken);
        return elements.Select(element => element.ToModel()).ToList();
    }

    public static async Task<IReadOnlyList<Connection>> ConnectionsOf(MongoDatabase mongo, Guid boardId, CancellationToken cancellationToken)
    {
        var connections = await ConnectionsIn(mongo).Find(connection => connection.BoardId == boardId).ToListAsync(cancellationToken);
        return connections.Select(connection => connection.ToModel()).ToList();
    }

    public static async Task<IReadOnlyList<Element>> ElementsById(MongoDatabase mongo, Guid boardId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken, IClientSessionHandle? session = null)
    {
        var filter = Builders<MongoElement>.Filter.Where(element => element.BoardId == boardId && ids.Contains(element.Id));
        var found = session is null
            ? await ElementsIn(mongo).Find(filter).ToListAsync(cancellationToken)
            : await ElementsIn(mongo).Find(session, filter).ToListAsync(cancellationToken);
        return found.Select(element => element.ToModel()).ToList();
    }

    /// <summary>
    /// Increments the board's revision (and optionally its element count) and stamps who changed it.
    /// Every content change goes through here, which is what keeps the revision a reliable watermark.
    /// </summary>
    public static async Task<long> BumpRevision(MongoDatabase mongo, Guid boardId, ActorRef by, DateTimeOffset at, int elementCountDelta, CancellationToken cancellationToken, IClientSessionHandle? session = null)
    {
        var update = Builders<MongoBoard>.Update
            .Inc(board => board.Revision, 1)
            .Inc(board => board.ElementCount, elementCountDelta)
            .Set(board => board.UpdatedAt, at)
            .Set(board => board.UpdatedBy, MongoActorRef.From(by));
        var options = new FindOneAndUpdateOptions<MongoBoard> { ReturnDocument = ReturnDocument.After };

        var board = session is null
            ? await BoardsIn(mongo).FindOneAndUpdateAsync<MongoBoard>(candidate => candidate.Id == boardId, update, options, cancellationToken)
            : await BoardsIn(mongo).FindOneAndUpdateAsync<MongoBoard>(session, candidate => candidate.Id == boardId, update, options, cancellationToken);
        return board?.Revision ?? 0;
    }

    public static async Task<long> CurrentRevision(MongoDatabase mongo, Guid boardId, CancellationToken cancellationToken) =>
        await BoardsIn(mongo)
            .Find(board => board.Id == boardId)
            .Project(board => board.Revision)
            .FirstOrDefaultAsync(cancellationToken);
}
