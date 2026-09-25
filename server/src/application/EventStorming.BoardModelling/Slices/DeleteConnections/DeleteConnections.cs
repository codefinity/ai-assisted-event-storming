using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.DeleteConnections;

public sealed record DeleteConnectionsCommand(Actor Actor, Guid BoardId, IReadOnlyList<Guid> ConnectionIds, string? OperationId = null);

public sealed class DeleteConnectionsResult : IUseCaseResult
{
    private DeleteConnectionsResult(BoardChangeSet? changes, IReadOnlyList<Failure> failures)
    {
        Changes = changes;
        Failures = failures;
    }

    public BoardChangeSet? Changes { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static DeleteConnectionsResult Succeeded(BoardChangeSet changes) => new(changes, []);

    public static DeleteConnectionsResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static DeleteConnectionsResult Failed(Failure failure) => new(null, [failure]);
}

public interface IDeleteConnectionsCommandHandler
{
    Task<DeleteConnectionsResult> Handle(DeleteConnectionsCommand command, CancellationToken cancellationToken);
}

public sealed record DeletedConnections(long Revision, IReadOnlyList<Removed> Connections);

public interface IDeleteConnectionsStore
{
    Task<DeletedConnections> Delete(Guid boardId, IReadOnlyList<Guid> connectionIds, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken);
}
