using EventStorming.Identity.Shared;
using EventStorming.PublicIntegration.Shared;
using EventStorming.Teams.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EventStorming.Security;

public static class SecurityServiceExtensions
{
    /// <summary>Password hashing, access-token issuing and secret generation for every context that needs them.</summary>
    public static IServiceCollection AddEventStormingSecurity(this IServiceCollection services, SecurityOptions options)
    {
        if (Convert.FromBase64String(options.SigningKey).Length < 32)
        {
            throw new InvalidOperationException("The access-token signing key must be at least 32 bytes (Base64-encoded).");
        }

        services.AddSingleton(options);
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        services.AddSingleton<SecureSecrets>();
        services.AddSingleton<IRefreshTokenGenerator>(provider => provider.GetRequiredService<SecureSecrets>());
        services.AddSingleton<IInvitationTokenGenerator>(provider => provider.GetRequiredService<SecureSecrets>());
        services.AddSingleton<IApiKeySecretGenerator>(provider => provider.GetRequiredService<SecureSecrets>());

        return services;
    }
}
