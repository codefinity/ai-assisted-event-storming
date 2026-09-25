using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.ImportBoardDocument;

/// <summary>
/// A whole board described in one document - the Board Document format of the public API. Array order
/// is timeline order; positions are optional and anything without one is laid out automatically.
/// </summary>
public sealed record BoardDocument(
    int? Version,
    DocumentBoard? Board,
    IReadOnlyList<DocumentElement>? Elements,
    IReadOnlyList<DocumentConnection>? Connections);

public sealed record DocumentBoard(string? Name = null, string? Level = null);

public sealed record DocumentElement(
    string? Type,
    string? Text = null,
    string? Key = null,
    Position? Position = null,
    Size? Size = null,
    string? Swimlane = null,
    string? Boundary = null,
    string? Anchor = null,
    bool? Pivotal = null,
    string? Color = null);

public sealed record DocumentConnection(string? From, string? To, string? Label = null);

/// <summary>
/// With <see cref="TeamId"/> the document becomes a new board of that team; with <see cref="BoardId"/>
/// it replaces that board's entire content. Exactly one of the two is given.
/// </summary>
public sealed record ImportBoardDocumentCommand(Actor Actor, BoardDocument Document, Guid? TeamId = null, Guid? BoardId = null);

/// <summary><see cref="KeyedIds"/> maps each key used in the document to the id its element was given.</summary>
public sealed record ImportedBoard(Board Board, IReadOnlyDictionary<string, Guid> KeyedIds, int ElementCount, int ConnectionCount);

public sealed class ImportBoardDocumentResult : IUseCaseResult
{
    private ImportBoardDocumentResult(ImportedBoard? imported, IReadOnlyList<Failure> failures)
    {
        Imported = imported;
        Failures = failures;
    }

    public ImportedBoard? Imported { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static ImportBoardDocumentResult Succeeded(ImportedBoard imported) => new(imported, []);

    public static ImportBoardDocumentResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static ImportBoardDocumentResult Failed(Failure failure) => new(null, [failure]);
}

public interface IImportBoardDocumentCommandHandler
{
    Task<ImportBoardDocumentResult> Handle(ImportBoardDocumentCommand command, CancellationToken cancellationToken);
}

public interface IImportBoardDocumentStore
{
    Task<Board?> FindBoard(Guid boardId, CancellationToken cancellationToken);

    /// <summary>Stores a new board with all of its content in one transaction.</summary>
    Task InsertNew(Board board, IReadOnlyList<Element> elements, IReadOnlyList<Connection> connections, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes every element and connection of the board and stores the given ones instead, in one
    /// transaction, renaming the board if <paramref name="newName"/> is given. Returns the board as it now is.
    /// </summary>
    Task<Board?> ReplaceContents(Guid boardId, string? newName, IReadOnlyList<Element> elements, IReadOnlyList<Connection> connections, ActorRef by, DateTimeOffset at, CancellationToken cancellationToken);
}
