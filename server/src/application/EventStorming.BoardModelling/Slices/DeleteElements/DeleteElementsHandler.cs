using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.BoardModelling.Slices.DeleteElements;

public sealed class DeleteElementsCommandHandler(
    IValidator<DeleteElementsCommand> validator,
    IBoardAccess access,
    IDeleteElementsStore store,
    IBoardChangeBroadcaster broadcaster,
    IClock clock) : IDeleteElementsCommandHandler
{
    public async Task<DeleteElementsResult> Handle(DeleteElementsCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return DeleteElementsResult.Failed(validation.ToFailures());
        }

        if (BoardRules.RequireEdit(await access.ForBoard(command.BoardId, command.Actor, cancellationToken)) is { } refused)
        {
            return DeleteElementsResult.Failed(refused);
        }

        var deleted = await store.Delete(command.BoardId, command.ElementIds.Distinct().ToList(), command.Actor.Ref, clock.UtcNow, cancellationToken);
        var changes = new BoardChangeSet(command.BoardId, deleted.Revision, command.OperationId, command.Actor.Ref, [], deleted.Elements, [], deleted.Connections);
        if (deleted.Elements.Count > 0)
        {
            broadcaster.ContentChanged(changes);
        }

        return DeleteElementsResult.Succeeded(changes);
    }
}

public sealed class DeleteElementsCommandValidator : AbstractValidator<DeleteElementsCommand>
{
    public DeleteElementsCommandValidator()
    {
        RuleFor(command => command.ElementIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("required").WithMessage("Send the ids of the elements to delete.")
            .Must(ids => ids.Count <= BoardLimits.MaxElements).WithErrorCode("too-many")
            .WithMessage($"At most {BoardLimits.MaxElements} elements can be deleted at once.");

        RuleFor(command => command.OperationId)
            .MaximumLength(BoardLimits.MaxOperationIdLength).WithErrorCode("too-long")
            .WithMessage($"An operation id must be at most {BoardLimits.MaxOperationIdLength} characters.");
    }
}
