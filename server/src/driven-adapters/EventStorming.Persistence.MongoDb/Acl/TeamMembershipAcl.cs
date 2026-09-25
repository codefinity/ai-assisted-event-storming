using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.Collaboration.Shared;
using EventStorming.Persistence.MongoDb.BoardModelling;
using EventStorming.Persistence.MongoDb.Infrastructure;
using EventStorming.Persistence.MongoDb.Teams;
using EventStorming.PublicIntegration.Model;
using EventStorming.PublicIntegration.Shared;
using EventStorming.SharedKernel;
using EventStorming.Teams.Model;
using MongoDB.Driver;

namespace EventStorming.Persistence.MongoDb.Acl;

/// <summary>
/// The anti-corruption layer over the Teams context. Board Modelling, Collaboration and Public
/// Integration each ask their own narrow question ("may this actor edit this board?", "may they watch
/// it?", "do they own this team?"); this is the one place that answers all three from team roles and
/// API-key scopes, so none of those contexts ever learns what a TeamRole is.
/// </summary>
internal sealed class TeamMembershipAcl(MongoDatabase mongo) : IBoardAccess, IBoardViewAccess, ITeamOwnership
{
    public async Task<BoardAccess> ForBoard(Guid boardId, Actor actor, CancellationToken cancellationToken)
    {
        var board = await BoardCollections.BoardsIn(mongo)
            .Find(candidate => candidate.Id == boardId)
            .Project(candidate => new { candidate.TeamId, candidate.ArchivedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (board is null)
        {
            return BoardAccess.NoAccess;
        }

        return new BoardAccess(await ForTeam(board.TeamId, actor, cancellationToken), board.ArchivedAt is not null);
    }

    public async Task<BoardPermission> ForTeam(Guid teamId, Actor actor, CancellationToken cancellationToken)
    {
        if (actor.Kind == ActorKind.ApiKey)
        {
            return actor.TeamId != teamId ? BoardPermission.None
                : actor.HasScope(ApiScopes.Write) ? BoardPermission.Edit
                : actor.HasScope(ApiScopes.Read) ? BoardPermission.View
                : BoardPermission.None;
        }

        return await RoleOf(teamId, actor.Id, cancellationToken) switch
        {
            TeamRole.Owner or TeamRole.Editor => BoardPermission.Edit,
            TeamRole.Viewer => BoardPermission.View,
            _ => BoardPermission.None,
        };
    }

    public async Task<bool> CanView(Guid boardId, Actor actor, CancellationToken cancellationToken) =>
        (await ForBoard(boardId, actor, cancellationToken)).Permission != BoardPermission.None;

    public async Task<bool> IsOwner(Guid teamId, Guid accountId, CancellationToken cancellationToken) =>
        await RoleOf(teamId, accountId, cancellationToken) == TeamRole.Owner;

    private async Task<TeamRole?> RoleOf(Guid teamId, Guid accountId, CancellationToken cancellationToken)
    {
        var members = await TeamsCollections.TeamsIn(mongo)
            .Find(team => team.Id == teamId)
            .Project(team => team.Members)
            .FirstOrDefaultAsync(cancellationToken);
        return members?.FirstOrDefault(member => member.AccountId == accountId)?.Role;
    }
}
