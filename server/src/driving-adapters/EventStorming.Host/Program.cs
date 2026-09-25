using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventStorming.Api.Rest;
using EventStorming.Api.Rest.Http;
using EventStorming.BoardModelling;
using EventStorming.Broadcasting;
using EventStorming.Collaboration;
using EventStorming.ElementTypes.Json;
using EventStorming.Email.Smtp;
using EventStorming.Host.Configuration;
using EventStorming.Host.Seed;
using EventStorming.Identity;
using EventStorming.Persistence.MongoDb;
using EventStorming.Presence.InMemory;
using EventStorming.PublicIntegration;
using EventStorming.Realtime.SignalR;
using EventStorming.Security;
using EventStorming.SystemClock;
using EventStorming.Teams;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

using var bootstrapLogging = LoggerFactory.Create(logging => logging.AddConsole());
var logger = bootstrapLogging.CreateLogger("EventStorming.Host");

var mongo = configuration.Required<MongoOptions>("Mongo");
var jwt = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
var signingKey = SigningKeys.Resolve(jwt, configuration, builder.Environment, logger);
var webAppBaseUrl = configuration["WebApp:BaseUrl"] ?? "http://localhost:3000";
var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var restApi = new RestApiOptions(
    SecureCookies: configuration.GetValue("RestApi:SecureCookies", !builder.Environment.IsDevelopment()),
    SignInPermitsPerMinute: configuration.GetValue("RestApi:SignInPermitsPerMinute", 10),
    PublicApiPermitsPerMinute: configuration.GetValue("RestApi:PublicApiPermitsPerMinute", 300));
var smtp = new SmtpOptions(
    configuration["Smtp:Host"] ?? "localhost",
    configuration.GetValue("Smtp:Port", 1025),
    configuration["Smtp:From"] ?? "EventStorming <noreply@eventstorming.local>",
    webAppBaseUrl,
    configuration["Smtp:Username"],
    configuration["Smtp:Password"]);

// The composition root: the only place that knows which adapters are in play. Each project exposes
// one AddEventStorming* extension; the bounded contexts register their use cases, the driven adapters
// register the ports those use cases need, and the driving adapters register how requests arrive.
builder.Services
    // Bounded contexts - the application core
    .AddEventStormingIdentity()
    .AddEventStormingTeams()
    .AddEventStormingBoardModelling()
    .AddEventStormingCollaboration()
    .AddEventStormingPublicIntegration()
    // Driven adapters
    .AddEventStormingMongoDb(mongo)                                   // every store, and the ACL over team membership
    .AddEventStormingSecurity(new SecurityOptions(
        jwt.Issuer,
        jwt.Audience,
        signingKey,
        jwt.AccessTokenMinutes,
        configuration.GetValue("Security:PasswordHashIterations", 600_000)))
    .AddEventStormingSystemClock()
    .AddEventStormingSmtpEmail(smtp)                                  // invitation emails
    .AddEventStormingElementTypes(configuration["ElementTypes:Path"]) // the notation, as data
    .AddEventStormingInMemoryPresence()                               // who is on which board
    .AddEventStormingBroadcasting()                                   // committed changes and presence, onto the board feed
    // Driving adapters
    .AddEventStormingRestApi(restApi)                                 // /api/app for the web app, /api/v1 for everyone else
    .AddEventStormingRealtime();                                      // the board hub, and the relay from the board feed

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower)));

builder.Services
    .AddAuthentication()
    .AddJwtBearer(HostAuth.UserScheme, options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(signingKey)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
        };
        options.Events = new JwtBearerEvents
        {
            // A browser cannot set headers on a WebSocket, so the board hub - and only the hub - accepts
            // the access token from the query string.
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
        };
    });

builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy(HostAuth.UserPolicy, policy => policy.AddAuthenticationSchemes(HostAuth.UserScheme).RequireAuthenticatedUser());

