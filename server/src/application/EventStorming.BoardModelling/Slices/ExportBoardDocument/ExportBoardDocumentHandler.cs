using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Slices.ExportBoardDocument;

public sealed class ExportBoardDocumentQueryHandler(
    IBoardAccess access,
    IElementTypeRegistry registry,
    IExportBoardDocumentStore store) : IExportBoardDocumentQueryHandler
{
    public async Task<ExportBoardDocumentResult> Handle(ExportBoardDocumentQuery query, CancellationToken cancellationToken)
    {
        if (BoardRules.RequireView(await access.ForBoard(query.BoardId, query.Actor, cancellationToken)) is { } refused)
        {
            return ExportBoardDocumentResult.Failed(refused);
        }

        var board = await store.FindBoard(query.BoardId, cancellationToken);
        if (board is null)
        {
            return ExportBoardDocumentResult.Failed(Failures.NotFound("The board"));
        }

        var elements = await store.Elements(board.Id, cancellationToken);
        var connections = await store.Connections(board.Id, cancellationToken);

        LayoutRole RoleOf(Element element) => registry.Find(element.Type)?.LayoutRole ?? LayoutRole.Item;

        var lanes = elements.Where(element => RoleOf(element) == LayoutRole.Lane).OrderBy(element => element.Y).ToList();
        var boundaries = elements.Where(element => RoleOf(element) == LayoutRole.Boundary).OrderBy(element => element.X).ToList();
        var items = elements.Where(element => RoleOf(element) == LayoutRole.Item).OrderBy(element => element.X).ThenBy(element => element.Y).ToList();

        // Structures first, then the timeline in reading order: left to right, top to bottom.
        var exported = lanes.Concat(boundaries).Concat(items)
            .Select(element => new ExportedElement(
                element.Id.ToString(),
                element.Type,
                element.Text,
                new Position(element.X, element.Y),
                new Size(element.Width, element.Height),
                element.Pivotal,
                element.Color,
                RoleOf(element) == LayoutRole.Item ? SmallestContaining(element, lanes)?.Id.ToString() : null,
                RoleOf(element) == LayoutRole.Item ? SmallestContaining(element, boundaries)?.Id.ToString() : null))
            .ToList();

        return ExportBoardDocumentResult.Succeeded(new ExportedDocument(
            1,
            new ExportedBoard(board.Id, board.Name, board.Level),
            exported,
            connections.Select(connection => new ExportedConnection(connection.From.ToString(), connection.To.ToString(), connection.Label)).ToList()));
    }

    private static Element? SmallestContaining(Element element, IReadOnlyList<Element> areas)
    {
        var centerX = element.X + (element.Width / 2);
        var centerY = element.Y + (element.Height / 2);
        return areas
            .Where(area => centerX >= area.X && centerX <= area.X + area.Width && centerY >= area.Y && centerY <= area.Y + area.Height)
            .OrderBy(area => area.Width * area.Height)
            .FirstOrDefault();
    }
}
