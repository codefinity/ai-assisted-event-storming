using System.Collections.Concurrent;
using EventStorming.Collaboration.Model;
using EventStorming.Collaboration.Slices.JoinBoard;
using EventStorming.Collaboration.Slices.LeaveBoard;
using EventStorming.Collaboration.Slices.MoveCursor;
using EventStorming.Collaboration.Slices.SetEditingFocus;
using EventStorming.Collaboration.Slices.ShareDragPreview;
using Microsoft.Extensions.DependencyInjection;

namespace EventStorming.Presence.InMemory;

/// <summary>
/// Who is on which board, for this process only. Presence is ephemeral by nature, so losing it on a
/// restart costs nothing: every client reconnects and joins again. Running several API instances
/// would mean swapping this for a shared store (e.g. Redis) behind the same ports.
/// </summary>
internal sealed class InMemoryPresence :
    IJoinBoardStore,
    ILeaveBoardStore,
    IMoveCursorStore,
    ISetEditingFocusStore,
    IShareDragPreviewStore
{
    private readonly ConcurrentDictionary<string, Participant> byConnection = new(StringComparer.Ordinal);

    public Task<Participant?> Leave(string connectionId, CancellationToken cancellationToken) =>
        Task.FromResult(byConnection.TryRemove(connectionId, out var participant) ? participant : null);

    public Task Add(Participant participant, CancellationToken cancellationToken)
    {
        byConnection[participant.ConnectionId] = participant;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Participant>> OnBoard(Guid boardId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Participant>>(byConnection.Values
            .Where(participant => participant.BoardId == boardId)
            .OrderBy(participant => participant.JoinedAt)
            .ToList());

    public Task<Participant?> Remove(string connectionId, CancellationToken cancellationToken) => Leave(connectionId, cancellationToken);

    public Task<Participant?> Find(string connectionId, CancellationToken cancellationToken) =>
        Task.FromResult(byConnection.GetValueOrDefault(connectionId));

    public Task<Participant?> SetEditing(string connectionId, Guid? elementId, CancellationToken cancellationToken)
    {
        while (byConnection.TryGetValue(connectionId, out var current))
        {
            var updated = current with { EditingElementId = elementId };
            if (byConnection.TryUpdate(connectionId, updated, current))
            {
                return Task.FromResult<Participant?>(updated);
            }
        }

        return Task.FromResult<Participant?>(null);
    }
}

public static class InMemoryPresenceServiceExtensions
{
    public static IServiceCollection AddEventStormingInMemoryPresence(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryPresence>();
        services.AddSingleton<IJoinBoardStore>(provider => provider.GetRequiredService<InMemoryPresence>());
        services.AddSingleton<ILeaveBoardStore>(provider => provider.GetRequiredService<InMemoryPresence>());
        services.AddSingleton<IMoveCursorStore>(provider => provider.GetRequiredService<InMemoryPresence>());
        services.AddSingleton<ISetEditingFocusStore>(provider => provider.GetRequiredService<InMemoryPresence>());
        services.AddSingleton<IShareDragPreviewStore>(provider => provider.GetRequiredService<InMemoryPresence>());
        return services;
    }
}
