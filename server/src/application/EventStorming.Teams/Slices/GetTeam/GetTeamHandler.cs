using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Teams.Slices.GetTeam;

public sealed class GetTeamQueryHandler(IGetTeamStore store) : IGetTeamQueryHandler
{
    public async Task<GetTeamResult> Handle(GetTeamQuery query, CancellationToken cancellationToken)
    {
        var team = await store.Find(query.TeamId, cancellationToken);
        var myRole = team is null || query.Actor.Kind != ActorKind.Account ? null : team.RoleOf(query.Actor.Id);
        if (team is null || myRole is null)
        {
            return GetTeamResult.Failed(Failures.NotFound("The team"));
        }

        var profiles = await store.Profiles(team.Members.Select(member => member.AccountId).ToList(), cancellationToken);
        var members = team.Members
            .Select(member =>
            {
                var profile = profiles.GetValueOrDefault(member.AccountId) ?? new MemberProfile("Former member", string.Empty);
                return new TeamMember(member.AccountId, profile.DisplayName, profile.Email, member.Role, member.JoinedAt);
            })
            .OrderBy(member => member.Role)
            .ThenBy(member => member.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return GetTeamResult.Succeeded(new TeamDetails(team.Id, team.Name, myRole.Value, members));
    }
}