// Explicit origins only. The web app's API and hub need credentials (the refresh cookie); the public
// API is called server-to-server and admits browser origins only if configured.
builder.Services.AddCors(cors =>
{
    cors.AddPolicy(HostAuth.WebAppCors, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
    cors.AddPolicy(HostAuth.PublicApiCors, policy => policy
        .WithOrigins(configuration.GetSection("Cors:PublicApiOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("Location", "Retry-After", "RateLimit-Policy", "Idempotent-Replayed"));
});

// Every error the framework itself produces (401/403 challenges, 404, 405, unhandled exceptions) gets
// the same RFC 9457 shape and problem-type URIs as the ones the use cases produce.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    var problem = context.ProblemDetails;
    var status = problem.Status ?? context.HttpContext.Response.StatusCode;
    if (problem.Type is null || !problem.Type.StartsWith(ProblemCatalog.TypeBase, StringComparison.Ordinal))
    {
        var type = ProblemCatalog.ForStatus(status) ?? ProblemCatalog.InternalError;
        problem.Type = type.Uri;
        problem.Title = type.Title;
        problem.Detail = status >= 500 ? type.Description : problem.Detail ?? type.Description;
        problem.Extensions["code"] = type.Slug;
        problem.Extensions["fix"] = type.WhatToDo;
    }

    problem.Instance ??= context.HttpContext.Request.Path;
    problem.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
});

// Unreadable bodies are thrown so BadRequestExceptionHandler can say exactly where the JSON broke.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

builder.Services.AddOpenApi(RestApiServiceExtensions.PublicApiGroup, options =>
{
    options.ShouldInclude = description => description.GroupName == RestApiServiceExtensions.PublicApiGroup;
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "EventStorming public API",
            Version = "v1",
            Description = "Build and read EventStorming boards from other systems and LLMs. Authenticate with a team API key: "
                + "'Authorization: Bearer es_…'. Every error is RFC 9457 Problem Details naming the offending field and the fix. "
                + "Start with GET /api/v1/element-types and POST /api/v1/boards/import. Guide for LLMs: /docs/llm-guide.md.",
        };
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["apiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "es_<key id>_<secret>",
            Description = "A team API key, created by a team Owner in the web app.",
        };
        document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("apiKey", document)] = [] }];
        return Task.CompletedTask;
    });
});

builder.Services.AddOpenApi(RestApiServiceExtensions.AppApiGroup, options =>
{
    options.ShouldInclude = description => description.GroupName == RestApiServiceExtensions.AppApiGroup;
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "EventStorming web app API",
            Version = "internal",
            Description = "The API behind the EventStorming web app. Unversioned and subject to change: integrate against /api/v1 instead.",
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest ? badRequest.StatusCode : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseEventStormingIdempotency();

app.MapOpenApi();
app.MapScalarApiReference("/docs", scalar => scalar
    .WithTitle("EventStorming API")
    .AddDocument(RestApiServiceExtensions.PublicApiGroup, "Public API v1", isDefault: true)
    .AddDocument(RestApiServiceExtensions.AppApiGroup, "Web app API (internal)"));

app.MapGuides();
app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" })).ExcludeFromDescription();

app.MapEventStormingAppApi()
    .RequireAuthorization(HostAuth.UserPolicy)
    .RequireCors(HostAuth.WebAppCors);

app.MapEventStormingPublicApi()
    .RequireCors(HostAuth.PublicApiCors);

app.MapEventStormingBoardHub()
    .RequireAuthorization(HostAuth.UserPolicy)
    .RequireCors(HostAuth.WebAppCors);

if (args.Contains("seed", StringComparer.OrdinalIgnoreCase))
{
    // Start the host so its startup work (index creation) runs, seed through the use cases, and stop.
    await app.StartAsync();
    await DemoSeeder.Run(app.Services, configuration, app.Logger, CancellationToken.None);
    await app.StopAsync();
    return;
}

app.Run();

/// <summary>Exposed so the integration tests can host the API in-process.</summary>
public partial class Program;
