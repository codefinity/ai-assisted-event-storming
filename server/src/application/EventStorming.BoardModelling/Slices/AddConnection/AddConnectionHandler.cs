using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.BoardModelling.Slices.AddConnection;

public sealed class AddConnectionCommandHandler(
    IValidator<AddConnectionCommand> validator,
    IBoardAccess access,
    IAddConnectionStore store,
    IBoardChangeBroadcaster broadcaster,
    IClock clock) : IAddConnectionCommandHandler
{
    public async Task<AddConnectionResult> Handle(AddConnectionCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return AddConnectionResult.Failed(validation.ToFailures());
        }

        if (BoardRules.RequireEdit(await access.ForBoard(command.BoardId, command.Actor, cancellationToken)) is { } refused)
        {
            return AddConnectionResult.Failed(refused);
        }

        var endpoints = await store.Endpoints(command.BoardId, command.From, command.To, cancellationToken);
        var failures = new List<Failure>();
        if (!endpoints.FromExists)
        {
            failures.Add(Failures.Invalid("from", "unknown-element", $"No element with id '{command.From}' is on this board.", "Use the id of an element on this board."));
        }

        if (!endpoints.ToExists)
        {
            failures.Add(Failures.Invalid("to", "unknown-element", $"No element with id '{command.To}' is on this board.", "Use the id of an element on this board."));
        }

        if (failures.Count > 0)
        {
            return AddConnectionResult.Failed(failures);
        }

        if (endpoints.Existing is { } existing)
        {
            return existing.Id == command.Id || command.Id is null
                ? AddConnectionResult.Succeeded(new BoardChangeSet(command.BoardId, 0, command.OperationId, command.Actor.Ref, [], [], [existing], []))
                : AddConnectionResult.Failed(Failures.Conflict("connection-exists", "These two elements are already connected.", "to",
                    $"Use the existing connection {existing.Id}, or delete it first."));
        }

        if (endpoints.ConnectionCount >= BoardLimits.MaxConnections)
        {
            return AddConnectionResult.Failed(new Failure(FailureKind.LimitExceeded, "board-limit-exceeded",
                $"A board holds at most {BoardLimits.MaxConnections} connections.", Fix: "Delete connections that are no longer needed."));
        }

        var now = clock.UtcNow;
        var connection = new Connection(
            command.Id ?? Guid.CreateVersion7(),
            command.BoardId,
            command.From,
            command.To,
            string.IsNullOrWhiteSpace(command.Label) ? null : command.Label.Trim(),
            Version: 1,
            now,
            command.Actor.Ref);

        var added = await store.Insert(connection, cancellationToken);
        var changes = new BoardChangeSet(command.BoardId, added.Revision, command.OperationId, command.Actor.Ref, [], [], [added.Connection], []);
        if (!added.AlreadyExisted)
        {
            broadcaster.ContentChanged(changes);
        }

        return AddConnectionResult.Succeeded(changes);
    }
}

public sealed class AddConnectionCommandValidator : AbstractValidator<AddConnectionCommand>
{
    public AddConnectionCommandValidator()
    {
        RuleFor(command => command.From).NotEmpty().WithErrorCode("required").WithMessage("A connection needs a 'from' element id.");
        RuleFor(command => command.To).NotEmpty().WithErrorCode("required").WithMessage("A connection needs a 'to' element id.");

        RuleFor(command => command.To)
            .NotEqual(command => command.From).When(command => command.From != Guid.Empty)
            .WithErrorCode("self-connection").WithMessage("A connection must join two different elements.")
            .WithFix("Point 'to' at a different element than 'from'.");

        RuleFor(command => command.Label)
            .MaximumLength(BoardLimits.MaxLabelLength).WithErrorCode("too-long")
            .WithMessage($"A label must be at most {BoardLimits.MaxLabelLength} characters.");

        RuleFor(command => command.OperationId)
            .MaximumLength(BoardLimits.MaxOperationIdLength).WithErrorCode("too-long")
            .WithMessage($"An operation id must be at most {BoardLimits.MaxOperationIdLength} characters.");
    }
}
