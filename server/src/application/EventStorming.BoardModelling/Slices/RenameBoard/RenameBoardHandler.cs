using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.BoardModelling.Slices.RenameBoard;

public sealed class RenameBoardCommandHandler(
    IValidator<RenameBoardCommand> validator,
    IBoardAccess access,
    IRenameBoardStore store,
    IBoardChangeBroadcaster broadcaster,
    IClock clock) : IRenameBoardCommandHandler
{
    public async Task<RenameBoardResult> Handle(RenameBoardCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return RenameBoardResult.Failed(validation.ToFailures());
        }

        if (BoardRules.RequireEdit(await access.ForBoard(command.BoardId, command.Actor, cancellationToken)) is { } refused)
        {
            return RenameBoardResult.Failed(refused);
        }

        var board = await store.Rename(command.BoardId, command.Name.Trim(), command.Actor.Ref, clock.UtcNow, cancellationToken);
        if (board is null)
        {
            return RenameBoardResult.Failed(Failures.NotFound("The board"));
        }

        broadcaster.DetailsChanged(board);
        return RenameBoardResult.Succeeded(board);
    }
}

public sealed class RenameBoardCommandValidator : AbstractValidator<RenameBoardCommand>
{
    public RenameBoardCommandValidator()
    {
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode("required").WithMessage("A board needs a name.")
            .MaximumLength(BoardLimits.MaxBoardNameLength).WithErrorCode("too-long")
            .WithMessage($"A board name must be at most {BoardLimits.MaxBoardNameLength} characters.");
    }
}
