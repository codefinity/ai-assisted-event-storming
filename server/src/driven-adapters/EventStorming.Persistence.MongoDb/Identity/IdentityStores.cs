using EventStorming.Identity.Model;
using EventStorming.Identity.Slices.GetMyAccount;
using EventStorming.Identity.Slices.RefreshSession;
using EventStorming.Identity.Slices.RegisterAccount;
using EventStorming.Identity.Slices.SignIn;
using EventStorming.Identity.Slices.SignOut;
using EventStorming.Persistence.MongoDb.Infrastructure;
using MongoDB.Driver;

namespace EventStorming.Persistence.MongoDb.Identity;

internal sealed class RegisterAccountStore(MongoDatabase mongo) : IRegisterAccountStore
{
    public async Task<bool> TryInsert(Account account, CancellationToken cancellationToken)
    {
        try
        {
            await IdentityCollections.AccountsIn(mongo).InsertOneAsync(MongoAccount.From(account), cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoException exception) when (MongoErrors.IsDuplicateKey(exception))
        {
            return false;
        }
    }
}

internal sealed class SignInStore(MongoDatabase mongo) : ISignInStore
{
    public async Task<Account?> FindByEmail(string normalizedEmail, CancellationToken cancellationToken)
    {
        var account = await IdentityCollections.AccountsIn(mongo)
            .Find(candidate => candidate.Email == normalizedEmail)
            .FirstOrDefaultAsync(cancellationToken);
        return account?.ToModel();
    }

    public Task Save(RefreshToken refreshToken, CancellationToken cancellationToken) =>
        IdentityCollections.RefreshTokensIn(mongo).InsertOneAsync(MongoRefreshToken.From(refreshToken), cancellationToken: cancellationToken);
}

internal sealed class RefreshSessionStore(MongoDatabase mongo) : IRefreshSessionStore
{
    public async Task<RefreshToken?> FindByHash(string tokenHash, CancellationToken cancellationToken)
    {
        var token = await IdentityCollections.RefreshTokensIn(mongo)
            .Find(candidate => candidate.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken);
        return token?.ToModel();
    }

    public async Task<Account?> FindAccount(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await IdentityCollections.AccountsIn(mongo)
            .Find(candidate => candidate.Id == accountId)
            .FirstOrDefaultAsync(cancellationToken);
        return account?.ToModel();
    }

    public Task<bool> TryRotate(Guid currentId, RefreshToken replacement, DateTimeOffset rotatedAt, CancellationToken cancellationToken) =>
        mongo.InTransaction(async (session, token) =>
        {
            var tokens = IdentityCollections.RefreshTokensIn(mongo);
            var marked = await tokens.UpdateOneAsync(
                session,
                candidate => candidate.Id == currentId && candidate.ReplacedById == null && candidate.RevokedAt == null,
                Builders<MongoRefreshToken>.Update
                    .Set(candidate => candidate.ReplacedById, replacement.Id)
                    .Set(candidate => candidate.RotatedAt, rotatedAt),
                cancellationToken: token);

            if (marked.ModifiedCount != 1)
            {
                return false;
            }

            await tokens.InsertOneAsync(session, MongoRefreshToken.From(replacement), cancellationToken: token);
            return true;
        }, cancellationToken);

    public Task RevokeFamily(Guid familyId, string reason, DateTimeOffset revokedAt, CancellationToken cancellationToken) =>
        IdentityCollections.RevokeFamily(mongo, familyId, reason, revokedAt, cancellationToken);
}

internal sealed class SignOutStore(MongoDatabase mongo) : ISignOutStore
{
    public async Task<RefreshToken?> FindByHash(string tokenHash, CancellationToken cancellationToken)
    {
        var token = await IdentityCollections.RefreshTokensIn(mongo)
            .Find(candidate => candidate.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken);
        return token?.ToModel();
    }

    public Task RevokeFamily(Guid familyId, string reason, DateTimeOffset revokedAt, CancellationToken cancellationToken) =>
        IdentityCollections.RevokeFamily(mongo, familyId, reason, revokedAt, cancellationToken);
}

internal sealed class GetMyAccountStore(MongoDatabase mongo) : IGetMyAccountStore
{
    public async Task<Account?> Find(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await IdentityCollections.AccountsIn(mongo)
            .Find(candidate => candidate.Id == accountId)
            .FirstOrDefaultAsync(cancellationToken);
        return account?.ToModel();
    }
}
