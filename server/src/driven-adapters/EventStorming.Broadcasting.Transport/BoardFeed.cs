using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventStorming.Broadcasting.Transport;

/// <summary>
/// One message for everyone watching one board. <see cref="ExcludeConnectionId"/> keeps a
/// participant's own cursor and drag previews from being echoed back to them.
/// </summary>
public sealed record FeedItem(Guid BoardId, string Method, object Message, string? ExcludeConnectionId = null);

/// <summary>
/// The in-process hand-off between the driven broadcaster (which writes what the core announces) and
/// the driving SignalR relay (which delivers it). Neither side knows the other exists. Scaling out to
/// several API instances would mean replacing this with a shared backplane behind the same interface.
/// </summary>
public interface IBoardFeed
{
    void Publish(FeedItem item);

    IAsyncEnumerable<FeedItem> ReadAll(CancellationToken cancellationToken);
}

internal sealed class ChannelBoardFeed : IBoardFeed
{
    // Bounded so a stalled relay cannot exhaust memory. If it ever falls this far behind, dropping the
    // oldest messages is the lesser evil: clients converge again on their next resync.
    private readonly Channel<FeedItem> channel = Channel.CreateBounded<FeedItem>(new BoundedChannelOptions(100_000)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false,
    });

    public void Publish(FeedItem item) => channel.Writer.TryWrite(item);

    public IAsyncEnumerable<FeedItem> ReadAll(CancellationToken cancellationToken) => channel.Reader.ReadAllAsync(cancellationToken);
}

public static class BoardFeedServiceExtensions
{
    /// <summary>TryAdd: both the broadcaster and the relay call this, and they must share one feed.</summary>
    public static IServiceCollection AddEventStormingBoardFeed(this IServiceCollection services)
    {
        services.TryAddSingleton<IBoardFeed, ChannelBoardFeed>();
        return services;
    }
}
