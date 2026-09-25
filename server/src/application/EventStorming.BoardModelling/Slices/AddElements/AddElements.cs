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

public sealed record AddedElements(BoardChangeSet Changes, IReadOnlyDictionary<string, Guid> KeyedIds);

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
public sealed record BoardContentStats(int ElementCount, int ConnectionCount, double? Left, double? Top, double? Right, double? Bottom);

public sealed record InsertedContent(long Revision, IReadOnlyList<Element> Elements, IReadOnlyList<Connection> Connections);

public interface IAddElementsStore
{
    Task<BoardContentStats> Stats(Guid boardId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Element>> FindElements(Guid boardId, IReadOnlyCollection<Guid> elementIds, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the elements and connections in one transaction and returns what the board now holds for
    /// each of them. An id that already exists on the board is left untouched and returned as it is, so
    /// replaying the same request is harmless.
    /// </summary>
    Task<InsertedContent> Insert(Guid boardId, IReadOnlyList<Element> elements, IReadOnlyList<Connection> connections, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken);
}
