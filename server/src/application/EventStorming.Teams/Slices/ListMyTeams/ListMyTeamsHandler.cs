using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Teams.Slices.ListMyTeams;

public sealed class ListMyTeamsQueryHandler(IListMyTeamsStore store) : IListMyTeamsQueryHandler
{
    public async Task<ListMyTeamsResult> Handle(ListMyTeamsQuery query, CancellationToken cancellationToken)
    {
        if (query.Actor.Kind != ActorKind.Account)
        {
            return ListMyTeamsResult.Failed(Failures.Forbidden("Only a signed-in person belongs to teams."));
        }

        var teams = await store.TeamsOf(query.Actor.Id, cancellationToken);
        return ListMyTeamsResult.Succeeded(teams
            .Select(team => new TeamSummary(team.Id, team.Name, team.RoleOf(query.Actor.Id)!.Value, team.Members.Count))
            .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }
}
