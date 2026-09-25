using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.ExportBoardDocument;

/// <summary>A board as a Board Document: the same format import accepts, so an export can be re-imported as it is.</summary>
public sealed record ExportBoardDocumentQuery(Actor Actor, Guid BoardId);

public sealed record ExportedBoard(Guid Id, string Name, BoardLevel Level);

/// <summary>
/// <see cref="Key"/> is the element's id. <see cref="Swimlane"/> and <see cref="Boundary"/> say which
/// swimlane and boundary the element sits in on the board, for readers; on re-import the explicit
/// position already puts it there.
/// </summary>
public sealed record ExportedElement(string Key, string Type, string Text, Position Position, Size Size, bool Pivotal, string? Color, string? Swimlane, string? Boundary);

public sealed record ExportedConnection(string From, string To, string? Label);

public sealed record ExportedDocument(int Version, ExportedBoard Board, IReadOnlyList<ExportedElement> Elements, IReadOnlyList<ExportedConnection> Connections);

public sealed class ExportBoardDocumentResult : IUseCaseResult
{
    private ExportBoardDocumentResult(ExportedDocument? document, IReadOnlyList<Failure> failures)
    {
        Document = document;
        Failures = failures;
    }

    public ExportedDocument? Document { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static ExportBoardDocumentResult Succeeded(ExportedDocument document) => new(document, []);

    public static ExportBoardDocumentResult Failed(Failure failure) => new(null, [failure]);
}

public interface IExportBoardDocumentQueryHandler
{
    Task<ExportBoardDocumentResult> Handle(ExportBoardDocumentQuery query, CancellationToken cancellationToken);
}

public interface IExportBoardDocumentStore
{
    Task<Board?> FindBoard(Guid boardId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Element>> Elements(Guid boardId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Connection>> Connections(Guid boardId, CancellationToken cancellationToken);
}
