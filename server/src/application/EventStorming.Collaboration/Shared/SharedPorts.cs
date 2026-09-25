using EventStorming.Collaboration.Model;
using EventStorming.SharedKernel;

namespace EventStorming.Collaboration.Shared;

/// <summary>
/// Anti-corruption layer over Board Modelling and Teams: may this actor watch this board? Collaboration
/// needs nothing more from either context.
/// </summary>
public interface IBoardViewAccess
{
    Task<bool> CanView(Guid boardId, Actor actor, CancellationToken cancellationToken);
}

/// <summary>Tells everyone else on a board what a participant is doing. Fire-and-forget: presence is best effort.</summary>
public interface IPresenceBroadcaster
{
    void Joined(Participant participant);

    void Left(Participant participant);

    void CursorMoved(Participant participant, double x, double y);

    void EditingFocusChanged(Participant participant);

    void DragPreviewed(Participant participant, IReadOnlyList<DragMove> moves);
}
