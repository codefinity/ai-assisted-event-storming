using EventStorming.Identity.Model;
using EventStorming.Identity.Slices.GetMyAccount;
using EventStorming.Identity.Slices.RefreshSession;
using EventStorming.Identity.Slices.RegisterAccount;
using EventStorming.Identity.Slices.SignIn;
using EventStorming.Identity.Slices.SignOut;

namespace EventStorming.Specs.Support.Fakes;

/// <summary>Every Identity store port over two lists - the same behaviour the MongoDB stores promise.</summary>
public sealed class InMemoryIdentity :
    IRegisterAccountStore,
    ISignInStore,
    IRefreshSessionStore,
    ISignOutStore,
    IGetMyAccountStore
{
    public List<Account> Accounts { get; } = [];

    public List<RefreshToken> RefreshTokens { get; } = [];

    public Task<bool> TryInsert(Account account, CancellationToken cancellationToken)
    {
        if (Accounts.Any(existing => existing.Email == account.Email))
        {
            return Task.FromResult(false);
        }

        Accounts.Add(account);
        return Task.FromResult(true);
    }

    public Task<Account?> FindByEmail(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.FirstOrDefault(account => account.Email == normalizedEmail));

    public Task Save(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        RefreshTokens.Add(refreshToken);
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> FindByHash(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(RefreshTokens.FirstOrDefault(token => token.TokenHash == tokenHash));

    public Task<Account?> FindAccount(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.FirstOrDefault(account => account.Id == accountId));

    public Task<Account?> Find(Guid accountId, CancellationToken cancellationToken) => FindAccount(accountId, cancellationToken);

    public Task<bool> TryRotate(Guid currentId, RefreshToken replacement, DateTimeOffset rotatedAt, CancellationToken cancellationToken)
    {
        var index = RefreshTokens.FindIndex(token => token.Id == currentId);
        if (index < 0 || RefreshTokens[index].ReplacedById is not null || RefreshTokens[index].RevokedAt is not null)
        {
            return Task.FromResult(false);
        }

        RefreshTokens[index] = RefreshTokens[index] with { ReplacedById = replacement.Id, RotatedAt = rotatedAt };
        RefreshTokens.Add(replacement);
        return Task.FromResult(true);
    }

    public Task RevokeFamily(Guid familyId, string reason, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        for (var i = 0; i < RefreshTokens.Count; i++)
        {
            if (RefreshTokens[i].FamilyId == familyId && RefreshTokens[i].RevokedAt is null)
            {
                RefreshTokens[i] = RefreshTokens[i] with { RevokedAt = revokedAt, RevokedReason = reason };
            }
        }

        return Task.CompletedTask;
    }
}
