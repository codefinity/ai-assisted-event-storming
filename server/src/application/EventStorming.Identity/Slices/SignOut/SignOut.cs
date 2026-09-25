using EventStorming.Identity.Model;
using EventStorming.SharedKernel;

namespace EventStorming.Identity.Slices.SignOut;

/// <summary>Ends the session the refresh token belongs to. A missing or unknown token is not an error.</summary>
public sealed record SignOutCommand(string? RefreshToken);

public sealed class SignOutResult : IUseCaseResult
{
    private SignOutResult(IReadOnlyList<Failure> failures) => Failures = failures;

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static SignOutResult Succeeded() => new([]);

    public static SignOutResult Failed(Failure failure) => new([failure]);
}

public interface ISignOutCommandHandler
{
    Task<SignOutResult> Handle(SignOutCommand command, CancellationToken cancellationToken);
}

public interface ISignOutStore
{
    Task<RefreshToken?> FindByHash(string tokenHash, CancellationToken cancellationToken);

    Task RevokeFamily(Guid familyId, string reason, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}
