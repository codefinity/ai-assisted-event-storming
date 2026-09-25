using System.Security.Claims;
using System.Text.Encodings.Web;
using EventStorming.Api.Rest.Http;
using EventStorming.PublicIntegration.Model;
using EventStorming.PublicIntegration.Slices.AuthenticateApiKey;
using EventStorming.SharedKernel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventStorming.Api.Rest.V1;

/// <summary>
/// Authenticates the public API's callers by API key ("Authorization: Bearer es_…", or the
/// "X-Api-Key" header). Deciding whether a key is valid is the Public Integration context's job; this
/// handler only moves the key from the header into the use case and the answer into a principal.
/// </summary>
internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "api-key";
    public const string ReadPolicy = "api-read";
    public const string WritePolicy = "api-write";

    private const string FailureItem = "api-key-failure";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var presented = PresentedKey(Request);
        if (presented is null)
        {
            return AuthenticateResult.NoResult();
        }

        var handler = Context.RequestServices.GetRequiredService<IAuthenticateApiKeyCommandHandler>();
        var result = await handler.Handle(new AuthenticateApiKeyCommand(presented), Context.RequestAborted);
        if (!result.Success)
        {
            Context.Items[FailureItem] = result.Failures[0];
            return AuthenticateResult.Fail(result.Failures[0].Message);
        }

        var key = result.Key!;
        var claims = new List<Claim>
        {
            new(Actors.ApiKeyIdClaim, key.KeyId.ToString()),
            new(Actors.TeamIdClaim, key.TeamId.ToString()),
            new("name", key.Name),
        };
        claims.AddRange(key.Scopes.Select(scope => new Claim(Actors.ScopeClaim, scope)));

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme, "name", null)), Scheme));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var failure = Context.Items.TryGetValue(FailureItem, out var stored) ? stored as Failure : null;
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer realm=\"eventstorming\"";
        var problem = failure is null
            ? Problems.Of(ProblemCatalog.Unauthenticated, Context, "No API key was sent.", "api-key-missing",
                "Send 'Authorization: Bearer <api key>'. Team Owners create keys in the team settings.")
            : Problems.From([failure], Context);
        await problem.ExecuteAsync(Context);
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await Problems.Of(ProblemCatalog.Forbidden, Context, "This API key does not have the 'write' scope.", "insufficient-scope",
            "Use an API key created with the 'write' scope for requests that change anything.").ExecuteAsync(Context);
    }

    public static string? PresentedKey(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return header["Bearer ".Length..].Trim();
        }

        var apiKey = request.Headers["X-Api-Key"].ToString();
        return string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
    }

    /// <summary>The key id part of a presented key, which is enough to rate-limit without a database read.</summary>
    public static string? KeyIdOf(HttpRequest request) =>
        ApiKeyFormat.TryParse(PresentedKey(request), out var keyId, out _) ? keyId.ToString("N") : null;
}
