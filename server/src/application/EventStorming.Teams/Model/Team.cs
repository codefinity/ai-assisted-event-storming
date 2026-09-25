namespace EventStorming.Teams.Model;

/// <summary>Owners manage members, invitations and API keys; Editors change boards; Viewers only look.</summary>
public enum TeamRole
{
    Owner,
    Editor,
    Viewer,
}

public sealed record Membership(Guid AccountId, TeamRole Role, DateTimeOffset JoinedAt);

/// <summary>
/// A team and everyone in it. <see cref="Version"/> increases on every membership change, so a store can
/// refuse a write based on a stale read (which is how "a team always keeps an Owner" survives races).
/// </summary>
public sealed record Team(Guid Id, string Name, DateTimeOffset CreatedAt, Guid CreatedBy, IReadOnlyList<Membership> Members, long Version);

public sealed record TeamSummary(Guid Id, string Name, TeamRole MyRole, int MemberCount);

public sealed record MemberProfile(string DisplayName, string Email);

public sealed record TeamMember(Guid AccountId, string DisplayName, string Email, TeamRole Role, DateTimeOffset JoinedAt);

public sealed record TeamDetails(Guid Id, string Name, TeamRole MyRole, IReadOnlyList<TeamMember> Members);

public static class TeamRoles
{
    public static readonly IReadOnlyList<string> Names = ["owner", "editor", "viewer"];

    public static bool IsValid(string? role) => role is not null && Names.Contains(role.Trim().ToLowerInvariant());

    public static TeamRole Parse(string role) => Enum.Parse<TeamRole>(role.Trim(), ignoreCase: true);

    public static string Name(TeamRole role) => role.ToString().ToLowerInvariant();

    public static TeamRole? RoleOf(this Team team, Guid accountId) =>
        team.Members.FirstOrDefault(member => member.AccountId == accountId)?.Role;

    public static int OwnerCount(this Team team) => team.Members.Count(member => member.Role == TeamRole.Owner);
}
