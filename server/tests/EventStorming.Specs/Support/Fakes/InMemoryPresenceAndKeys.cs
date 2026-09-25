using EventStorming.Collaboration.Model;
using EventStorming.Collaboration.Slices.JoinBoard;
using EventStorming.Collaboration.Slices.LeaveBoard;
using EventStorming.Collaboration.Slices.MoveCursor;
using EventStorming.Collaboration.Slices.SetEditingFocus;
using EventStorming.Collaboration.Slices.ShareDragPreview;
using EventStorming.PublicIntegration.Model;
using EventStorming.PublicIntegration.Slices.AuthenticateApiKey;
using EventStorming.PublicIntegration.Slices.CreateApiKey;
using EventStorming.PublicIntegration.Slices.ListApiKeys;
using EventStorming.PublicIntegration.Slices.RecordIdempotentResponse;
using EventStorming.PublicIntegration.Slices.ReserveIdempotencyKey;
using EventStorming.PublicIntegration.Slices.RevokeApiKey;

namespace EventStorming.Specs.Support.Fakes;

public sealed class InMemoryPresence : IJoinBoardStore, ILeaveBoardStore, IMoveCursorStore, ISetEditingFocusStore, IShareDragPreviewStore
{
    public Dictionary<string, Participant> Participants { get; } = new(StringComparer.Ordinal);

    public Task<Participant?> Leave(string connectionId, CancellationToken cancellationToken) => Remove(connectionId, cancellationToken);

    public Task Add(Participant participant, CancellationToken cancellationToken)
    {
        Participants[participant.ConnectionId] = participant;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Participant>> OnBoard(Guid boardId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Participant>>(Participants.Values.Where(participant => participant.BoardId == boardId).ToList());

    public Task<Participant?> Remove(string connectionId, CancellationToken cancellationToken) =>
        Task.FromResult(Participants.Remove(connectionId, out var participant) ? participant : null);

    public Task<Participant?> Find(string connectionId, CancellationToken cancellationToken) =>
        Task.FromResult(Participants.GetValueOrDefault(connectionId));

    public Task<Participant?> SetEditing(string connectionId, Guid? elementId, CancellationToken cancellationToken)
    {
        if (!Participants.TryGetValue(connectionId, out var participant))
        {
            return Task.FromResult<Participant?>(null);
        }

        Participants[connectionId] = participant with { EditingElementId = elementId };
        return Task.FromResult<Participant?>(Participants[connectionId]);
    }
}

public sealed class InMemoryPublicIntegration :
    ICreateApiKeyStore,
    IListApiKeysStore,
    IRevokeApiKeyStore,
    IAuthenticateApiKeyStore,
    IReserveIdempotencyKeyStore,
    IRecordIdempotentResponseStore
{
    public List<ApiKey> Keys { get; } = [];

    public Dictionary<string, IdempotencyRecord> Idempotency { get; } = new(StringComparer.Ordinal);

    public Task Insert(ApiKey key, CancellationToken cancellationToken)
    {
        Keys.Add(key);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ApiKey>> KeysOf(Guid teamId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ApiKey>>(Keys.Where(key => key.TeamId == teamId).ToList());

    public Task<ApiKey?> Revoke(Guid teamId, Guid keyId, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        var index = Keys.FindIndex(key => key.Id == keyId && key.TeamId == teamId);
        if (index < 0)
        {
            return Task.FromResult<ApiKey?>(null);
        }

        Keys[index] = Keys[index] with { RevokedAt = Keys[index].RevokedAt ?? revokedAt };
        return Task.FromResult<ApiKey?>(Keys[index]);
    }

    public Task<ApiKey?> Find(Guid keyId, CancellationToken cancellationToken) => Task.FromResult(Keys.FirstOrDefault(key => key.Id == keyId));

    public Task TouchLastUsed(Guid keyId, DateTimeOffset usedAt, CancellationToken cancellationToken)
    {
        var index = Keys.FindIndex(key => key.Id == keyId);
        Keys[index] = Keys[index] with { LastUsedAt = usedAt };
        return Task.CompletedTask;
    }

    public Task<IdempotencyRecord?> TryInsert(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        if (Idempotency.TryGetValue(record.Id, out var existing))
        {
            return Task.FromResult<IdempotencyRecord?>(existing);
        }

        Idempotency[record.Id] = record;
        return Task.FromResult<IdempotencyRecord?>(null);
    }

    public Task Replace(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        Idempotency[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task Complete(string recordId, StoredResponse response, CancellationToken cancellationToken)
    {
        Idempotency[recordId] = Idempotency[recordId] with { Completed = true, Response = response };
        return Task.CompletedTask;
    }

    public Task Release(string recordId, CancellationToken cancellationToken)
    {
        Idempotency.Remove(recordId);
        return Task.CompletedTask;
    }
}
