using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.ListConnections;

public sealed record ListConnectionsQuery(Actor Actor, Guid BoardId, string? Cursor = null, int? Limit = null);

public sealed class ListConnectionsResult : IUseCaseResult
{
    private ListConnectionsResult(Page<Connection>? page, IReadOnlyList<Failure> failures)
    {
        Page = page;
        Failures = failures;
    }

    public Page<Connection>? Page { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static ListConnectionsResult Succeeded(Page<Connection> page) => new(page, []);

    public static ListConnectionsResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static ListConnectionsResult Failed(Failure failure) => new(null, [failure]);
}

public interface IListConnectionsQueryHandler
{
    Task<ListConnectionsResult> Handle(ListConnectionsQuery query, CancellationToken cancellationToken);
}

public interface IListConnectionsStore
{
    /// <summary>Null when <paramref name="cursor"/> is not a cursor this store issued.</summary>
    Task<Page<Connection>?> Page(Guid boardId, string? cursor, int limit, CancellationToken cancellationToken);
}
