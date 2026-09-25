using EventStorming.Api.Rest.Http;
using EventStorming.Teams.Model;
using EventStorming.Teams.Slices.AcceptInvitation;
using EventStorming.Teams.Slices.ChangeMemberRole;
using EventStorming.Teams.Slices.CreateInvitation;
using EventStorming.Teams.Slices.CreateTeam;
using EventStorming.Teams.Slices.GetTeam;
using EventStorming.Teams.Slices.ListInvitations;
using EventStorming.Teams.Slices.ListMyTeams;
using EventStorming.Teams.Slices.PreviewInvitation;
using EventStorming.Teams.Slices.RemoveMember;
using EventStorming.Teams.Slices.RevokeInvitation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace EventStorming.Api.Rest.App.Teams;

public sealed record CreateTeamRequest(string? Name);

public sealed record TeamSummaryResponse(Guid Id, string Name, string MyRole, int MemberCount)
{
    public static TeamSummaryResponse From(TeamSummary team) => new(team.Id, team.Name, TeamRoles.Name(team.MyRole), team.MemberCount);
}

public sealed record TeamMemberResponse(Guid AccountId, string DisplayName, string Email, string Role, DateTimeOffset JoinedAt);

public sealed record TeamResponse(Guid Id, string Name, string MyRole, IReadOnlyList<TeamMemberResponse> Members)
{
    public static TeamResponse From(TeamDetails team) => new(
        team.Id,
        team.Name,
        TeamRoles.Name(team.MyRole),
        team.Members.Select(member => new TeamMemberResponse(member.AccountId, member.DisplayName, member.Email, TeamRoles.Name(member.Role), member.JoinedAt)).ToList());
}

public sealed record CreateInvitationRequest(string? Role, string? Email);

public sealed record InvitationResponse(Guid Id, string Kind, string? Email, string Role, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt)
{
    public static InvitationResponse From(InvitationView invitation) => new(
        invitation.Id, invitation.Kind == InvitationKind.Email ? "email" : "link", invitation.Email, TeamRoles.Name(invitation.Role), invitation.CreatedAt, invitation.ExpiresAt);
}

/// <summary><see cref="Token"/> goes in the invitation link: /invitations/{token}. It is not shown again.</summary>
public sealed record CreatedInvitationResponse(InvitationResponse Invitation, string Token);

public sealed record InvitationPreviewResponse(string TeamName, string InvitedBy, string Role, string Kind, string? Email, DateTimeOffset ExpiresAt, string Status);

public sealed record ChangeRoleRequest(string? Role);

internal static class TeamEndpoints
{
    public static void Map(RouteGroupBuilder app)
    {
        var teams = app.MapGroup("/teams").WithTags("Teams");

        teams.MapGet("/", async Task<Results<Ok<IReadOnlyList<TeamSummaryResponse>>, ProblemHttpResult>> (IListMyTeamsQueryHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new ListMyTeamsQuery(Actors.From(http.User)), cancellationToken);
                return result.Success
                    ? TypedResults.Ok<IReadOnlyList<TeamSummaryResponse>>(result.Teams.Select(TeamSummaryResponse.From).ToList())
                    : Problems.From(result, http);
            })
            .WithName("ListMyTeams").WithSummary("The teams the signed-in person belongs to");

