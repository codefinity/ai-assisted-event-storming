using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.BoardModelling.Slices.ListElements;

public sealed class ListElementsQueryHandler(
    IValidator<ListElementsQuery> validator,
    IBoardAccess access,
    IElementTypeRegistry registry,
    IListElementsStore store) : IListElementsQueryHandler
{
    public async Task<ListElementsResult> Handle(ListElementsQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ListElementsResult.Failed(validation.ToFailures());
        }

        if (BoardRules.RequireView(await access.ForBoard(query.BoardId, query.Actor, cancellationToken)) is { } refused)
        {
            return ListElementsResult.Failed(refused);
        }

        if (query.Type is not null && registry.Find(query.Type) is null)
        {
            return ListElementsResult.Failed(BoardRules.UnknownType("type", query.Type, registry));
        }

        var page = await store.Page(query.BoardId, query.Type, query.Cursor, query.Limit ?? Paging.DefaultLimit, cancellationToken);
        return page is null
            ? ListElementsResult.Failed(Failures.Invalid("cursor", "invalid-cursor", "The cursor is not one this API issued.",
                "Omit 'cursor' to start from the first page, or pass the 'nextCursor' of the previous page unchanged."))
            : ListElementsResult.Succeeded(page);
    }
}

public sealed class ListElementsQueryValidator : AbstractValidator<ListElementsQuery>
{
    public ListElementsQueryValidator()
    {
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, Paging.MaxLimit).When(query => query.Limit is not null)
            .WithErrorCode("out-of-range").WithMessage($"The limit must be between 1 and {Paging.MaxLimit}.")
            .WithFix($"Send a limit from 1 to {Paging.MaxLimit}, or leave it out for {Paging.DefaultLimit}.");
    }
}
