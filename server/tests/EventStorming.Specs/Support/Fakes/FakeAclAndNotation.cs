using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.Collaboration.Model;
using EventStorming.Collaboration.Shared;
using EventStorming.PublicIntegration.Shared;
using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Specs.Support.Fakes;

/// <summary>The anti-corruption layer's rules - role to permission, API-key scopes to permission - over the Teams and Boards fakes.</summary>
public sealed class InMemoryAcl(InMemoryTeams teams, InMemoryBoards boards) : IBoardAccess, IBoardViewAccess, ITeamOwnership
{
    public async Task<BoardAccess> ForBoard(Guid boardId, Actor actor, CancellationToken cancellationToken)
    {
        var board = boards.Boards.FirstOrDefault(candidate => candidate.Id == boardId);
        return board is null
            ? BoardAccess.NoAccess
            : new BoardAccess(await ForTeam(board.TeamId, actor, cancellationToken), board.ArchivedAt is not null);
    }

    public Task<BoardPermission> ForTeam(Guid teamId, Actor actor, CancellationToken cancellationToken)
    {
        if (actor.Kind == ActorKind.ApiKey)
        {
            return Task.FromResult(actor.TeamId != teamId ? BoardPermission.None
                : actor.HasScope("write") ? BoardPermission.Edit
                : actor.HasScope("read") ? BoardPermission.View
                : BoardPermission.None);
        }

        var role = teams.Teams.FirstOrDefault(team => team.Id == teamId)?.Members.FirstOrDefault(member => member.AccountId == actor.Id)?.Role;
        return Task.FromResult(role switch
        {
            TeamRole.Owner or TeamRole.Editor => BoardPermission.Edit,
            TeamRole.Viewer => BoardPermission.View,
            _ => BoardPermission.None,
        });
    }

    public async Task<bool> CanView(Guid boardId, Actor actor, CancellationToken cancellationToken) =>
        (await ForBoard(boardId, actor, cancellationToken)).Permission != BoardPermission.None;

    public Task<bool> IsOwner(Guid teamId, Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult(teams.Teams.FirstOrDefault(team => team.Id == teamId)?.Members.Any(member => member.AccountId == accountId && member.Role == TeamRole.Owner) == true);
}

/// <summary>The standard notation, written out in code so the specs do not depend on the registry file.</summary>
public sealed class FakeElementTypeRegistry : IElementTypeRegistry
{
    private static readonly BoardLevel[] AllLevels = [BoardLevel.BigPicture, BoardLevel.ProcessModelling, BoardLevel.SoftwareDesign];

    public IReadOnlyList<ElementType> Types { get; } =
    [
        Sticky("domain-event", "Domain Event", "E", canBePivotal: true),
        Sticky("command", "Command", "C", levels: [BoardLevel.ProcessModelling, BoardLevel.SoftwareDesign]),
        Sticky("actor", "Actor", "A", width: 120, height: 72, maxText: 80),
        Sticky("policy", "Policy", "P", levels: [BoardLevel.ProcessModelling, BoardLevel.SoftwareDesign]),
        Sticky("read-model", "Read Model", "R", levels: [BoardLevel.ProcessModelling, BoardLevel.SoftwareDesign]),
        Sticky("external-system", "External System", "X"),
        Sticky("aggregate", "Aggregate / Constraint", "G", width: 240, height: 160, levels: [BoardLevel.SoftwareDesign]),
        Sticky("hot-spot", "Hot Spot", "H", maxText: 300),
        Sticky("opportunity", "Opportunity", "O", levels: [BoardLevel.BigPicture]),
        Structure("swimlane", "Swimlane", "L", LayoutRole.Lane, 1600, 240),
        Structure("boundary", "Boundary", "B", LayoutRole.Boundary, 720, 480),
    ];

    public IReadOnlyList<LevelDescription> Levels { get; } =
    [
        new(BoardLevel.BigPicture, "Big Picture", "Explore a whole domain."),
        new(BoardLevel.ProcessModelling, "Process Modelling", "Model one process."),
        new(BoardLevel.SoftwareDesign, "Software Design", "Design the software."),
    ];

    public ElementType? Find(string typeId) => Types.FirstOrDefault(type => type.Id == typeId);

    private static ElementType Sticky(string id, string name, string shortcut, bool canBePivotal = false, double width = 160, double height = 100, int maxText = 200, BoardLevel[]? levels = null) =>
        new(id, name, ElementCategory.Sticky, "sticky", "#FFA94D", "#1B1B1F", "zap", new Size(width, height), levels ?? AllLevels,
            shortcut, maxText, canBePivotal, LayoutRole.Item, name, name, null, []);

    private static ElementType Structure(string id, string name, string shortcut, LayoutRole role, double width, double height) =>
        new(id, name, ElementCategory.Structure, role == LayoutRole.Lane ? "lane" : "area", "#F1F3F5", "#495057", "rows", new Size(width, height),
            AllLevels, shortcut, 100, false, role, name, name, null, []);
}

/// <summary>Records everything announced, so scenarios can assert what open boards would have seen.</summary>
public sealed class RecordingBroadcaster : IBoardChangeBroadcaster, IPresenceBroadcaster
{
    public List<BoardChangeSet> Changes { get; } = [];

    public List<(Guid BoardId, long Revision)> Replaced { get; } = [];

    public List<Board> Details { get; } = [];

    public List<(string What, Participant Participant)> Presence { get; } = [];

    public void ContentChanged(BoardChangeSet changes) => Changes.Add(changes);

    public void ContentReplaced(Guid boardId, long revision, ActorRef by) => Replaced.Add((boardId, revision));

    public void DetailsChanged(Board board) => Details.Add(board);

    public void Joined(Participant participant) => Presence.Add(("joined", participant));

    public void Left(Participant participant) => Presence.Add(("left", participant));

    public void CursorMoved(Participant participant, double x, double y) => Presence.Add(("cursor", participant));

    public void EditingFocusChanged(Participant participant) => Presence.Add(("focus", participant));

    public void DragPreviewed(Participant participant, IReadOnlyList<DragMove> moves) => Presence.Add(("drag", participant));
}