        teams.MapPost("/", async Task<Results<Created<TeamSummaryResponse>, ProblemHttpResult>> (CreateTeamRequest request, ICreateTeamCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new CreateTeamCommand(Actors.From(http.User), request.Name ?? string.Empty), cancellationToken);
                return result.Success
                    ? TypedResults.Created($"/api/app/teams/{result.Team!.Id}", TeamSummaryResponse.From(result.Team))
                    : Problems.From(result, http);
            })
            .WithName("CreateTeam").WithSummary("Create a team; its creator becomes its Owner");

        teams.MapGet("/{teamId:guid}", async Task<Results<Ok<TeamResponse>, ProblemHttpResult>> (Guid teamId, IGetTeamQueryHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new GetTeamQuery(Actors.From(http.User), teamId), cancellationToken);
                return result.Success ? TypedResults.Ok(TeamResponse.From(result.Team!)) : Problems.From(result, http);
            })
            .WithName("GetTeam").WithSummary("A team, its members and your role in it");

        teams.MapGet("/{teamId:guid}/invitations", async Task<Results<Ok<IReadOnlyList<InvitationResponse>>, ProblemHttpResult>> (Guid teamId, IListInvitationsQueryHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new ListInvitationsQuery(Actors.From(http.User), teamId), cancellationToken);
                return result.Success
                    ? TypedResults.Ok<IReadOnlyList<InvitationResponse>>(result.Invitations.Select(InvitationResponse.From).ToList())
                    : Problems.From(result, http);
            })
            .WithName("ListInvitations").WithSummary("A team's open invitations (Owners only)");

        teams.MapPost("/{teamId:guid}/invitations", async Task<Results<Created<CreatedInvitationResponse>, ProblemHttpResult>> (Guid teamId, CreateInvitationRequest request, ICreateInvitationCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new CreateInvitationCommand(Actors.From(http.User), teamId, request.Role ?? string.Empty, request.Email), cancellationToken);
                return result.Success
                    ? TypedResults.Created($"/api/app/invitations/{result.Created!.Token}", new CreatedInvitationResponse(InvitationResponse.From(result.Created.Invitation), result.Created.Token))
                    : Problems.From(result, http);
            })
            .WithName("CreateInvitation").WithSummary("Invite by link (no email) or by email (Owners only)");

        teams.MapDelete("/{teamId:guid}/invitations/{invitationId:guid}", async Task<Results<NoContent, ProblemHttpResult>> (Guid teamId, Guid invitationId, IRevokeInvitationCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new RevokeInvitationCommand(Actors.From(http.User), teamId, invitationId), cancellationToken);
                return result.Success ? TypedResults.NoContent() : Problems.From(result, http);
            })
            .WithName("RevokeInvitation").WithSummary("Revoke an invitation (Owners only)");

        teams.MapPatch("/{teamId:guid}/members/{accountId:guid}", async Task<Results<NoContent, ProblemHttpResult>> (Guid teamId, Guid accountId, ChangeRoleRequest request, IChangeMemberRoleCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new ChangeMemberRoleCommand(Actors.From(http.User), teamId, accountId, request.Role ?? string.Empty), cancellationToken);
                return result.Success ? TypedResults.NoContent() : Problems.From(result, http);
            })
            .WithName("ChangeMemberRole").WithSummary("Change a member's role (Owners only)");

        teams.MapDelete("/{teamId:guid}/members/{accountId:guid}", async Task<Results<NoContent, ProblemHttpResult>> (Guid teamId, Guid accountId, IRemoveMemberCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new RemoveMemberCommand(Actors.From(http.User), teamId, accountId), cancellationToken);
                return result.Success ? TypedResults.NoContent() : Problems.From(result, http);
            })
            .WithName("RemoveMember").WithSummary("Remove a member, or leave the team yourself");

        var invitations = app.MapGroup("/invitations").WithTags("Teams");

        invitations.MapGet("/{token}", async Task<Results<Ok<InvitationPreviewResponse>, ProblemHttpResult>> (string token, IPreviewInvitationQueryHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new PreviewInvitationQuery(token), cancellationToken);
                if (!result.Success)
                {
                    return Problems.From(result, http);
                }

                var preview = result.Preview!;
                return TypedResults.Ok(new InvitationPreviewResponse(
                    preview.TeamName, preview.InvitedBy, TeamRoles.Name(preview.Role), preview.Kind == InvitationKind.Email ? "email" : "link",
                    preview.Email, preview.ExpiresAt, preview.Status.ToString().ToLowerInvariant()));
            })
            .AllowAnonymous()
            .WithName("PreviewInvitation").WithSummary("What an invitation is for - works signed out");

        invitations.MapPost("/{token}/accept", async Task<Results<Ok<TeamSummaryResponse>, ProblemHttpResult>> (string token, IAcceptInvitationCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new AcceptInvitationCommand(Actors.From(http.User), token), cancellationToken);
                return result.Success ? TypedResults.Ok(TeamSummaryResponse.From(result.Team!)) : Problems.From(result, http);
            })
            .WithName("AcceptInvitation").WithSummary("Join the team the invitation is for");
    }
}
