using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Model;

public sealed record Position(double X, double Y);

public sealed record Size(double Width, double Height);

/// <summary>
/// Anything placed on a board - a sticky such as a Domain Event, or a structure such as a Swimlane.
/// <see cref="Type"/> is an id from the element-type registry, so the core never enumerates types.
/// <see cref="Version"/> increases on every change; clients keep whichever image of an element has the
/// highest version, which is what makes concurrent edits converge.
/// </summary>
public sealed record Element(
    Guid Id,
    Guid BoardId,
    string Type,
    string Text,
    double X,
    double Y,
    double Width,
    double Height,
    bool Pivotal,
    string? Color,
    long Version,
    DateTimeOffset CreatedAt,
    ActorRef CreatedBy,
    DateTimeOffset UpdatedAt,
    ActorRef UpdatedBy);

/// <summary>An optional arrow from one element to another.</summary>
public sealed record Connection(
    Guid Id,
    Guid BoardId,
    Guid From,
    Guid To,
    string? Label,
    long Version,
    DateTimeOffset CreatedAt,
    ActorRef CreatedBy);

/// <summary>Something that was deleted, and the version it had when it was.</summary>
public sealed record Removed(Guid Id, long Version);

/// <summary>
/// Everything one committed change did to a board's content, with full post-images of what was added
/// or changed. This is the published language Board Modelling offers the Collaboration context; it is
/// a message sent through a port, not an event raised inside the model.
/// </summary>
public sealed record BoardChangeSet(
    Guid BoardId,
    long Revision,
    string? OperationId,
    ActorRef Actor,
    IReadOnlyList<Element> Elements,
    IReadOnlyList<Removed> RemovedElements,
    IReadOnlyList<Connection> Connections,
    IReadOnlyList<Removed> RemovedConnections);
