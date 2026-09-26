using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.AddElements;

/// <summary>
/// One element to add. <see cref="Id"/> may be chosen by the client (the web app does, so a retry or an
/// undo re-adds the very same element); otherwise the server assigns one. Without a
/// <see cref="Position"/> the element is laid out automatically.
/// </summary>
public sealed record NewElement(
    string? Type,
    string? Text = null,
    Guid? Id = null,
    string? Key = null,
    Position? Position = null,
    Size? Size = null,
    string? Swimlane = null,
    string? Boundary = null,
    string? Anchor = null,
    bool Pivotal = false,
    string? Color = null);

/// <summary><see cref="From"/> and <see cref="To"/> are the key of a new element in the same request, or the id of an existing one.</summary>
public sealed record NewConnection(string? From, string? To, string? Label = null, Guid? Id = null);

/// <param name="OperationId">The client's id for this change, echoed back in the broadcast so it can recognise its own edit.</param>
public sealed record AddElementsCommand(
    Actor Actor,
    Guid BoardId,
    IReadOnlyList<NewElement> Elements,
    IReadOnlyList<NewConnection>? Connections = null,
    string? OperationId = null);

/// <param name="Created">The new elements, in the order they were requested.</param>
/// <param name="Grown">Swimlanes and boundaries already on the board that were resized to hold them.</param>
public sealed record AddedElements(BoardChangeSet Changes, IReadOnlyDictionary<string, Guid> KeyedIds, IReadOnlyList<Element> Created, IReadOnlyList<Element> Grown);

public sealed class AddElementsResult : IUseCaseResult
{
    private AddElementsResult(AddedElements? added, IReadOnlyList<Failure> failures)
    {
        Added = added;
        Failures = failures;
    }

    public AddedElements? Added { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static AddElementsResult Succeeded(AddedElements added) => new(added, []);

    public static AddElementsResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static AddElementsResult Failed(Failure failure) => new(null, [failure]);
}

public interface IAddElementsCommandHandler
{
    Task<AddElementsResult> Handle(AddElementsCommand command, CancellationToken cancellationToken);
}

/// <summary>How much is already on a board, and where. The bounds are null for an empty board.</summary>
/// <param name="ItemRight">The right edge of the rightmost element that is not a structure: where the timeline continues. Null when there is none.</param>
public sealed record BoardContentStats(int ElementCount, int ConnectionCount, double? Left, double? Top, double? Right, double? Bottom, double? ItemRight = null);

public sealed record InsertedContent(long Revision, IReadOnlyList<Element> Elements, IReadOnlyList<Connection> Connections);

public interface IAddElementsStore
{
    /// <param name="structureTypes">The type ids of structures (swimlanes, boundaries), which do not count towards <see cref="BoardContentStats.ItemRight"/>.</param>
    Task<BoardContentStats> Stats(Guid boardId, IReadOnlyCollection<string> structureTypes, CancellationToken cancellationToken);

    Task<IReadOnlyList<Element>> FindElements(Guid boardId, IReadOnlyCollection<Guid> elementIds, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the elements and connections in one transaction and returns what the board now holds for
    /// each of them. An id that already exists on the board is left untouched and returned as it is, so
    /// replaying the same request is harmless.
    /// </summary>
    /// <param name="grown">Existing elements to resize in the same transaction (the swimlanes and boundaries the new elements sit in); their versions go up and they are returned with the rest.</param>
    Task<InsertedContent> Insert(Guid boardId, IReadOnlyList<Element> elements, IReadOnlyList<Connection> connections, IReadOnlyList<Element> grown, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken);
}
