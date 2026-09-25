using EventStorming.Api.Rest.Http;
using EventStorming.PublicIntegration.Model;
using EventStorming.PublicIntegration.Slices.CreateApiKey;
using EventStorming.PublicIntegration.Slices.ListApiKeys;
using EventStorming.PublicIntegration.Slices.RevokeApiKey;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace EventStorming.Api.Rest.App.PublicIntegration;

public sealed record CreateApiKeyRequest(string? Name, IReadOnlyList<string>? Scopes, DateTimeOffset? ExpiresAt);

public sealed record ApiKeyResponse(
    Guid Id,
    string Name,
    string Prefix,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt)
{
    public static ApiKeyResponse From(ApiKeySummary key) =>
        new(key.Id, key.Name, key.DisplayPrefix, key.Scopes, key.CreatedAt, key.ExpiresAt, key.LastUsedAt, key.RevokedAt);
}

/// <summary><see cref="Key"/> is the full API key. It is shown this once and cannot be retrieved again.</summary>
public sealed record CreatedApiKeyResponse(ApiKeyResponse ApiKey, string Key);

internal static class ApiKeyEndpoints
{
    public static void Map(RouteGroupBuilder app)
    {
        var keys = app.MapGroup("/teams/{teamId:guid}/api-keys").WithTags("API keys");

        keys.MapGet("/", async Task<Results<Ok<IReadOnlyList<ApiKeyResponse>>, ProblemHttpResult>> (Guid teamId, IListApiKeysQueryHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new ListApiKeysQuery(Actors.From(http.User), teamId), cancellationToken);
                return result.Success
                    ? TypedResults.Ok<IReadOnlyList<ApiKeyResponse>>(result.Keys.Select(ApiKeyResponse.From).ToList())
                    : Problems.From(result, http);
            })
            .WithName("ListApiKeys").WithSummary("A team's API keys, newest first (Owners only)");

        keys.MapPost("/", async Task<Results<Created<CreatedApiKeyResponse>, ProblemHttpResult>> (Guid teamId, CreateApiKeyRequest request, ICreateApiKeyCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(
                    new CreateApiKeyCommand(Actors.From(http.User), teamId, request.Name ?? string.Empty, request.Scopes ?? [], request.ExpiresAt),
                    cancellationToken);
                return result.Success
                    ? TypedResults.Created($"/api/app/teams/{teamId}/api-keys/{result.Created!.Summary.Id}", new CreatedApiKeyResponse(ApiKeyResponse.From(result.Created.Summary), result.Created.Key))
                    : Problems.From(result, http);
            })
            .WithName("CreateApiKey").WithSummary("Create an API key; the full key is in the response only (Owners only)");

        keys.MapDelete("/{keyId:guid}", async Task<Results<Ok<ApiKeyResponse>, ProblemHttpResult>> (Guid teamId, Guid keyId, IRevokeApiKeyCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new RevokeApiKeyCommand(Actors.From(http.User), teamId, keyId), cancellationToken);
                return result.Success ? TypedResults.Ok(ApiKeyResponse.From(result.Key!)) : Problems.From(result, http);
            })
            .WithName("RevokeApiKey").WithSummary("Revoke an API key; it stops working immediately (Owners only)");
    }
}
