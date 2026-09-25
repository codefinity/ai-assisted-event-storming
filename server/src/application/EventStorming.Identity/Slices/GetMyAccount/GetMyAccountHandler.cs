using EventStorming.Identity.Model;
using EventStorming.SharedKernel;

namespace EventStorming.Identity.Slices.GetMyAccount;

public sealed class GetMyAccountQueryHandler(IGetMyAccountStore store) : IGetMyAccountQueryHandler
{
    public async Task<GetMyAccountResult> Handle(GetMyAccountQuery query, CancellationToken cancellationToken)
    {
        if (query.Actor.Kind != ActorKind.Account)
        {
            return GetMyAccountResult.Failed(Failures.Forbidden("Only a signed-in person has an account; an API key does not."));
        }

        var account = await store.Find(query.Actor.Id, cancellationToken);
        return account is null
            ? GetMyAccountResult.Failed(Failures.NotFound("The account", "Sign in again."))
            : GetMyAccountResult.Succeeded(new AccountSummary(account.Id, account.Email, account.DisplayName));
    }
}
