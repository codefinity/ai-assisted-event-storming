using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Shared;

/// <summary>
/// The anti-corruption layer over the Teams context. Its adapter translates team membership and
/// role - or an API key's team and scopes - into a <see cref="BoardPermission"/>.
/// </summary>
public interface IBoardAccess
{
    Task<BoardAccess> ForBoard(Guid boardId, Actor actor, CancellationToken cancellationToken);

    Task<BoardPermission> ForTeam(Guid teamId, Actor actor, CancellationToken cancellationToken);
}

/// <summary>The EventStorming notation: every element type and level, as data.</summary>
public interface IElementTypeRegistry
{
    IReadOnlyList<ElementType> Types { get; }

    IReadOnlyList<LevelDescription> Levels { get; }

    ElementType? Find(string typeId);
}

/// <summary>
/// Announces committed changes to whoever has the board open. Called after the store has committed, and
/// never expected to fail the request: delivery is the adapter's concern.
/// </summary>
public interface IBoardChangeBroadcaster
{
    void ContentChanged(BoardChangeSet changes);

    /// <summary>The whole content was replaced (an import): open clients must reload it.</summary>
    void ContentReplaced(Guid boardId, long revision, ActorRef by);

    /// <summary>The board's name or archived state changed.</summary>
    void DetailsChanged(Board board);

    /// <summary>The board and all of its content are gone: open clients must close it.</summary>
    void Deleted(Guid boardId, ActorRef by);
}
