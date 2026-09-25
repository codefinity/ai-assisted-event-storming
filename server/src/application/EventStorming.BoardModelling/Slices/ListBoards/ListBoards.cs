using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.ListBoards;

/// <summary>A team's boards, most recently changed first.</summary>
public sealed record ListBoardsQuery(Actor Actor, Guid TeamId, bool IncludeArchived = false, string? Cursor = null, int? Limit = null);

public sealed class ListBoardsResult : IUseCaseResult
{
    private ListBoardsResult(Page<Board>? page, BoardPermission permission, IReadOnlyList<Failure> failures)
    {
        Page = page;
        Permission = permission;
        Failures = failures;
    }

    public Page<Board>? Page { get; }

    /// <summary>What the caller may do with the team's boards, so a dashboard can hide "New board" from Viewers.</summary>
    public BoardPermission Permission { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static ListBoardsResult Succeeded(Page<Board> page, BoardPermission permission) => new(page, permission, []);

    public static ListBoardsResult Failed(IReadOnlyList<Failure> failures) => new(null, BoardPermission.None, failures);

    public static ListBoardsResult Failed(Failure failure) => new(null, BoardPermission.None, [failure]);
}

public interface IListBoardsQueryHandler
{
    Task<ListBoardsResult> Handle(ListBoardsQuery query, CancellationToken cancellationToken);
}

public interface IListBoardsStore
{
    /// <summary>Null when <paramref name="cursor"/> is not a cursor this store issued.</summary>
    Task<Page<Board>?> Page(Guid teamId, bool includeArchived, string? cursor, int limit, CancellationToken cancellationToken);
}
