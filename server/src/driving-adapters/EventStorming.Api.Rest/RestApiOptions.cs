namespace EventStorming.Api.Rest;

/// <param name="SecureCookies">Marks the refresh cookie Secure. Off only for plain-HTTP local development.</param>
/// <param name="SignInPermitsPerMinute">Sign-in and sign-up attempts allowed per client IP per minute.</param>
/// <param name="PublicApiPermitsPerMinute">Requests allowed per API key per minute on /api/v1.</param>
public sealed record RestApiOptions(
    bool SecureCookies = true,
    int SignInPermitsPerMinute = 10,
    int PublicApiPermitsPerMinute = 300);
