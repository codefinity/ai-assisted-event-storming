using System.Security.Claims;
using EventStorming.Identity.Model;
using EventStorming.Identity.Shared;
using EventStorming.SharedKernel;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EventStorming.Security;

/// <summary>
/// Issues an HMAC-SHA256 JWT carrying the registered claims "sub" (account id), "name" and "email".
/// Driving adapters read exactly those claims back when they build an Actor.
/// </summary>
internal sealed class JwtAccessTokenIssuer(SecurityOptions options, IClock clock) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler handler = new();
    private readonly SigningCredentials credentials = new(
        new SymmetricSecurityKey(Convert.FromBase64String(options.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    public IssuedAccessToken Issue(AccountSummary account)
    {
        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(options.AccessTokenMinutes);

        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, account.DisplayName),
                new Claim(JwtRegisteredClaimNames.Email, account.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
        });

        return new IssuedAccessToken(token, expiresAt);
    }
}
