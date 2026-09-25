using EventStorming.Broadcasting.Transport;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventStorming.Realtime.SignalR;

/// <summary>
/// Delivers every message on the board feed to the SignalR group of its board. It is the only reader
/// of the feed, so messages for one board reach its clients in the order they were published.
/// </summary>
internal sealed class BoardFeedRelay(IBoardFeed feed, IHubContext<BoardHub> hub, ILogger<BoardFeedRelay> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var item in feed.ReadAll(stoppingToken))
            {
                try
                {
                    var group = BoardHub.Group(item.BoardId);
                    var clients = item.ExcludeConnectionId is null
                        ? hub.Clients.Group(group)
                        : hub.Clients.GroupExcept(group, item.ExcludeConnectionId);
                    await clients.SendAsync(item.Method, item.Message, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(exception, "Could not deliver {Method} for board {BoardId}.", item.Method, item.BoardId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }
}
