using EventStorming.Collaboration.Model;
using EventStorming.SharedKernel;

namespace EventStorming.Collaboration.Slices.LeaveBoard;

/// <summary>A connection stops watching its board - explicitly, or because it disconnected.</summary>
public sealed record LeaveBoardCommand(string ConnectionId);

public sealed class LeaveBoardResult : IUseCaseResult
{
    private LeaveBoardResult(Participant? left) => Left = left;

    /// <summary>Who left, or null if the connection was not on a board.</summary>
    public Participant? Left { get; }

    public IReadOnlyList<Failure> Failures => [];

    public bool Success => true;

    public static LeaveBoardResult Succeeded(Participant? left) => new(left);
}

public interface ILeaveBoardCommandHandler
{
    Task<LeaveBoardResult> Handle(LeaveBoardCommand command, CancellationToken cancellationToken);
}

public interface ILeaveBoardStore
{
    Task<Participant?> Remove(string connectionId, CancellationToken cancellationToken);
}
