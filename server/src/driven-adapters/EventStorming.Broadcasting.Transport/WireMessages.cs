namespace EventStorming.Broadcasting.Transport;

// The messages a board's open clients receive, exactly as they go over the wire (SignalR serializes
// them as camelCase JSON). They are shared by the broadcaster that writes them and the SignalR relay
// that delivers them, and know nothing of the core - mapping into them is the broadcaster's job.

/// <summary>The names of the client methods these messages are delivered to.</summary>
public static class ClientMethods
{
    public const string BoardChanged = "boardChanged";
    public const string BoardReplaced = "boardReplaced";
    public const string BoardDetailsChanged = "boardDetailsChanged";
    public const string ParticipantJoined = "participantJoined";
    public const string ParticipantLeft = "participantLeft";
    public const string CursorMoved = "cursorMoved";
    public const string EditingFocusChanged = "editingFocusChanged";
    public const string DragPreviewed = "dragPreviewed";
}

public sealed record ActorWire(string Kind, Guid Id, string Name);

public sealed record ElementWire(
    Guid Id,
    string Type,
    string Text,
    double X,
    double Y,
    double Width,
    double Height,
    bool Pivotal,
    string? Color,
    long Version,
    DateTimeOffset UpdatedAt,
    ActorWire UpdatedBy);

public sealed record ConnectionWire(Guid Id, Guid From, Guid To, string? Label, long Version);

public sealed record RemovedWire(Guid Id, long Version);

/// <summary>A committed change: full post-images of what was added or changed, and what was removed.</summary>
public sealed record BoardChangedMessage(
    Guid BoardId,
    long Revision,
    string? OperationId,
    ActorWire Actor,
    IReadOnlyList<ElementWire> Elements,
    IReadOnlyList<RemovedWire> RemovedElements,
    IReadOnlyList<ConnectionWire> Connections,
    IReadOnlyList<RemovedWire> RemovedConnections);

/// <summary>The whole content was replaced: reload it.</summary>
public sealed record BoardReplacedMessage(Guid BoardId, long Revision, ActorWire Actor);

public sealed record BoardDetailsMessage(Guid BoardId, string Name, DateTimeOffset? ArchivedAt);

public sealed record ParticipantWire(string ConnectionId, Guid AccountId, string DisplayName, string Color, Guid? EditingElementId);

public sealed record ParticipantJoinedMessage(Guid BoardId, ParticipantWire Participant);

public sealed record ParticipantLeftMessage(Guid BoardId, string ConnectionId, Guid AccountId);

public sealed record CursorMovedMessage(Guid BoardId, string ConnectionId, double X, double Y);

public sealed record EditingFocusMessage(Guid BoardId, string ConnectionId, Guid? ElementId);

public sealed record DragMoveWire(Guid ElementId, double X, double Y);

public sealed record DragPreviewMessage(Guid BoardId, string ConnectionId, IReadOnlyList<DragMoveWire> Moves);
