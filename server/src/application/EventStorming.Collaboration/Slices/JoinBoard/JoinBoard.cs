using EventStorming.Collaboration.Model;
using EventStorming.SharedKernel;

namespace EventStorming.Collaboration.Slices.JoinBoard;

/// <summary>A connection starts watching a board. Joining another board first leaves the previous one.</summary>
public sealed record JoinBoardCommand(Actor Actor, Guid BoardId, string ConnectionId);

public sealed record JoinedBoard(Participant You, IReadOnlyList<Participant> Participants);

public sealed class JoinBoardResult : IUseCaseResult
{
    private JoinBoardResult(JoinedBoard? joined, IReadOnlyList<Failure> failures)
    {
        Joined = joined;
        Failures = failures;
    }

    public JoinedBoard? Joined { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static JoinBoardResult Succeeded(JoinedBoard joined) => new(joined, []);

    public static JoinBoardResult Failed(Failure failure) => new(null, [failure]);
}

public interface IJoinBoardCommandHandler
{
    Task<JoinBoardResult> Handle(JoinBoardCommand command, CancellationToken cancellationToken);
}

public interface IJoinBoardStore
{
    /// <summary>Removes whatever board the connection was on before, returning that participant.</summary>
    Task<Participant?> Leave(string connectionId, CancellationToken cancellationToken);

    Task Add(Participant participant, CancellationToken cancellationToken);

    Task<IReadOnlyList<Participant>> OnBoard(Guid boardId, CancellationToken cancellationToken);
}
