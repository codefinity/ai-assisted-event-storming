using EventStorming.BoardModelling.Shared;

namespace EventStorming.BoardModelling.Slices.ListElementTypes;

public sealed class ListElementTypesQueryHandler(IElementTypeRegistry registry) : IListElementTypesQueryHandler
{
    public Task<ListElementTypesResult> Handle(ListElementTypesQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(ListElementTypesResult.Succeeded(new Notation(registry.Types, registry.Levels)));
}
