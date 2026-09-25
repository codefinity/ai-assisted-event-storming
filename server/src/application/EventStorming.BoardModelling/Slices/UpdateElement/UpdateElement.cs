using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.UpdateElement;

/// <summary>
/// Changes some fields of one element; every null field is left as it is. Only the fields sent are
/// written, so a concurrent change to a different field is never overwritten. With
/// <see cref="ExpectedVersion"/> the change is refused if the element has changed since it was read.
/// An empty <see cref="Color"/> clears a color override.
/// </summary>
public sealed record UpdateElementCommand(
    Actor Actor,
    Guid BoardId,
    Guid ElementId,
    string? Text = null,
    string? Type = null,
    Position? Position = null,
    Size? Size = null,
    bool? Pivotal = null,
    string? Color = null,
    long? ExpectedVersion = null,
    string? OperationId = null);

public sealed class UpdateElementResult : IUseCaseResult
{
    private UpdateElementResult(BoardChangeSet? changes, IReadOnlyList<Failure> failures)
    {
        Changes = changes;
        Failures = failures;
    }

    public BoardChangeSet? Changes { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static UpdateElementResult Succeeded(BoardChangeSet changes) => new(changes, []);

    public static UpdateElementResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static UpdateElementResult Failed(Failure failure) => new(null, [failure]);
}

public interface IUpdateElementCommandHandler
{
    Task<UpdateElementResult> Handle(UpdateElementCommand command, CancellationToken cancellationToken);
}

/// <summary>The fields to write; null means "leave alone". <see cref="ClearColor"/> removes a color override.</summary>
public sealed record ElementPatch(string? Text, string? Type, double? X, double? Y, double? Width, double? Height, bool? Pivotal, string? Color, bool ClearColor);

public sealed record UpdatedElement(long Revision, Element Element);

public interface IUpdateElementStore
{
    Task<Element?> Find(Guid boardId, Guid elementId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the patch and increments the element's version. Returns null if the element no longer
    /// exists or, when <paramref name="expectedVersion"/> is given, if its version has moved on.
    /// </summary>
    Task<UpdatedElement?> Update(Guid boardId, Guid elementId, ElementPatch patch, long? expectedVersion, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken);
}
