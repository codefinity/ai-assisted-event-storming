using EventStorming.Identity.Model;
using EventStorming.SharedKernel;

namespace EventStorming.Identity.Slices.RegisterAccount;

public sealed record RegisterAccountCommand(string Email, string DisplayName, string Password);

public sealed class RegisterAccountResult : IUseCaseResult
{
    private RegisterAccountResult(AccountSummary? account, IReadOnlyList<Failure> failures)
    {
        Account = account;
        Failures = failures;
    }

    public AccountSummary? Account { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static RegisterAccountResult Succeeded(AccountSummary account) => new(account, []);

    public static RegisterAccountResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static RegisterAccountResult Failed(Failure failure) => new(null, [failure]);
}

public interface IRegisterAccountCommandHandler
{
    Task<RegisterAccountResult> Handle(RegisterAccountCommand command, CancellationToken cancellationToken);
}

public interface IRegisterAccountStore
{
    /// <summary>Stores the account unless its email is taken; false means it was taken.</summary>
    Task<bool> TryInsert(Account account, CancellationToken cancellationToken);
}
