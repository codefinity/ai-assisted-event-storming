using EventStorming.Identity.Model;

namespace EventStorming.Identity.Shared;

public sealed record IssuedAccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>Mints the short-lived bearer token a signed-in account presents on every request.</summary>
public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(AccountSummary account);
}
