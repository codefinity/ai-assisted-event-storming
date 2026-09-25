using EventStorming.Api.Rest.Http;
using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Slices.ArchiveBoard;
using EventStorming.BoardModelling.Slices.CreateBoard;
using EventStorming.BoardModelling.Slices.DuplicateBoard;
using EventStorming.BoardModelling.Slices.ExportBoardDocument;
using EventStorming.BoardModelling.Slices.GetBoardSnapshot;
using EventStorming.BoardModelling.Slices.ImportBoardDocument;
using EventStorming.BoardModelling.Slices.ListBoards;
using EventStorming.BoardModelling.Slices.ListElementTypes;
using EventStorming.BoardModelling.Slices.RenameBoard;
using EventStorming.BoardModelling.Slices.RestoreBoard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace EventStorming.Api.Rest.App.Boards;

public sealed record CreateBoardRequest(string? Name, string? Level);

public sealed record RenameBoardRequest(string? Name);

public sealed record DuplicateBoardRequest(string? Name);

/// <param name="CanEdit">Whether the caller may create and change boards in this team.</param>
public sealed record BoardListResponse(IReadOnlyList<BoardResponse> Items, string? NextCursor, bool CanEdit);

/// <summary>The same flat shape the board hub broadcasts, so the web app handles one element type.</summary>
public sealed record AppElementResponse(
    Guid Id,
    string Type,
    string Text,
    double X,
    double Y,
    double Width,
    double Height,
    bool Pivotal,
    string? Color,
    long Version,
    DateTimeOffset UpdatedAt,
    ActorResponse UpdatedBy)
{
    public static AppElementResponse From(Element element) => new(
        element.Id, element.Type, element.Text, element.X, element.Y, element.Width, element.Height,
        element.Pivotal, element.Color, element.Version, element.UpdatedAt, ActorResponse.From(element.UpdatedBy));
}

public sealed record AppConnectionResponse(Guid Id, Guid From, Guid To, string? Label, long Version)
{
    public static AppConnectionResponse Of(Connection connection) => new(connection.Id, connection.From, connection.To, connection.Label, connection.Version);
}

/// <param name="Permission">"view" or "edit".</param>
public sealed record BoardSnapshotResponse(BoardResponse Board, string Permission, IReadOnlyList<AppElementResponse> Elements, IReadOnlyList<AppConnectionResponse> Connections);

internal static class BoardEndpoints
{
    public static void Map(RouteGroupBuilder app)
    {
        var boards = app.MapGroup(string.Empty).WithTags("Boards");

        boards.MapGet("/teams/{teamId:guid}/boards", async Task<Results<Ok<BoardListResponse>, ProblemHttpResult>> (
                Guid teamId, bool? includeArchived, string? cursor, int? limit, IListBoardsQueryHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new ListBoardsQuery(Actors.From(http.User), teamId, includeArchived ?? false, cursor, limit), cancellationToken);
                return result.Success
                    ? TypedResults.Ok(new BoardListResponse(result.Page!.Items.Select(BoardResponse.From).ToList(), result.Page.NextCursor, result.Permission == BoardPermission.Edit))
                    : Problems.From(result, http);
            })
            .WithName("ListTeamBoards").WithSummary("The team dashboard: boards, most recently changed first");

