using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.BoardModelling.Slices.MoveElements;

public sealed class MoveElementsCommandHandler(
    IValidator<MoveElementsCommand> validator,
    IBoardAccess access,
    IMoveElementsStore store,
    IBoardChangeBroadcaster broadcaster,
    IClock clock) : IMoveElementsCommandHandler
{
    public async Task<MoveElementsResult> Handle(MoveElementsCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return MoveElementsResult.Failed(validation.ToFailures());
        }

        if (BoardRules.RequireEdit(await access.ForBoard(command.BoardId, command.Actor, cancellationToken)) is { } refused)
        {
            return MoveElementsResult.Failed(refused);
        }

        var moved = await store.Move(command.BoardId, command.Moves, command.Actor.Ref, clock.UtcNow, cancellationToken);
        var changes = new BoardChangeSet(command.BoardId, moved.Revision, command.OperationId, command.Actor.Ref, moved.Elements, [], [], []);
        if (moved.Elements.Count > 0)
        {
            broadcaster.ContentChanged(changes);
        }

        return MoveElementsResult.Succeeded(changes);
    }
}

public sealed class MoveElementsCommandValidator : AbstractValidator<MoveElementsCommand>
{
    public MoveElementsCommandValidator()
    {
        RuleFor(command => command.Moves)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("required").WithMessage("Send at least one move.")
            .Must(moves => moves.Count <= BoardLimits.MaxElements).WithErrorCode("too-many")
            .WithMessage($"At most {BoardLimits.MaxElements} elements can be moved at once.")
            .Must(moves => moves.Select(move => move.ElementId).Distinct().Count() == moves.Count)
            .WithErrorCode("duplicate-id").WithMessage("The same element is moved twice in one request.")
            .WithFix("Send each element once, with its final position.");

        RuleForEach(command => command.Moves).ChildRules(move =>
            move.RuleFor(item => item.Position).NotNull().WithErrorCode("required").WithMessage("Every move needs a position.")
                .SetValidator(new PositionValidator()));

        RuleFor(command => command.OperationId)
            .MaximumLength(BoardLimits.MaxOperationIdLength).WithErrorCode("too-long")
            .WithMessage($"An operation id must be at most {BoardLimits.MaxOperationIdLength} characters.");
    }
}
