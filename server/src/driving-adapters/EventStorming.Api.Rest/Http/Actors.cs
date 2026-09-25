using System.Security.Claims;
using EventStorming.SharedKernel;

namespace EventStorming.Api.Rest.Http;

/// <summary>
/// Builds the core's <see cref="Actor"/> from whichever scheme authenticated the request: a user's JWT
/// ("sub", "name") or an API key (the claims ApiKeyAuthenticationHandler writes).
/// </summary>
public static class Actors
{
    public const string ApiKeyIdClaim = "api_key_id";
    public const string TeamIdClaim = "team_id";
    public const string ScopeClaim = "scope";

    public static Actor From(ClaimsPrincipal principal)
    {
        var keyId = principal.FindFirstValue(ApiKeyIdClaim);
        if (keyId is not null)
        {
            return Actor.ApiKey(
                Guid.Parse(keyId),
                principal.FindFirstValue("name") ?? "API key",
                Guid.Parse(principal.FindFirstValue(TeamIdClaim)!),
                principal.FindAll(ScopeClaim).Select(claim => claim.Value).ToList());
        }

        var subject = principal.FindFirstValue("sub")
            ?? throw new InvalidOperationException("The request is not authenticated; the endpoint must require authorization.");
        return Actor.Account(Guid.Parse(subject), principal.FindFirstValue("name") ?? string.Empty);
    }
}
