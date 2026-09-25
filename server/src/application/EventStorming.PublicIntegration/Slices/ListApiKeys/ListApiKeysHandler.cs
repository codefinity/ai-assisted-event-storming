using EventStorming.PublicIntegration.Model;
using EventStorming.PublicIntegration.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.PublicIntegration.Slices.ListApiKeys;

public sealed class ListApiKeysQueryHandler(ITeamOwnership ownership, IListApiKeysStore store) : IListApiKeysQueryHandler
{
    public async Task<ListApiKeysResult> Handle(ListApiKeysQuery query, CancellationToken cancellationToken)
    {
        if (query.Actor.Kind != ActorKind.Account || !await ownership.IsOwner(query.TeamId, query.Actor.Id, cancellationToken))
        {
            return ListApiKeysResult.Failed(Failures.Forbidden("Only a team Owner can see its API keys."));
        }

        var keys = await store.KeysOf(query.TeamId, cancellationToken);
        return ListApiKeysResult.Succeeded(keys.OrderByDescending(key => key.CreatedAt).Select(ApiKeySummary.From).ToList());
    }
}
