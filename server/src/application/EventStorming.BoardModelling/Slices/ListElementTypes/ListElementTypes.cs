using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.ListElementTypes;

/// <summary>The notation: every element type and every level, with the palette each level starts with.</summary>
public sealed record ListElementTypesQuery;

public sealed record Notation(IReadOnlyList<ElementType> Types, IReadOnlyList<LevelDescription> Levels);

public sealed class ListElementTypesResult : IUseCaseResult
{
    private ListElementTypesResult(Notation notation)
    {
        Notation = notation;
    }

    public Notation Notation { get; }

    public IReadOnlyList<Failure> Failures => [];

    public bool Success => true;

    public static ListElementTypesResult Succeeded(Notation notation) => new(notation);
}

public interface IListElementTypesQueryHandler
{
    Task<ListElementTypesResult> Handle(ListElementTypesQuery query, CancellationToken cancellationToken);
}
