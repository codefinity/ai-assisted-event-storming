using EventStorming.Broadcasting.Transport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EventStorming.Realtime.SignalR;

public static class RealtimeServiceExtensions
{
    public const string HubPath = "/hubs/board";

    public static IServiceCollection AddEventStormingRealtime(this IServiceCollection services)
    {
        services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 1024 * 1024;
            options.EnableDetailedErrors = false;
        });
        services.AddEventStormingBoardFeed();
        services.AddHostedService<BoardFeedRelay>();
        return services;
    }

    /// <summary>
    /// The host decides authorization and CORS for the hub. A connection is closed when its access token
    /// expires; the web app then reconnects with a fresh token.
    /// </summary>
    public static HubEndpointConventionBuilder MapEventStormingBoardHub(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapHub<BoardHub>(HubPath, options => options.CloseOnAuthenticationExpiration = true);
}
