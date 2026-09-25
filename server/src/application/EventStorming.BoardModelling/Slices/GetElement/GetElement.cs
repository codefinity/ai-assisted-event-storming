using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.GetElement;

public sealed record GetElementQuery(Actor Actor, Guid BoardId, Guid ElementId);

public sealed class GetElementResult : IUseCaseResult
{
    private GetElementResult(Element? element, IReadOnlyList<Failure> failures)
    {
        Element = element;
        Failures = failures;
    }

    public Element? Element { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static GetElementResult Succeeded(Element element) => new(element, []);

    public static GetElementResult Failed(Failure failure) => new(null, [failure]);
}

public interface IGetElementQueryHandler
{
    Task<GetElementResult> Handle(GetElementQuery query, CancellationToken cancellationToken);
}

public interface IGetElementStore
{
    Task<Element?> Find(Guid boardId, Guid elementId, CancellationToken cancellationToken);
}
