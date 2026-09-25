using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.BoardModelling.Slices.ListBoards;

public sealed class ListBoardsQueryHandler(
    IValidator<ListBoardsQuery> validator,
    IBoardAccess access,
    IListBoardsStore store) : IListBoardsQueryHandler
{
    public async Task<ListBoardsResult> Handle(ListBoardsQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ListBoardsResult.Failed(validation.ToFailures());
        }

        var permission = await access.ForTeam(query.TeamId, query.Actor, cancellationToken);
        if (permission == BoardPermission.None)
        {
            return ListBoardsResult.Failed(Failures.NotFound("The team"));
        }

        var page = await store.Page(query.TeamId, query.IncludeArchived, query.Cursor, query.Limit ?? Paging.DefaultLimit, cancellationToken);
        return page is null
            ? ListBoardsResult.Failed(Failures.Invalid("cursor", "invalid-cursor", "The cursor is not one this API issued.",
                "Omit 'cursor' to start from the first page, or pass the 'nextCursor' of the previous page unchanged."))
            : ListBoardsResult.Succeeded(page, permission);
    }
}

public sealed class ListBoardsQueryValidator : AbstractValidator<ListBoardsQuery>
{
    public ListBoardsQueryValidator()
    {
        RuleFor(query => query.Limit)
            .InclusiveBetween(1, Paging.MaxLimit).When(query => query.Limit is not null)
            .WithErrorCode("out-of-range").WithMessage($"The limit must be between 1 and {Paging.MaxLimit}.")
            .WithFix($"Send a limit from 1 to {Paging.MaxLimit}, or leave it out for {Paging.DefaultLimit}.");
    }
}
