using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.BoardModelling.Slices.ListConnections;

public sealed class ListConnectionsQueryHandler(
    IValidator<ListConnectionsQuery> validator,
    IBoardAccess access,
    IListConnectionsStore store) : IListConnectionsQueryHandler
{
    public async Task<ListConnectionsResult> Handle(ListConnectionsQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ListConnectionsResult.Failed(validation.ToFailures());
        }

        if (BoardRules.RequireView(await access.ForBoard(query.BoardId, query.Actor, cancellationToken)) is { } refused)
        {
            return ListConnectionsResult.Failed(refused);
        }

        var page = await store.Page(query.BoardId, query.Cursor, query.Limit ?? Paging.DefaultLimit, cancellationToken);
        return page is null
            ? ListConnectionsResult.Failed(Failures.Invalid("cursor", "invalid-cursor", "The cursor is not one this API issued.",
                "Omit 'cursor' to start from the first page, or pass the 'nextCursor' of the previous page unchanged."))
            : ListConnectionsResult.Succeeded(page);
    }
}

public sealed class ListConnectionsQueryValidator : AbstractValidator<ListConnectionsQuery>
{
    public ListConnectionsQueryValidator()
    {
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, Paging.MaxLimit).When(query => query.Limit is not null)
            .WithErrorCode("out-of-range").WithMessage($"The limit must be between 1 and {Paging.MaxLimit}.")
            .WithFix($"Send a limit from 1 to {Paging.MaxLimit}, or leave it out for {Paging.DefaultLimit}.");
    }
}
