using EventStorming.Identity.Model;
using EventStorming.SharedKernel;

namespace EventStorming.Identity.Slices.GetMyAccount;

public sealed record GetMyAccountQuery(Actor Actor);

public sealed class GetMyAccountResult : IUseCaseResult
{
    private GetMyAccountResult(AccountSummary? account, IReadOnlyList<Failure> failures)
    {
        Account = account;
        Failures = failures;
    }

    public AccountSummary? Account { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static GetMyAccountResult Succeeded(AccountSummary account) => new(account, []);

    public static GetMyAccountResult Failed(Failure failure) => new(null, [failure]);
}

public interface IGetMyAccountQueryHandler
{
    Task<GetMyAccountResult> Handle(GetMyAccountQuery query, CancellationToken cancellationToken);
}

public interface IGetMyAccountStore
{
    Task<Account?> Find(Guid accountId, CancellationToken cancellationToken);
}
