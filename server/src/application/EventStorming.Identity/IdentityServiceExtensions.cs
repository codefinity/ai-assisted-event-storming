using EventStorming.Identity.Slices.GetMyAccount;
using EventStorming.Identity.Slices.RefreshSession;
using EventStorming.Identity.Slices.RegisterAccount;
using EventStorming.Identity.Slices.SignIn;
using EventStorming.Identity.Slices.SignOut;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EventStorming.Identity;

public static class IdentityServiceExtensions
{
    /// <summary>
    /// Registers the Identity context's use cases. The ports they need (stores, hasher, token issuer,
    /// clock) are registered by whichever driven adapters the host chooses.
    /// </summary>
    public static IServiceCollection AddEventStormingIdentity(this IServiceCollection services)
    {
        services.AddScoped<IRegisterAccountCommandHandler, RegisterAccountCommandHandler>();
        services.AddSingleton<IValidator<RegisterAccountCommand>, RegisterAccountCommandValidator>();

        services.AddScoped<ISignInCommandHandler, SignInCommandHandler>();
        services.AddSingleton<IValidator<SignInCommand>, SignInCommandValidator>();

        services.AddScoped<IRefreshSessionCommandHandler, RefreshSessionCommandHandler>();
        services.AddScoped<ISignOutCommandHandler, SignOutCommandHandler>();
        services.AddScoped<IGetMyAccountQueryHandler, GetMyAccountQueryHandler>();

        return services;
    }
}
