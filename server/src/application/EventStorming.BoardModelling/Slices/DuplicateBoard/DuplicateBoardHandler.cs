using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.BoardModelling.Slices.DuplicateBoard;

public sealed class DuplicateBoardCommandHandler(
    IValidator<DuplicateBoardCommand> validator,
    IBoardAccess access,
    IDuplicateBoardStore store,
    IClock clock) : IDuplicateBoardCommandHandler
{
    public async Task<DuplicateBoardResult> Handle(DuplicateBoardCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return DuplicateBoardResult.Failed(validation.ToFailures());
        }

        var granted = await access.ForBoard(command.BoardId, command.Actor, cancellationToken);
        if (BoardRules.RequireView(granted) is { } hidden)
        {
            return DuplicateBoardResult.Failed(hidden);
        }

        var source = await store.FindBoard(command.BoardId, cancellationToken);
        if (source is null)
        {
            return DuplicateBoardResult.Failed(Failures.NotFound("The board"));
        }

        // The copy is a new board in the same team, so it needs the right to create boards there.
        if (BoardRules.RequireTeamEdit(await access.ForTeam(source.TeamId, command.Actor, cancellationToken)) is { } refused)
        {
            return DuplicateBoardResult.Failed(refused);
        }

        var now = clock.UtcNow;
        var name = string.IsNullOrWhiteSpace(command.Name) ? CopyName(source.Name) : command.Name.Trim();
        var copy = source with
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Revision = 0,
            CreatedAt = now,
            CreatedBy = command.Actor.Ref,
            UpdatedAt = now,
            UpdatedBy = command.Actor.Ref,
            ArchivedAt = null,
        };

        return DuplicateBoardResult.Succeeded(await store.Duplicate(source.Id, copy, cancellationToken));
    }

    private static string CopyName(string name)
    {
        var copy = "Copy of " + name;
        return copy.Length <= BoardLimits.MaxBoardNameLength ? copy : copy[..BoardLimits.MaxBoardNameLength];
    }
}

public sealed class DuplicateBoardCommandValidator : AbstractValidator<DuplicateBoardCommand>
{
    public DuplicateBoardCommandValidator()
    {
        RuleFor(command => command.Name)
            .MaximumLength(BoardLimits.MaxBoardNameLength).WithErrorCode("too-long")
            .WithMessage($"A board name must be at most {BoardLimits.MaxBoardNameLength} characters.");
    }
}
