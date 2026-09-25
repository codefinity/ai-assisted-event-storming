using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.BoardModelling.Slices.CreateBoard;

public sealed class CreateBoardCommandHandler(
    IValidator<CreateBoardCommand> validator,
    IBoardAccess access,
    ICreateBoardStore store,
    IClock clock) : ICreateBoardCommandHandler
{
    public async Task<CreateBoardResult> Handle(CreateBoardCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return CreateBoardResult.Failed(validation.ToFailures());
        }

        if (BoardRules.RequireTeamEdit(await access.ForTeam(command.TeamId, command.Actor, cancellationToken)) is { } refused)
        {
            return CreateBoardResult.Failed(refused);
        }

        var now = clock.UtcNow;
        var board = new Board(
            Guid.CreateVersion7(),
            command.TeamId,
            command.Name.Trim(),
            BoardLevels.Parse(command.Level),
            Revision: 0,
            ElementCount: 0,
            now,
            command.Actor.Ref,
            now,
            command.Actor.Ref,
            ArchivedAt: null);

        await store.Insert(board, cancellationToken);
        return CreateBoardResult.Succeeded(board);
    }
}

public sealed class CreateBoardCommandValidator : AbstractValidator<CreateBoardCommand>
{
    public CreateBoardCommandValidator()
    {
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode("required").WithMessage("A board needs a name.")
            .WithFix("Name it after the domain or process being explored, e.g. \"Online food ordering\".")
            .MaximumLength(BoardLimits.MaxBoardNameLength).WithErrorCode("too-long")
            .WithMessage($"A board name must be at most {BoardLimits.MaxBoardNameLength} characters.");

        RuleFor(command => command.Level)
            .Must(BoardLevels.IsValid).WithErrorCode("unknown-level")
            .WithMessage("The level must be big-picture, process-modelling or software-design.")
            .WithFix("Use \"big-picture\" to explore a whole domain, \"process-modelling\" for one process, or \"software-design\" to design aggregates.");
    }
}
