using EventStorming.Identity.Model;
using EventStorming.SharedKernel;

namespace EventStorming.Identity.Slices.RefreshSession;

public sealed record RefreshSessionCommand(string RefreshToken);

public sealed class RefreshSessionResult : IUseCaseResult
{
    private RefreshSessionResult(SessionTokens? session, IReadOnlyList<Failure> failures)
    {
        Session = session;
        Failures = failures;
    }

    public SessionTokens? Session { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static RefreshSessionResult Succeeded(SessionTokens session) => new(session, []);

    public static RefreshSessionResult Failed(Failure failure) => new(null, [failure]);
}

public interface IRefreshSessionCommandHandler
{
    Task<RefreshSessionResult> Handle(RefreshSessionCommand command, CancellationToken cancellationToken);
}

public interface IRefreshSessionStore
{
    Task<RefreshToken?> FindByHash(string tokenHash, CancellationToken cancellationToken);

    Task<Account?> FindAccount(Guid accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically marks <paramref name="currentId"/> as replaced and stores its replacement - but only
    /// if it has not already been rotated or revoked. False means another request won the race.
    /// </summary>
    Task<bool> TryRotate(Guid currentId, RefreshToken replacement, DateTimeOffset rotatedAt, CancellationToken cancellationToken);

    Task RevokeFamily(Guid familyId, string reason, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}
