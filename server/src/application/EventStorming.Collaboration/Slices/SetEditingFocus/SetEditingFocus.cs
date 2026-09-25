using EventStorming.Collaboration.Model;
using EventStorming.SharedKernel;

namespace EventStorming.Collaboration.Slices.SetEditingFocus;

/// <summary>
/// "I am editing this element" (or, with a null <see cref="ElementId"/>, "I stopped"). Others see who
/// is typing where. It is advisory, never a lock: two people can still edit the same element.
/// </summary>
public sealed record SetEditingFocusCommand(string ConnectionId, Guid BoardId, Guid? ElementId);

public sealed class SetEditingFocusResult : IUseCaseResult
{
    private SetEditingFocusResult(IReadOnlyList<Failure> failures) => Failures = failures;

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static SetEditingFocusResult Succeeded() => new([]);

    public static SetEditingFocusResult Failed(Failure failure) => new([failure]);
}

public interface ISetEditingFocusCommandHandler
{
    Task<SetEditingFocusResult> Handle(SetEditingFocusCommand command, CancellationToken cancellationToken);
}

public interface ISetEditingFocusStore
{
    /// <summary>Records the focus and returns the updated participant, or null if the connection is on no board.</summary>
    Task<Participant?> SetEditing(string connectionId, Guid? elementId, CancellationToken cancellationToken);
}
