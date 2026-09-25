using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.AddConnection;

/// <summary>Draws an arrow from one existing element to another. A client-chosen <see cref="Id"/> makes a retry harmless.</summary>
public sealed record AddConnectionCommand(Actor Actor, Guid BoardId, Guid From, Guid To, string? Label = null, Guid? Id = null, string? OperationId = null);

public sealed class AddConnectionResult : IUseCaseResult
{
    private AddConnectionResult(BoardChangeSet? changes, IReadOnlyList<Failure> failures)
    {
        Changes = changes;
        Failures = failures;
    }

    public BoardChangeSet? Changes { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static AddConnectionResult Succeeded(BoardChangeSet changes) => new(changes, []);

    public static AddConnectionResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static AddConnectionResult Failed(Failure failure) => new(null, [failure]);
}

public interface IAddConnectionCommandHandler
{
    Task<AddConnectionResult> Handle(AddConnectionCommand command, CancellationToken cancellationToken);
}

public sealed record ConnectionEndpoints(bool FromExists, bool ToExists, int ConnectionCount, Connection? Existing);

/// <param name="AlreadyExisted">True when an identical connection was already there, so nothing changed.</param>
public sealed record AddedConnection(long Revision, Connection Connection, bool AlreadyExisted);

public interface IAddConnectionStore
{
    /// <summary>Whether both elements exist, how many connections the board has, and any connection already joining the two.</summary>
    Task<ConnectionEndpoints> Endpoints(Guid boardId, Guid from, Guid to, CancellationToken cancellationToken);

    Task<AddedConnection> Insert(Connection connection, CancellationToken cancellationToken);
}
