using EventStorming.Persistence.MongoDb;
using EventStorming.Persistence.MongoDb.IntegrationTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.MongoDb;
using Xunit;

[assembly: AssemblyFixture(typeof(MongoContainer))]

namespace EventStorming.Persistence.MongoDb.IntegrationTests;

/// <summary>
/// One real MongoDB for the whole run - a single-node replica set, as in production, because the stores
/// use multi-document transactions. Each test gets its own database, so tests never see each other.
/// </summary>
public sealed class MongoContainer : IAsyncLifetime
{
    private readonly MongoDbContainer container = new MongoDbBuilder()
        .WithImage("mongo:8.0")
        .WithReplicaSet()
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    /// <summary>The adapter as the host registers it, on a fresh database, with its indexes in place.</summary>
    public async Task<ServiceProvider> NewStores()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventStormingMongoDb(new MongoOptions(ConnectionString, "test_" + Guid.NewGuid().ToString("N")));
        var provider = services.BuildServiceProvider();
        foreach (var hosted in provider.GetServices<IHostedService>())
        {
            await hosted.StartAsync(CancellationToken.None);
        }

        return provider;
    }
}
