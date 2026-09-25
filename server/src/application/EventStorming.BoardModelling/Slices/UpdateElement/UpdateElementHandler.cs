using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.BoardModelling.Slices.UpdateElement;

public sealed class UpdateElementCommandHandler(
    IValidator<UpdateElementCommand> validator,
    IBoardAccess access,
    IElementTypeRegistry registry,
    IUpdateElementStore store,
    IBoardChangeBroadcaster broadcaster,
    IClock clock) : IUpdateElementCommandHandler
{
    public async Task<UpdateElementResult> Handle(UpdateElementCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return UpdateElementResult.Failed(validation.ToFailures());
        }

        if (BoardRules.RequireEdit(await access.ForBoard(command.BoardId, command.Actor, cancellationToken)) is { } refused)
        {
            return UpdateElementResult.Failed(refused);
        }

        var element = await store.Find(command.BoardId, command.ElementId, cancellationToken);
        if (element is null)
        {
            return UpdateElementResult.Failed(Failures.NotFound("The element", "It may have been deleted. List elements with GET /api/v1/boards/{boardId}/elements."));
        }

        if (command.ExpectedVersion is { } expected && expected != element.Version)
        {
            return UpdateElementResult.Failed(VersionConflict(element.Version));
        }

        var failures = new List<Failure>();
        var type = registry.Find(command.Type ?? element.Type);
        if (type is null)
        {
            return UpdateElementResult.Failed(BoardRules.UnknownType("type", command.Type ?? element.Type, registry));
        }

        var text = command.Text?.Trim();
        if ((text ?? element.Text).Length > type.MaxTextLength)
        {
            failures.Add(BoardRules.TextTooLong("text", type));
        }

        // Asking for a pivotal element of a type that cannot be one is an error; changing the type of a
        // pivotal element to such a type quietly drops the emphasis instead.
        bool? pivotal = command.Pivotal;
        if (command.Pivotal == true && !type.CanBePivotal)
        {
            failures.Add(BoardRules.NotPivotal("pivotal", type));
        }
        else if (command.Pivotal is null && element.Pivotal && !type.CanBePivotal)
        {
            pivotal = false;
        }

        if (failures.Count > 0)
        {
            return UpdateElementResult.Failed(failures);
        }

        var patch = new ElementPatch(
            text,
            command.Type is null ? null : type.Id,
            command.Position?.X,
            command.Position?.Y,
            command.Size?.Width,
            command.Size?.Height,
            pivotal,
            string.IsNullOrEmpty(command.Color) ? null : command.Color,
            ClearColor: command.Color is { Length: 0 });

        var updated = await store.Update(command.BoardId, command.ElementId, patch, command.ExpectedVersion, command.Actor.Ref, clock.UtcNow, cancellationToken);
        if (updated is null)
        {
            var current = await store.Find(command.BoardId, command.ElementId, cancellationToken);
            return UpdateElementResult.Failed(current is null ? Failures.NotFound("The element") : VersionConflict(current.Version));
        }

        var changes = new BoardChangeSet(command.BoardId, updated.Revision, command.OperationId, command.Actor.Ref, [updated.Element], [], [], []);
        broadcaster.ContentChanged(changes);
        return UpdateElementResult.Succeeded(changes);
    }

    private static Failure VersionConflict(long current) =>
        Failures.Conflict("version-conflict", $"The element has changed since it was read; it is now at version {current}.", "expectedVersion",
            $"Read the element again and resend with expectedVersion {current}, or leave expectedVersion out to overwrite.");
}

public sealed class UpdateElementCommandValidator : AbstractValidator<UpdateElementCommand>
{
    public UpdateElementCommandValidator()
    {
        RuleFor(command => command)
            .Must(command => command.Text is not null || command.Type is not null || command.Position is not null
                             || command.Size is not null || command.Pivotal is not null || command.Color is not null)
            .OverridePropertyName("text")
            .WithErrorCode("nothing-to-change").WithMessage("The request changes nothing.")
            .WithFix("Send at least one of: text, type, position, size, pivotal, color.");

        RuleFor(command => command.Text)
            .MaximumLength(BoardLimits.MaxTextLength).WithErrorCode("too-long")
            .WithMessage($"Text must be at most {BoardLimits.MaxTextLength} characters.");

        RuleFor(command => command.Type)
            .Must(type => type is null || !string.IsNullOrWhiteSpace(type)).WithErrorCode("required")
            .WithMessage("The type cannot be empty.").WithFix("Leave 'type' out to keep it, or send a type id such as \"command\".");

        RuleFor(command => command.Position!).SetValidator(new PositionValidator()).When(command => command.Position is not null);
        RuleFor(command => command.Size!).SetValidator(new SizeValidator()).When(command => command.Size is not null);

        RuleFor(command => command.Color)
            .Must(BoardRules.IsColor).WithErrorCode("invalid-color").WithMessage("A color must look like #RRGGBB.")
            .WithFix("Send a hex color such as \"#FFA94D\", or an empty string to go back to the type's color.");

        RuleFor(command => command.OperationId)
            .MaximumLength(BoardLimits.MaxOperationIdLength).WithErrorCode("too-long")
            .WithMessage($"An operation id must be at most {BoardLimits.MaxOperationIdLength} characters.");
    }
}
