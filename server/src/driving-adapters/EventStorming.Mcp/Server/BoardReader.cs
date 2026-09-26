using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Slices.ExportBoardDocument;
using EventStorming.BoardModelling.Slices.GetBoardSnapshot;
using EventStorming.SharedKernel;

namespace EventStorming.Mcp.Server;

/// <summary>
/// A board as an agent reads it: the snapshot gives ids, versions and arrows; the export says which
/// swimlane and boundary each element sits in, which a model would otherwise have to work out from
/// rectangles.
/// </summary>
internal static class BoardReader
{
    public static async Task<(BoardView? View, IReadOnlyList<Failure> Failures)> Read(
        Actor actor,
        Guid boardId,
        IGetBoardSnapshotQueryHandler snapshots,
        IExportBoardDocumentQueryHandler exports,
        McpAdapterOptions options,
        CancellationToken cancellationToken)
    {
        var snapshot = await snapshots.Handle(new GetBoardSnapshotQuery(actor, boardId), cancellationToken);
        if (!snapshot.Success)
        {
            return (null, snapshot.Failures);
        }

        var export = await exports.Handle(new ExportBoardDocumentQuery(actor, boardId), cancellationToken);
        var membership = export.Success
            ? export.Document!.Elements.ToDictionary(element => element.Key, element => (Swimlane: IdOf(element.Swimlane), Boundary: IdOf(element.Boundary)), StringComparer.OrdinalIgnoreCase)
            : [];

        var board = snapshot.Snapshot!;
        var elements = board.Elements
            .OrderBy(element => element.X).ThenBy(element => element.Y)
            .Select(element => membership.TryGetValue(element.Id.ToString(), out var sits)
                ? Views.Of(element, sits.Swimlane, sits.Boundary)
                : Views.Of(element))
            .ToList();

        return (new BoardView(
            Views.Link(board.Board, options),
            board.Board.Revision,
            board.Permission == BoardPermission.Edit && board.Board.ArchivedAt is null,
            board.Board.ArchivedAt is not null,
            elements,
            board.Connections.Select(Views.Of).ToList()), []);
    }

    private static Guid? IdOf(string? key) => Guid.TryParse(key, out var id) ? id : null;
}
