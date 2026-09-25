using MongoDB.Driver;

namespace EventStorming.Persistence.MongoDb.Infrastructure;

/// <summary>
/// The one handle every store in this adapter goes through: the typed collections and a way to run a
/// unit of work in a transaction. Collection names live here and nowhere else.
/// </summary>
internal sealed class MongoDatabase(IMongoClient client, MongoOptions options)
{
    private readonly IMongoDatabase database = client.GetDatabase(options.Database);

    public IMongoDatabase Database => database;

    public IMongoCollection<T> Collection<T>(string name) => database.GetCollection<T>(name);

    /// <summary>
    /// Runs <paramref name="work"/> in a multi-document transaction, retrying the driver's
    /// transient transaction errors.
    /// </summary>
    public async Task<T> InTransaction<T>(Func<IClientSessionHandle, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
        return await session.WithTransactionAsync(
            (handle, token) => work(handle, token),
            cancellationToken: cancellationToken);
    }
}

internal static class MongoErrors
{
    public static bool IsDuplicateKey(MongoException exception) => exception switch
    {
        MongoWriteException write => write.WriteError?.Category == ServerErrorCategory.DuplicateKey,
        MongoBulkWriteException bulk => bulk.WriteErrors.Any(error => error.Category == ServerErrorCategory.DuplicateKey),
        MongoCommandException command => command.Code == 11000,
        _ => false,
    };
}
