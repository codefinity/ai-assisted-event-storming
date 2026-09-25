using EventStorming.PublicIntegration.Model;
using EventStorming.PublicIntegration.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.PublicIntegration.Slices.RevokeApiKey;

public sealed class RevokeApiKeyCommandHandler(ITeamOwnership ownership, IRevokeApiKeyStore store, IClock clock) : IRevokeApiKeyCommandHandler
{
    public async Task<RevokeApiKeyResult> Handle(RevokeApiKeyCommand command, CancellationToken cancellationToken)
    {
        if (command.Actor.Kind != ActorKind.Account || !await ownership.IsOwner(command.TeamId, command.Actor.Id, cancellationToken))
        {
            return RevokeApiKeyResult.Failed(Failures.Forbidden("Only a team Owner can revoke API keys."));
        }

        var key = await store.Revoke(command.TeamId, command.KeyId, clock.UtcNow, cancellationToken);
        return key is null
            ? RevokeApiKeyResult.Failed(Failures.NotFound("The API key"))
            : RevokeApiKeyResult.Succeeded(ApiKeySummary.From(key));
    }
}
