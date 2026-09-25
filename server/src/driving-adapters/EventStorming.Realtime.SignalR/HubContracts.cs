using EventStorming.Broadcasting.Transport;
using EventStorming.SharedKernel;

namespace EventStorming.Realtime.SignalR;

// What the web app sends to the board hub, and what each call returns. Every editing call carries the
// client's operationId, which comes back in the broadcast so the sender can recognise its own change.

public sealed record NewElementWire(Guid? Id, string? Type, string? Text, double? X, double? Y, double? Width, double? Height, bool? Pivotal, string? Color);

public sealed record NewConnectionWire(Guid? Id, string? From, string? To, string? Label);

public sealed record AddElementsRequest(Guid BoardId, string? OperationId, IReadOnlyList<NewElementWire>? Elements, IReadOnlyList<NewConnectionWire>? Connections);

public sealed record UpdateElementRequest(
    Guid BoardId,
    string? OperationId,
    Guid ElementId,
    string? Text,
    string? Type,
    double? X,
    double? Y,
    double? Width,
    double? Height,
    bool? Pivotal,
    string? Color);

public sealed record ElementMoveWire(Guid ElementId, double X, double Y);

public sealed record MoveElementsRequest(Guid BoardId, string? OperationId, IReadOnlyList<ElementMoveWire>? Moves);

public sealed record DeleteElementsRequest(Guid BoardId, string? OperationId, IReadOnlyList<Guid>? ElementIds);

public sealed record AddConnectionRequest(Guid BoardId, string? OperationId, Guid? Id, Guid From, Guid To, string? Label);

public sealed record DeleteConnectionsRequest(Guid BoardId, string? OperationId, IReadOnlyList<Guid>? ConnectionIds);

public sealed record FailureWire(string Kind, string Code, string Message, string? Field, string? Fix)
{
    public static FailureWire From(Failure failure) =>
        new(failure.Kind.ToString().ToLowerInvariant(), failure.Code, failure.Message, failure.Field, failure.Fix);
}

/// <summary>The answer to an editing call. The change itself arrives separately, as a broadcast to the whole board.</summary>
public sealed record OperationResult(bool Ok, string? OperationId, long Revision, IReadOnlyList<FailureWire> Failures)
{
    public static OperationResult From(IUseCaseResult result, string? operationId, long revision) =>
        new(result.Success, operationId, revision, result.Failures.Select(FailureWire.From).ToList());
}

public sealed record JoinBoardResponse(bool Ok, ParticipantWire? You, IReadOnlyList<ParticipantWire> Participants, IReadOnlyList<FailureWire> Failures);
