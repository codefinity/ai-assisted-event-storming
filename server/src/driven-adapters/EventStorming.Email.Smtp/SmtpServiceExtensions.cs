using EventStorming.Teams.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EventStorming.Email.Smtp;

public static class SmtpServiceExtensions
{
    public static IServiceCollection AddEventStormingSmtpEmail(this IServiceCollection services, SmtpOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IInvitationMailer, SmtpInvitationMailer>();
        return services;
    }
}
