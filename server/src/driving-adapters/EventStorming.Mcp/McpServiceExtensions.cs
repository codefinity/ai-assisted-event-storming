using System.Reflection;
using EventStorming.Mcp.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace EventStorming.Mcp;

public static class McpServiceExtensions
{
    public const string Path = "/mcp";

    /// <summary>
    /// An MCP server (Streamable HTTP, stateless) whose tools draw and read EventStorming boards through
    /// Board Modelling's use cases. Whatever an agent draws is validated, laid out and broadcast like
    /// any other change, so it appears live on open boards.
    /// </summary>
    public static IServiceCollection AddEventStormingMcp(this IServiceCollection services, McpAdapterOptions options)
    {
        services.AddSingleton(options);
        services
            .AddMcpServer(server =>
            {
                server.ServerInfo = new Implementation { Name = "eventstorming", Title = "EventStorming", Version = "1.0.0" };
                server.ServerInstructions = ServerInstructions.Text;
            })
            .WithHttpTransport(transport => transport.Stateless = true)
            .WithResources<ModellingResources>()
            .WithPrompts<ModellingPrompts>(McpJson.Options);

        // Registered one by one rather than with WithTools<BoardTools>, which cannot take schema options.
        // Creating each tool with the service provider keeps the injected use case handlers out of its schema.
        foreach (var method in typeof(BoardTools).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                     .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null))
        {
            services.AddSingleton(provider => McpServerTool.Create(
                method,
                context => ActivatorUtilities.CreateInstance<BoardTools>(context.Services!),
                new McpServerToolCreateOptions { Services = provider, SerializerOptions = McpJson.Options, SchemaCreateOptions = McpJson.Schema }));
        }

        return services;
    }

    /// <summary>The host decides authentication, authorization and rate limits for the endpoint.</summary>
    public static IEndpointConventionBuilder MapEventStormingMcp(this IEndpointRouteBuilder endpoints) => endpoints.MapMcp(Path);
}
