using EventStorming.Identity.Model;
using EventStorming.Identity.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.Identity.Slices.RefreshSession;

public sealed class RefreshSessionCommandHandler(
    IRefreshSessionStore store,
    IAccessTokenIssuer accessTokenIssuer,
    IRefreshTokenGenerator refreshTokenGenerator,
    IClock clock) : IRefreshSessionCommandHandler
{
    public async Task<RefreshSessionResult> Handle(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return RefreshSessionResult.Failed(SessionExpired);
        }

        var now = clock.UtcNow;
        var current = await store.FindByHash(refreshTokenGenerator.HashOf(command.RefreshToken), cancellationToken);
        if (current is null || current.RevokedAt is not null || current.ExpiresAt <= now)
        {
            return RefreshSessionResult.Failed(SessionExpired);
        }

        if (current.ReplacedById is not null)
        {
            if (current.RotatedAt is { } rotatedAt && now - rotatedAt <= SessionPolicy.RotationGracePeriod)
            {
                return RefreshSessionResult.Failed(RotatedConcurrently);
            }

            // A token that was already rotated is being replayed: assume it was stolen and end every
            // session descended from the same sign-in.
            await store.RevokeFamily(current.FamilyId, "reuse-detected", now, cancellationToken);
            return RefreshSessionResult.Failed(SessionExpired);
        }

        var account = await store.FindAccount(current.AccountId, cancellationToken);
        if (account is null)
        {
            return RefreshSessionResult.Failed(SessionExpired);
        }

        var refresh = refreshTokenGenerator.Generate();
        var replacement = new RefreshToken(
            Guid.CreateVersion7(),
            account.Id,
            current.FamilyId,
            refresh.Hash,
            now,
            now + SessionPolicy.RefreshTokenLifetime);

        if (!await store.TryRotate(current.Id, replacement, now, cancellationToken))
        {
            return RefreshSessionResult.Failed(RotatedConcurrently);
        }

        var summary = new AccountSummary(account.Id, account.Email, account.DisplayName);
        var access = accessTokenIssuer.Issue(summary);
        return RefreshSessionResult.Succeeded(new SessionTokens(access.Value, access.ExpiresAt, refresh.Value, replacement.ExpiresAt, summary));
    }

    private static readonly Failure SessionExpired = new(
        FailureKind.Unauthenticated,
        "session-expired",
        "The session has ended.",
        Fix: "Sign in again.");

    private static readonly Failure RotatedConcurrently = new(
        FailureKind.Unauthenticated,
        "session-rotated",
        "This session was refreshed by another request a moment ago.",
        Fix: "Retry the refresh once; the newer session cookie is already in place.");
}
