using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using EventStorming.Host.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using ModelContextProtocol.Client;
using Shouldly;
using Testcontainers.MongoDb;
using Xunit;

[assembly: AssemblyFixture(typeof(MongoServer))]

namespace EventStorming.Host.IntegrationTests.Support;

public sealed class MongoServer : IAsyncLifetime
{
    private readonly MongoDbContainer container = new MongoDbBuilder("mongo:8.0").WithReplicaSet().Build();

    public string ConnectionString => container.GetConnectionString();

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public async ValueTask DisposeAsync() => await container.DisposeAsync();
}

/// <summary>
/// The real host - every adapter, the real pipeline - in memory, against a real MongoDB with a database of
/// its own. Password hashing is cheapened and the sign-in limit raised, so tests stay fast.
/// </summary>
public sealed class ApiHost(string connectionString, int publicApiPermitsPerMinute = 10_000) : WebApplicationFactory<Program>
{
    public const string WebOrigin = "http://localhost:3000";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Mongo:ConnectionString", connectionString);
        builder.UseSetting("Mongo:Database", "host_" + Guid.NewGuid().ToString("N"));
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        builder.UseSetting("Security:PasswordHashIterations", "1000");
        builder.UseSetting("RestApi:SignInPermitsPerMinute", "10000");
        builder.UseSetting("RestApi:PublicApiPermitsPerMinute", publicApiPermitsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Cors:AllowedOrigins:0", WebOrigin);
        builder.UseSetting("Smtp:Host", "127.0.0.1");
        builder.UseSetting("Smtp:Port", "9");
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    /// <summary>A signed-up person, with a client that carries their access token and refresh cookie.</summary>
    public async Task<Person> SignUp(string name)
    {
        var client = Browser();
        var response = await client.PostAsJsonAsync("/api/app/auth/sign-up", new
        {
            email = $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com",
            displayName = name,
            password = "correct horse battery",
        });
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var session = (await response.Content.ReadFromJsonAsync<JsonElement>(Json));
        var token = session.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Person(name, client, token, session.GetProperty("account").GetProperty("id").GetGuid());
    }

    public HubConnection Hub(Person person) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, "hubs/board"), options =>
            {
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(person.Token);
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

    public HttpClient ApiKeyClient(string key)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return client;
    }

    /// <summary>An agent connected to the MCP endpoint with an API key, through the official MCP client.</summary>
    public async Task<McpClient> Agent(string key)
    {
        var http = CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress!, "mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {key}" },
            },
            http,
            ownsHttpClient: true);
        return await McpClient.CreateAsync(transport);
    }
}

public sealed record Person(string Name, HttpClient Client, string Token, Guid AccountId)
{
    public async Task<JsonElement> Post(string path, object body, System.Net.HttpStatusCode expected = System.Net.HttpStatusCode.Created)
    {
        var response = await Client.PostAsJsonAsync(path, body);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, text);
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    public async Task<JsonElement> Get(string path)
    {
        var response = await Client.GetAsync(path);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public async Task<Guid> CreateTeam(string name) => (await Post("/api/app/teams", new { name })).GetProperty("id").GetGuid();

    public async Task<Guid> CreateBoard(Guid teamId, string name, string level = "big-picture") =>
        (await Post($"/api/app/teams/{teamId}/boards", new { name, level })).GetProperty("id").GetGuid();

    public async Task<string> CreateApiKey(Guid teamId, params string[] scopes) =>
        (await Post($"/api/app/teams/{teamId}/api-keys", new { name = "Test key", scopes })).GetProperty("key").GetString()!;
}

/// <summary>Collects messages a hub connection receives, so a test can wait for the one it expects.</summary>
public sealed class Inbox
{
    private readonly List<(string Method, JsonElement Message)> received = [];
    private readonly SemaphoreSlim arrived = new(0);

    public Inbox Listen(HubConnection connection, params string[] methods)
    {
        foreach (var method in methods)
        {
            connection.On<JsonElement>(method, message =>
            {
                lock (received)
                {
                    received.Add((method, message));
                }

                arrived.Release();
            });
        }

        return this;
    }

    public async Task<JsonElement> WaitFor(string method, Func<JsonElement, bool>? match = null, int timeoutMs = 10_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            lock (received)
            {
                foreach (var item in received.Where(item => item.Method == method))
                {
                    if (match is null || match(item.Message))
                    {
                        return item.Message;
                    }
                }
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero || !await arrived.WaitAsync(remaining))
            {
                throw new TimeoutException($"No '{method}' message arrived in time.");
            }
        }
    }

    public bool Got(string method)
    {
        lock (received)
        {
            return received.Any(item => item.Method == method);
        }
    }
}