        boards.MapPost("/teams/{teamId:guid}/boards", async Task<Results<Created<BoardResponse>, ProblemHttpResult>> (
                Guid teamId, CreateBoardRequest request, ICreateBoardCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new CreateBoardCommand(Actors.From(http.User), teamId, request.Name ?? string.Empty, request.Level ?? string.Empty), cancellationToken);
                return result.Success ? TypedResults.Created($"/api/app/boards/{result.Board!.Id}", BoardResponse.From(result.Board)) : Problems.From(result, http);
            })
            .WithName("CreateTeamBoard").WithSummary("Create an empty board at a level");

        boards.MapPost("/teams/{teamId:guid}/boards/import", async Task<Results<Created<ImportedBoardResponse>, ProblemHttpResult>> (
                Guid teamId, BoardDocumentDto document, IImportBoardDocumentCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new ImportBoardDocumentCommand(Actors.From(http.User), document.ToModel(), TeamId: teamId), cancellationToken);
                return result.Success
                    ? TypedResults.Created($"/api/app/boards/{result.Imported!.Board.Id}", ImportedBoardResponse.From(result.Imported))
                    : Problems.From(result, http);
            })
            .WithName("ImportTeamBoard").WithSummary("Create a board from a Board Document (JSON)");

        boards.MapGet("/boards/{boardId:guid}", async Task<Results<Ok<BoardSnapshotResponse>, ProblemHttpResult>> (
                Guid boardId, IGetBoardSnapshotQueryHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new GetBoardSnapshotQuery(Actors.From(http.User), boardId), cancellationToken);
                if (!result.Success)
                {
                    return Problems.From(result, http);
                }

                var snapshot = result.Snapshot!;
                return TypedResults.Ok(new BoardSnapshotResponse(
                    BoardResponse.From(snapshot.Board),
                    snapshot.Permission == BoardPermission.Edit ? "edit" : "view",
                    snapshot.Elements.Select(AppElementResponse.From).ToList(),
                    snapshot.Connections.Select(AppConnectionResponse.Of).ToList()));
            })
            .WithName("GetBoardSnapshot").WithSummary("Everything needed to open a board");

        boards.MapPatch("/boards/{boardId:guid}", async Task<Results<Ok<BoardResponse>, ProblemHttpResult>> (
                Guid boardId, RenameBoardRequest request, IRenameBoardCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new RenameBoardCommand(Actors.From(http.User), boardId, request.Name ?? string.Empty), cancellationToken);
                return result.Success ? TypedResults.Ok(BoardResponse.From(result.Board!)) : Problems.From(result, http);
            })
            .WithName("RenameTeamBoard").WithSummary("Rename a board");

        boards.MapPost("/boards/{boardId:guid}/duplicate", async Task<Results<Created<BoardResponse>, ProblemHttpResult>> (
                Guid boardId, DuplicateBoardRequest? request, IDuplicateBoardCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new DuplicateBoardCommand(Actors.From(http.User), boardId, request?.Name), cancellationToken);
                return result.Success ? TypedResults.Created($"/api/app/boards/{result.Board!.Id}", BoardResponse.From(result.Board)) : Problems.From(result, http);
            })
            .WithName("DuplicateBoard").WithSummary("Copy a board and all of its content");

        boards.MapPost("/boards/{boardId:guid}/archive", async Task<Results<Ok<BoardResponse>, ProblemHttpResult>> (
                Guid boardId, IArchiveBoardCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new ArchiveBoardCommand(Actors.From(http.User), boardId), cancellationToken);
                return result.Success ? TypedResults.Ok(BoardResponse.From(result.Board!)) : Problems.From(result, http);
            })
            .WithName("ArchiveTeamBoard").WithSummary("Archive a board (read-only, hidden from the dashboard)");

        boards.MapPost("/boards/{boardId:guid}/restore", async Task<Results<Ok<BoardResponse>, ProblemHttpResult>> (
                Guid boardId, IRestoreBoardCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new RestoreBoardCommand(Actors.From(http.User), boardId), cancellationToken);
                return result.Success ? TypedResults.Ok(BoardResponse.From(result.Board!)) : Problems.From(result, http);
            })
            .WithName("RestoreTeamBoard").WithSummary("Bring an archived board back");

        boards.MapGet("/boards/{boardId:guid}/document", async Task<Results<Ok<ExportedDocumentDto>, ProblemHttpResult>> (
                Guid boardId, IExportBoardDocumentQueryHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new ExportBoardDocumentQuery(Actors.From(http.User), boardId), cancellationToken);
                return result.Success ? TypedResults.Ok(ExportedDocumentDto.From(result.Document!)) : Problems.From(result, http);
            })
            .WithName("ExportTeamBoard").WithSummary("Export a board as a Board Document (JSON)");

        boards.MapPut("/boards/{boardId:guid}/document", async Task<Results<Ok<ImportedBoardResponse>, ProblemHttpResult>> (
                Guid boardId, BoardDocumentDto document, IImportBoardDocumentCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new ImportBoardDocumentCommand(Actors.From(http.User), document.ToModel(), BoardId: boardId), cancellationToken);
                return result.Success ? TypedResults.Ok(ImportedBoardResponse.From(result.Imported!)) : Problems.From(result, http);
            })
            .WithName("ReplaceTeamBoardContents").WithSummary("Replace a board's entire content from a Board Document");

        boards.MapGet("/element-types", async Task<Ok<NotationResponse>> (IListElementTypesQueryHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(NotationResponse.From((await handler.Handle(new ListElementTypesQuery(), cancellationToken)).Notation)))
            .WithTags("Notation")
            .WithName("ListAppElementTypes").WithSummary("The element types and levels");
    }
}
