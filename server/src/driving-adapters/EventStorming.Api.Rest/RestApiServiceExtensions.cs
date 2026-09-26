using System.Globalization;
using System.Threading.RateLimiting;
using EventStorming.Api.Rest.App.Boards;
using EventStorming.Api.Rest.App.Identity;
using EventStorming.Api.Rest.App.PublicIntegration;
using EventStorming.Api.Rest.App.Teams;
using EventStorming.Api.Rest.Http;
using EventStorming.Api.Rest.V1;
using EventStorming.PublicIntegration.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EventStorming.Api.Rest;

public static class RestApiServiceExtensions
{
    public const string AppApiGroup = "app";
    public const string PublicApiGroup = "v1";
    public const string PublicApiRateLimitPolicy = "public-api";

    /// <summary>Authenticates a team API key and requires its 'read' scope. Other adapters (the MCP server) can require it too.</summary>
    public const string ApiKeyReadPolicy = ApiKeyAuthenticationHandler.ReadPolicy;
    public const long MaxPublicBodyBytes = 2 * 1024 * 1024;

    public static IServiceCollection AddEventStormingRestApi(this IServiceCollection services, RestApiOptions options)
    {
        services.AddSingleton(options);
        services.AddExceptionHandler<BadRequestExceptionHandler>();

        // The public API authenticates with API keys, the Public Integration context's credential.
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
        services.AddAuthorizationBuilder()
            .AddPolicy(ApiKeyAuthenticationHandler.ReadPolicy, policy => policy
                .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
                .RequireClaim(Actors.ScopeClaim, ApiScopes.Read))
            .AddPolicy(ApiKeyAuthenticationHandler.WritePolicy, policy => policy
                .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
                .RequireClaim(Actors.ScopeClaim, ApiScopes.Write));

        services.AddRateLimiter(limiter =>
        {
            limiter.OnRejected = async (context, _) =>
            {
                var http = context.HttpContext;
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : TimeSpan.FromMinutes(1);
                http.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await Problems.Of(ProblemCatalog.RateLimited, http, "Too many requests in the current window.", "rate-limited",
                    "Wait for the number of seconds in the Retry-After header, then retry. Batch work with the bulk endpoints.").ExecuteAsync(http);
            };

            limiter.AddPolicy(SessionEndpoints.SignInRateLimitPolicy, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = options.SignInPermitsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

            // Per API key (read from the presented key itself, so a flood costs no database reads);
            // unauthenticated calls share a budget per client address.
            limiter.AddPolicy(PublicApiRateLimitPolicy, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ApiKeyAuthenticationHandler.KeyIdOf(http.Request) ?? "ip:" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.PublicApiPermitsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    /// <summary>
    /// The web app's API under /api/app. The host decides authorization and CORS for the group;
    /// endpoints that must work signed-out opt out with AllowAnonymous.
    /// </summary>
    public static RouteGroupBuilder MapEventStormingAppApi(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<RestApiOptions>();
        var app = endpoints.MapGroup("/api/app").WithGroupName(AppApiGroup);

        SessionEndpoints.Map(app, options);
        TeamEndpoints.Map(app);
        BoardEndpoints.Map(app);
        ApiKeyEndpoints.Map(app);

        return app;
    }

    /// <summary>
    /// The public API under /api/v1: API-key authentication ("read" by default, "write" on anything that
    /// changes state), a per-key rate limit and a body-size limit. The host adds CORS.
    /// </summary>
    public static RouteGroupBuilder MapEventStormingPublicApi(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<RestApiOptions>();
        var api = endpoints.MapGroup("/api/v1")
            .WithGroupName(PublicApiGroup)
            .RequireAuthorization(ApiKeyAuthenticationHandler.ReadPolicy)
            .RequireRateLimiting(PublicApiRateLimitPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(MaxPublicBodyBytes))
            .AddEndpointFilter(async (context, next) =>
            {
                context.HttpContext.Response.Headers["RateLimit-Policy"] = $"{options.PublicApiPermitsPerMinute};w=60";
                return await next(context);
            });

        PublicEndpoints.Map(api);
        DocsEndpoints.Map(api);

        return api;
    }

    /// <summary>Replays retried public-API writes (Idempotency-Key). Must run after authorization, before the endpoints.</summary>
    public static IApplicationBuilder UseEventStormingIdempotency(this IApplicationBuilder app) =>
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments("/api/v1"),
            branch => branch.UseMiddleware<IdempotencyMiddleware>());
}
