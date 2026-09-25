using EventStorming.Collaboration.Model;
using EventStorming.SharedKernel;

namespace EventStorming.Collaboration.Slices.ShareDragPreview;

/// <summary>
/// While a participant drags elements, the others see them move. Nothing is stored: the drop is
/// committed separately through Board Modelling, and a drag that is abandoned simply stops.
/// An empty <see cref="Moves"/> list ends the preview.
/// </summary>
public sealed record ShareDragPreviewCommand(string ConnectionId, Guid BoardId, IReadOnlyList<DragMove> Moves);

public sealed class ShareDragPreviewResult : IUseCaseResult
{
    private ShareDragPreviewResult(IReadOnlyList<Failure> failures) => Failures = failures;

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static ShareDragPreviewResult Succeeded() => new([]);

    public static ShareDragPreviewResult Failed(Failure failure) => new([failure]);
}

public interface IShareDragPreviewCommandHandler
{
    Task<ShareDragPreviewResult> Handle(ShareDragPreviewCommand command, CancellationToken cancellationToken);
}

public interface IShareDragPreviewStore
{
    Task<Participant?> Find(string connectionId, CancellationToken cancellationToken);
}
