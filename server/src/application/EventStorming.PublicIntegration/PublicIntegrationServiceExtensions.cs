using EventStorming.PublicIntegration.Slices.AuthenticateApiKey;
using EventStorming.PublicIntegration.Slices.CreateApiKey;
using EventStorming.PublicIntegration.Slices.ListApiKeys;
using EventStorming.PublicIntegration.Slices.RecordIdempotentResponse;
using EventStorming.PublicIntegration.Slices.ReserveIdempotencyKey;
using EventStorming.PublicIntegration.Slices.RevokeApiKey;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EventStorming.PublicIntegration;

public static class PublicIntegrationServiceExtensions
{
    public static IServiceCollection AddEventStormingPublicIntegration(this IServiceCollection services)
    {
        services.AddScoped<ICreateApiKeyCommandHandler, CreateApiKeyCommandHandler>();
        services.AddSingleton<IValidator<CreateApiKeyCommand>, CreateApiKeyCommandValidator>();
        services.AddScoped<IListApiKeysQueryHandler, ListApiKeysQueryHandler>();
        services.AddScoped<IRevokeApiKeyCommandHandler, RevokeApiKeyCommandHandler>();
        services.AddScoped<IAuthenticateApiKeyCommandHandler, AuthenticateApiKeyCommandHandler>();
        services.AddScoped<IReserveIdempotencyKeyCommandHandler, ReserveIdempotencyKeyCommandHandler>();
        services.AddScoped<IRecordIdempotentResponseCommandHandler, RecordIdempotentResponseCommandHandler>();

        return services;
    }
}
