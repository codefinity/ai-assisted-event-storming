using EventStorming.Identity.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.Identity.Slices.SignOut;

public sealed class SignOutCommandHandler(
    ISignOutStore store,
    IRefreshTokenGenerator refreshTokenGenerator,
    IClock clock) : ISignOutCommandHandler
{
    public async Task<SignOutResult> Handle(SignOutCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return SignOutResult.Succeeded();
        }

        var token = await store.FindByHash(refreshTokenGenerator.HashOf(command.RefreshToken), cancellationToken);
        if (token is not null && token.RevokedAt is null)
        {
            await store.RevokeFamily(token.FamilyId, "sign-out", clock.UtcNow, cancellationToken);
        }

        return SignOutResult.Succeeded();
    }
}
