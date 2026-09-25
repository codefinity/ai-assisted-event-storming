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
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EventStorming.Teams;

public static class TeamsServiceExtensions
{
    public static IServiceCollection AddEventStormingTeams(this IServiceCollection services)
    {
        services.AddScoped<ICreateTeamCommandHandler, CreateTeamCommandHandler>();
        services.AddSingleton<IValidator<CreateTeamCommand>, CreateTeamCommandValidator>();

        services.AddScoped<IListMyTeamsQueryHandler, ListMyTeamsQueryHandler>();
        services.AddScoped<IGetTeamQueryHandler, GetTeamQueryHandler>();

        services.AddScoped<ICreateInvitationCommandHandler, CreateInvitationCommandHandler>();
        services.AddSingleton<IValidator<CreateInvitationCommand>, CreateInvitationCommandValidator>();

        services.AddScoped<IListInvitationsQueryHandler, ListInvitationsQueryHandler>();
        services.AddScoped<IRevokeInvitationCommandHandler, RevokeInvitationCommandHandler>();
        services.AddScoped<IPreviewInvitationQueryHandler, PreviewInvitationQueryHandler>();
        services.AddScoped<IAcceptInvitationCommandHandler, AcceptInvitationCommandHandler>();

        services.AddScoped<IChangeMemberRoleCommandHandler, ChangeMemberRoleCommandHandler>();
        services.AddSingleton<IValidator<ChangeMemberRoleCommand>, ChangeMemberRoleCommandValidator>();

        services.AddScoped<IRemoveMemberCommandHandler, RemoveMemberCommandHandler>();

        return services;
    }
}
