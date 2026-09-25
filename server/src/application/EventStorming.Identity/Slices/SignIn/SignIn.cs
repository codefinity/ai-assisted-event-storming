using EventStorming.Identity.Model;
using EventStorming.SharedKernel;

namespace EventStorming.Identity.Slices.SignIn;

public sealed record SignInCommand(string Email, string Password);

public sealed class SignInResult : IUseCaseResult
{
    private SignInResult(SessionTokens? session, IReadOnlyList<Failure> failures)
    {
        Session = session;
        Failures = failures;
    }

    public SessionTokens? Session { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static SignInResult Succeeded(SessionTokens session) => new(session, []);

    public static SignInResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static SignInResult Failed(Failure failure) => new(null, [failure]);
}

public interface ISignInCommandHandler
{
    Task<SignInResult> Handle(SignInCommand command, CancellationToken cancellationToken);
}

public interface ISignInStore
{
    Task<Account?> FindByEmail(string normalizedEmail, CancellationToken cancellationToken);

    Task Save(RefreshToken refreshToken, CancellationToken cancellationToken);
}
