using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.BoardModelling.Slices.DeleteConnections;

public sealed class DeleteConnectionsCommandHandler(
    IValidator<DeleteConnectionsCommand> validator,
    IBoardAccess access,
    IDeleteConnectionsStore store,
    IBoardChangeBroadcaster broadcaster,
    IClock clock) : IDeleteConnectionsCommandHandler
{
    public async Task<DeleteConnectionsResult> Handle(DeleteConnectionsCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return DeleteConnectionsResult.Failed(validation.ToFailures());
        }

        if (BoardRules.RequireEdit(await access.ForBoard(command.BoardId, command.Actor, cancellationToken)) is { } refused)
        {
            return DeleteConnectionsResult.Failed(refused);
        }

        var deleted = await store.Delete(command.BoardId, command.ConnectionIds.Distinct().ToList(), command.Actor.Ref, clock.UtcNow, cancellationToken);
        var changes = new BoardChangeSet(command.BoardId, deleted.Revision, command.OperationId, command.Actor.Ref, [], [], [], deleted.Connections);
        if (deleted.Connections.Count > 0)
        {
            broadcaster.ContentChanged(changes);
        }

        return DeleteConnectionsResult.Succeeded(changes);
    }
}

public sealed class DeleteConnectionsCommandValidator : AbstractValidator<DeleteConnectionsCommand>
{
    public DeleteConnectionsCommandValidator()
    {
        RuleFor(command => command.ConnectionIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("required").WithMessage("Send the ids of the connections to delete.")
            .Must(ids => ids.Count <= BoardLimits.MaxConnections).WithErrorCode("too-many")
            .WithMessage($"At most {BoardLimits.MaxConnections} connections can be deleted at once.");

        RuleFor(command => command.OperationId)
            .MaximumLength(BoardLimits.MaxOperationIdLength).WithErrorCode("too-long")
            .WithMessage($"An operation id must be at most {BoardLimits.MaxOperationIdLength} characters.");
    }
}
