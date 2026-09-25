using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.GetElement;

public sealed class GetElementQueryHandler(IBoardAccess access, IGetElementStore store) : IGetElementQueryHandler
{
    public async Task<GetElementResult> Handle(GetElementQuery query, CancellationToken cancellationToken)
    {
        if (BoardRules.RequireView(await access.ForBoard(query.BoardId, query.Actor, cancellationToken)) is { } refused)
        {
            return GetElementResult.Failed(refused);
        }

        var element = await store.Find(query.BoardId, query.ElementId, cancellationToken);
        return element is null
            ? GetElementResult.Failed(Failures.NotFound("The element"))
            : GetElementResult.Succeeded(element);
    }
}
