using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.DeleteElements;

/// <summary>Deletes elements and every connection attached to them. Ids that no longer exist are ignored, so a retry is harmless.</summary>
public sealed record DeleteElementsCommand(Actor Actor, Guid BoardId, IReadOnlyList<Guid> ElementIds, string? OperationId = null);

public sealed class DeleteElementsResult : IUseCaseResult
{
    private DeleteElementsResult(BoardChangeSet? changes, IReadOnlyList<Failure> failures)
    {
        Changes = changes;
        Failures = failures;
    }

    public BoardChangeSet? Changes { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static DeleteElementsResult Succeeded(BoardChangeSet changes) => new(changes, []);

    public static DeleteElementsResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static DeleteElementsResult Failed(Failure failure) => new(null, [failure]);
}

public interface IDeleteElementsCommandHandler
{
    Task<DeleteElementsResult> Handle(DeleteElementsCommand command, CancellationToken cancellationToken);
}

public sealed record DeletedContent(long Revision, IReadOnlyList<Removed> Elements, IReadOnlyList<Removed> Connections);

public interface IDeleteElementsStore
{
    Task<DeletedContent> Delete(Guid boardId, IReadOnlyList<Guid> elementIds, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken);
}
