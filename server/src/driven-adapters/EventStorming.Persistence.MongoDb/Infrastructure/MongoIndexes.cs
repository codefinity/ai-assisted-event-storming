using EventStorming.Persistence.MongoDb.BoardModelling;
using EventStorming.Persistence.MongoDb.Identity;
using EventStorming.Persistence.MongoDb.PublicIntegration;
using EventStorming.Persistence.MongoDb.Teams;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventStorming.Persistence.MongoDb.Infrastructure;

/// <summary>
/// Creates every collection's indexes at startup. Index creation is idempotent, and hosted services
/// start before the server accepts requests, so no request ever runs against a collection without
/// its unique constraints.
/// </summary>
internal sealed class MongoIndexes(MongoDatabase mongo, ILogger<MongoIndexes> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await EnsureAll(mongo, cancellationToken);
        logger.LogInformation("MongoDB indexes are in place on database {Database}.", mongo.Database.DatabaseNamespace.DatabaseName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public static async Task EnsureAll(MongoDatabase mongo, CancellationToken cancellationToken)
    {
        await IdentityCollections.EnsureIndexes(mongo, cancellationToken);
        await TeamsCollections.EnsureIndexes(mongo, cancellationToken);
        await BoardCollections.EnsureIndexes(mongo, cancellationToken);
        await PublicIntegrationCollections.EnsureIndexes(mongo, cancellationToken);
    }
}
