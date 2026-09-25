using EventStorming.Api.Rest.Http;
using EventStorming.Identity.Model;
using EventStorming.Identity.Slices.GetMyAccount;
using EventStorming.Identity.Slices.RefreshSession;
using EventStorming.Identity.Slices.RegisterAccount;
using EventStorming.Identity.Slices.SignIn;
using EventStorming.Identity.Slices.SignOut;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace EventStorming.Api.Rest.App.Identity;

public sealed record SignUpRequest(string? Email, string? DisplayName, string? Password);

public sealed record SignInRequest(string? Email, string? Password);

public sealed record AccountResponse(Guid Id, string Email, string DisplayName)
{
    public static AccountResponse From(AccountSummary account) => new(account.Id, account.Email, account.DisplayName);
}

/// <summary>The access token for the Authorization header. The refresh token travels only in its HttpOnly cookie.</summary>
public sealed record SessionResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, AccountResponse Account)
{
    public static SessionResponse From(SessionTokens session) =>
        new(session.AccessToken, session.AccessTokenExpiresAt, AccountResponse.From(session.Account));
}

internal static class SessionEndpoints
{
    public const string SignInRateLimitPolicy = "sign-in";

    public static void Map(RouteGroupBuilder app, RestApiOptions options)
    {
        var auth = app.MapGroup("/auth").WithTags("Session");

        auth.MapPost("/sign-up", async Task<Results<Created<SessionResponse>, ProblemHttpResult>> (
                SignUpRequest request,
                IRegisterAccountCommandHandler register,
                ISignInCommandHandler signIn,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var registered = await register.Handle(
                    new RegisterAccountCommand(request.Email ?? string.Empty, request.DisplayName ?? string.Empty, request.Password ?? string.Empty),
                    cancellationToken);
                if (!registered.Success)
                {
                    return Problems.From(registered, http);
                }

                // Signing the new account straight in is this adapter's composition of two use cases.
                var session = await signIn.Handle(new SignInCommand(request.Email!, request.Password!), cancellationToken);
                if (!session.Success)
                {
                    return Problems.From(session, http);
                }

                RefreshCookie.Write(http.Response, session.Session!, options);
                return TypedResults.Created("/api/app/me", SessionResponse.From(session.Session!));
            })
            .AllowAnonymous()
            .RequireRateLimiting(SignInRateLimitPolicy)
            .WithName("SignUp")
            .WithSummary("Create an account and sign it in");

        auth.MapPost("/sign-in", async Task<Results<Ok<SessionResponse>, ProblemHttpResult>> (
                SignInRequest request,
                ISignInCommandHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new SignInCommand(request.Email ?? string.Empty, request.Password ?? string.Empty), cancellationToken);
                if (!result.Success)
                {
                    return Problems.From(result, http);
                }

                RefreshCookie.Write(http.Response, result.Session!, options);
                return TypedResults.Ok(SessionResponse.From(result.Session!));
            })
            .AllowAnonymous()
            .RequireRateLimiting(SignInRateLimitPolicy)
            .WithName("SignIn")
            .WithSummary("Sign in with email and password");

        auth.MapPost("/refresh", async Task<Results<Ok<SessionResponse>, ProblemHttpResult>> (
                IRefreshSessionCommandHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                if (!RefreshCookie.HasClientHeader(http.Request))
                {
                    return Problems.Of(ProblemCatalog.Forbidden, http, "The refresh request is missing its client header.", "missing-client-header",
                        $"Send the header '{RefreshCookie.ClientHeader}: fetch' with every session request.");
                }

                var result = await handler.Handle(new RefreshSessionCommand(RefreshCookie.Read(http.Request) ?? string.Empty), cancellationToken);
                if (!result.Success)
                {
                    if (result.Failures[0].Code != "session-rotated")
                    {
                        RefreshCookie.Clear(http.Response, options);
                    }

                    return Problems.From(result, http);
                }

                RefreshCookie.Write(http.Response, result.Session!, options);
                return TypedResults.Ok(SessionResponse.From(result.Session!));
            })
            .AllowAnonymous()
            .WithName("RefreshSession")
            .WithSummary("Exchange the refresh cookie for a new access token (and rotate the cookie)");

        auth.MapPost("/sign-out", async Task<Results<NoContent, ProblemHttpResult>> (
                ISignOutCommandHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                if (!RefreshCookie.HasClientHeader(http.Request))
                {
                    return Problems.Of(ProblemCatalog.Forbidden, http, "The sign-out request is missing its client header.", "missing-client-header",
                        $"Send the header '{RefreshCookie.ClientHeader}: fetch' with every session request.");
                }

                await handler.Handle(new SignOutCommand(RefreshCookie.Read(http.Request)), cancellationToken);
                RefreshCookie.Clear(http.Response, options);
                return TypedResults.NoContent();
            })
            .AllowAnonymous()
            .WithName("SignOut")
            .WithSummary("End the session and clear the refresh cookie");

        app.MapGet("/me", async Task<Results<Ok<AccountResponse>, ProblemHttpResult>> (
                IGetMyAccountQueryHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new GetMyAccountQuery(Actors.From(http.User)), cancellationToken);
                return result.Success ? TypedResults.Ok(AccountResponse.From(result.Account!)) : Problems.From(result, http);
            })
            .WithTags("Session")
            .WithName("GetMyAccount")
            .WithSummary("The signed-in account");
    }
}

/// <summary>
/// The refresh token lives only in an HttpOnly, SameSite=Strict cookie scoped to the session
/// endpoints. Those endpoints also demand a custom header, which a cross-site form cannot send.
/// </summary>
internal static class RefreshCookie
{
    public const string Name = "es_refresh";
    public const string ClientHeader = "X-Requested-With";
    private const string Path = "/api/app/auth";

    public static void Write(HttpResponse response, SessionTokens session, RestApiOptions options) =>
        response.Cookies.Append(Name, session.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = options.SecureCookies,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = session.RefreshTokenExpiresAt,
            IsEssential = true,
        });

    public static string? Read(HttpRequest request) => request.Cookies.TryGetValue(Name, out var value) ? value : null;

    public static void Clear(HttpResponse response, RestApiOptions options) =>
        response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = options.SecureCookies,
            SameSite = SameSiteMode.Strict,
            Path = Path,
        });

    public static bool HasClientHeader(HttpRequest request) =>
        !string.IsNullOrWhiteSpace(request.Headers[ClientHeader]);
}
