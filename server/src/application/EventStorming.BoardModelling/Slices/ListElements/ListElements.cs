using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.ListElements;

/// <summary>A board's elements in creation order, optionally only those of one type.</summary>
public sealed record ListElementsQuery(Actor Actor, Guid BoardId, string? Type = null, string? Cursor = null, int? Limit = null);

public sealed class ListElementsResult : IUseCaseResult
{
    private ListElementsResult(Page<Element>? page, IReadOnlyList<Failure> failures)
    {
        Page = page;
        Failures = failures;
    }

    public Page<Element>? Page { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static ListElementsResult Succeeded(Page<Element> page) => new(page, []);

    public static ListElementsResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static ListElementsResult Failed(Failure failure) => new(null, [failure]);
}

public interface IListElementsQueryHandler
{
    Task<ListElementsResult> Handle(ListElementsQuery query, CancellationToken cancellationToken);
}

public interface IListElementsStore
{
    /// <summary>Null when <paramref name="cursor"/> is not a cursor this store issued.</summary>
    Task<Page<Element>?> Page(Guid boardId, string? type, string? cursor, int limit, CancellationToken cancellationToken);
}
