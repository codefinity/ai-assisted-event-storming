using EventStorming.Api.Rest.Http;
using EventStorming.BoardModelling.Slices.AddConnection;
using EventStorming.BoardModelling.Slices.AddElements;
using EventStorming.BoardModelling.Slices.ArchiveBoard;
using EventStorming.BoardModelling.Slices.CreateBoard;
using EventStorming.BoardModelling.Slices.DeleteConnections;
using EventStorming.BoardModelling.Slices.DeleteElements;
using EventStorming.BoardModelling.Slices.ExportBoardDocument;
using EventStorming.BoardModelling.Slices.GetBoard;
using EventStorming.BoardModelling.Slices.GetElement;
using EventStorming.BoardModelling.Slices.ImportBoardDocument;
using EventStorming.BoardModelling.Slices.ListBoards;
using EventStorming.BoardModelling.Slices.ListConnections;
using EventStorming.BoardModelling.Slices.ListElements;
using EventStorming.BoardModelling.Slices.ListElementTypes;
using EventStorming.BoardModelling.Slices.RenameBoard;
using EventStorming.BoardModelling.Slices.RestoreBoard;
using EventStorming.BoardModelling.Slices.UpdateElement;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace EventStorming.Api.Rest.V1;

/// <summary>
/// The public API, /api/v1: the Open Host Service over Board Modelling. It calls the same use cases as
/// the web app; this file only translates the published resources to and from their commands.
/// An API key belongs to one team, so boards are addressed by id alone.
/// </summary>
internal static class PublicEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/", () => TypedResults.Ok(new ApiIndexResponse(
                "EventStorming public API", "v1", "/docs", "/docs/llm-guide.md",
                "/api/v1/schemas/board-document.json", "/api/v1/element-types", "/api/v1/boards")))
            .AllowAnonymous()
            .WithTags("About")
            .WithName("ApiIndex").WithSummary("Where to start");

        api.MapGet("/element-types", async Task<Ok<NotationResponse>> (IListElementTypesQueryHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(NotationResponse.From((await handler.Handle(new ListElementTypesQuery(), cancellationToken)).Notation)))
            .WithTags("Element types")
            .WithName("ListElementTypes").WithSummary("Every element type, with when to use it, and each level's palette");

        MapBoards(api.MapGroup("/boards").WithTags("Boards"));
        MapElements(api.MapGroup("/boards/{boardId:guid}/elements").WithTags("Elements"));
        MapConnections(api.MapGroup("/boards/{boardId:guid}/connections").WithTags("Connections"));
    }

    private static void MapBoards(RouteGroupBuilder boards)
    {
        boards.MapGet("/", async Task<Results<Ok<PageResponse<PublicBoard>>, ProblemHttpResult>> (
                bool? includeArchived, string? cursor, int? limit, IListBoardsQueryHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var actor = Actors.From(http.User);
                var result = await handler.Handle(new ListBoardsQuery(actor, actor.TeamId!.Value, includeArchived ?? false, cursor, limit), cancellationToken);
                return result.Success
                    ? TypedResults.Ok(new PageResponse<PublicBoard>(result.Page!.Items.Select(PublicBoard.From).ToList(), result.Page.NextCursor))
                    : Problems.From(result, http);
            })
            .WithName("ListBoards").WithSummary("The team's boards, most recently changed first (paged)");

        boards.MapPost("/", async Task<Results<Created<PublicBoard>, ProblemHttpResult>> (
                CreatePublicBoardRequest request, ICreateBoardCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var actor = Actors.From(http.User);
                var result = await handler.Handle(new CreateBoardCommand(actor, actor.TeamId!.Value, request.Name ?? string.Empty, request.Level ?? string.Empty), cancellationToken);
                return result.Success ? TypedResults.Created($"/api/v1/boards/{result.Board!.Id}", PublicBoard.From(result.Board)) : Problems.From(result, http);
            })
            .RequireAuthorization(ApiKeyAuthenticationHandler.WritePolicy)
            .WithName("CreateBoard").WithSummary("Create an empty board");

        boards.MapPost("/import", async Task<Results<Created<ImportedBoardResponse>, ProblemHttpResult>> (
                BoardDocumentDto document, IImportBoardDocumentCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var actor = Actors.From(http.User);
                var result = await handler.Handle(new ImportBoardDocumentCommand(actor, document.ToModel(), TeamId: actor.TeamId), cancellationToken);
                return result.Success
                    ? TypedResults.Created($"/api/v1/boards/{result.Imported!.Board.Id}", ImportedBoardResponse.From(result.Imported))
                    : Problems.From(result, http);
            })
            .RequireAuthorization(ApiKeyAuthenticationHandler.WritePolicy)
            .WithName("ImportBoard").WithSummary("Create a board, with all its content, from one Board Document (declarative)");

        boards.MapGet("/{boardId:guid}", async Task<Results<Ok<PublicBoard>, ProblemHttpResult>> (
                Guid boardId, IGetBoardQueryHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new GetBoardQuery(Actors.From(http.User), boardId), cancellationToken);
                return result.Success ? TypedResults.Ok(PublicBoard.From(result.Board!)) : Problems.From(result, http);
            })
            .WithName("GetBoard").WithSummary("A board's details");

        boards.MapPatch("/{boardId:guid}", async Task<Results<Ok<PublicBoard>, ProblemHttpResult>> (
                Guid boardId, RenamePublicBoardRequest request, IRenameBoardCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new RenameBoardCommand(Actors.From(http.User), boardId, request.Name ?? string.Empty), cancellationToken);
                return result.Success ? TypedResults.Ok(PublicBoard.From(result.Board!)) : Problems.From(result, http);
            })
            .RequireAuthorization(ApiKeyAuthenticationHandler.WritePolicy)
            .WithName("RenameBoard").WithSummary("Rename a board");

        boards.MapDelete("/{boardId:guid}", async Task<Results<Ok<PublicBoard>, ProblemHttpResult>> (
                Guid boardId, IArchiveBoardCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new ArchiveBoardCommand(Actors.From(http.User), boardId), cancellationToken);
                return result.Success ? TypedResults.Ok(PublicBoard.From(result.Board!)) : Problems.From(result, http);
            })
            .RequireAuthorization(ApiKeyAuthenticationHandler.WritePolicy)
            .WithName("ArchiveBoard").WithSummary("Archive a board (nothing is deleted; restore it with POST /restore)");

        boards.MapPost("/{boardId:guid}/restore", async Task<Results<Ok<PublicBoard>, ProblemHttpResult>> (
                Guid boardId, IRestoreBoardCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new RestoreBoardCommand(Actors.From(http.User), boardId), cancellationToken);
                return result.Success ? TypedResults.Ok(PublicBoard.From(result.Board!)) : Problems.From(result, http);
            })
            .RequireAuthorization(ApiKeyAuthenticationHandler.WritePolicy)
            .WithName("RestoreBoard").WithSummary("Restore an archived board");

        boards.MapGet("/{boardId:guid}/document", async Task<Results<Ok<ExportedDocumentDto>, ProblemHttpResult>> (
                Guid boardId, IExportBoardDocumentQueryHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new ExportBoardDocumentQuery(Actors.From(http.User), boardId), cancellationToken);
                return result.Success ? TypedResults.Ok(ExportedDocumentDto.From(result.Document!)) : Problems.From(result, http);
            })
            .WithName("ExportBoard").WithSummary("The whole board as a Board Document");

        boards.MapPut("/{boardId:guid}/document", async Task<Results<Ok<ImportedBoardResponse>, ProblemHttpResult>> (
                Guid boardId, BoardDocumentDto document, IImportBoardDocumentCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new ImportBoardDocumentCommand(Actors.From(http.User), document.ToModel(), BoardId: boardId), cancellationToken);
                return result.Success ? TypedResults.Ok(ImportedBoardResponse.From(result.Imported!)) : Problems.From(result, http);
            })
            .RequireAuthorization(ApiKeyAuthenticationHandler.WritePolicy)
            .WithName("ReplaceBoardContents").WithSummary("Replace a board's entire content with one Board Document (declarative)");
    }

    private static void MapElements(RouteGroupBuilder elements)
    {
        elements.MapGet("/", async Task<Results<Ok<PageResponse<PublicElement>>, ProblemHttpResult>> (
                Guid boardId, string? type, string? cursor, int? limit, IListElementsQueryHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new ListElementsQuery(Actors.From(http.User), boardId, type, cursor, limit), cancellationToken);
                return result.Success
                    ? TypedResults.Ok(new PageResponse<PublicElement>(result.Page!.Items.Select(PublicElement.From).ToList(), result.Page.NextCursor))
                    : Problems.From(result, http);
            })
            .WithName("ListElements").WithSummary("A board's elements (paged), optionally of one type");

        elements.MapPost("/", async Task<Results<Created<PublicElement>, ProblemHttpResult>> (
                Guid boardId, CreateElementRequest request, IAddElementsCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(
                    new AddElementsCommand(
                        Actors.From(http.User),
                        boardId,
                        [new NewElement(request.Type, request.Text, Position: request.Position?.ToModel(), Size: request.Size?.ToModel(),
                            Swimlane: request.Swimlane, Boundary: request.Boundary, Anchor: request.Anchor, Pivotal: request.Pivotal ?? false, Color: request.Color)]),
                    cancellationToken);
                if (!result.Success)
                {
                    return Problems.From(FailurePaths.Unbatch(result.Failures, "elements[0]."), http);
                }

                var element = result.Added!.Changes.Elements[0];
                return TypedResults.Created($"/api/v1/boards/{boardId}/elements/{element.Id}", PublicElement.From(element));
            })
            .RequireAuthorization(ApiKeyAuthenticationHandler.WritePolicy)
            .WithName("CreateElement").WithSummary("Add one element");

        elements.MapPost("/bulk", async Task<Results<Created<BulkElementsResponse>, ProblemHttpResult>> (
                Guid boardId, BulkElementsRequest request, IAddElementsCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(
                    new AddElementsCommand(
                        Actors.From(http.User),
                        boardId,
                        (request.Elements ?? []).Select(element => new NewElement(
                            element?.Type, element?.Text, Key: element?.Key, Position: element?.Position?.ToModel(), Size: element?.Size?.ToModel(),
                            Swimlane: element?.Swimlane, Boundary: element?.Boundary, Anchor: element?.Anchor, Pivotal: element?.Pivotal ?? false, Color: element?.Color)).ToList(),
                        (request.Connections ?? []).Select(connection => new NewConnection(connection?.From, connection?.To, connection?.Label)).ToList()),
                    cancellationToken);
                if (!result.Success)
                {
                    return Problems.From(result, http);
                }

                var changes = result.Added!.Changes;
                return TypedResults.Created(
                    $"/api/v1/boards/{boardId}/elements",
                    new BulkElementsResponse(changes.Elements.Select(PublicElement.From).ToList(), changes.Connections.Select(PublicConnection.Of).ToList(), result.Added.KeyedIds));
            })
            .RequireAuthorization(ApiKeyAuthenticationHandler.WritePolicy)
            .WithName("AddElementsInBulk").WithSummary("Add up to 500 elements and their connections in one request; positions optional");

        elements.MapGet("/{elementId:guid}", async Task<Results<Ok<PublicElement>, ProblemHttpResult>> (
                Guid boardId, Guid elementId, IGetElementQueryHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new GetElementQuery(Actors.From(http.User), boardId, elementId), cancellationToken);
                return result.Success ? TypedResults.Ok(PublicElement.From(result.Element!)) : Problems.From(result, http);
            })
            .WithName("GetElement").WithSummary("One element");

        elements.MapPatch("/{elementId:guid}", async Task<Results<Ok<PublicElement>, ProblemHttpResult>> (
                Guid boardId, Guid elementId, UpdateElementRequestV1 request, IUpdateElementCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(
                    new UpdateElementCommand(
                        Actors.From(http.User), boardId, elementId, request.Text, request.Type, request.Position?.ToModel(), request.Size?.ToModel(),
                        request.Pivotal, request.Color, request.ExpectedVersion),
                    cancellationToken);
                return result.Success ? TypedResults.Ok(PublicElement.From(result.Changes!.Elements[0])) : Problems.From(result, http);
            })
            .RequireAuthorization(ApiKeyAuthenticationHandler.WritePolicy)
            .WithName("UpdateElement").WithSummary("Change some fields of an element; fields left out stay as they are");

        elements.MapDelete("/{elementId:guid}", async Task<Results<NoContent, ProblemHttpResult>> (
                Guid boardId, Guid elementId, IDeleteElementsCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new DeleteElementsCommand(Actors.From(http.User), boardId, [elementId]), cancellationToken);
                return result.Success ? TypedResults.NoContent() : Problems.From(result, http);
            })
            .RequireAuthorization(ApiKeyAuthenticationHandler.WritePolicy)
            .WithName("DeleteElement").WithSummary("Delete an element and the connections attached to it");
    }

    private static void MapConnections(RouteGroupBuilder connections)
    {
        connections.MapGet("/", async Task<Results<Ok<PageResponse<PublicConnection>>, ProblemHttpResult>> (
                Guid boardId, string? cursor, int? limit, IListConnectionsQueryHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new ListConnectionsQuery(Actors.From(http.User), boardId, cursor, limit), cancellationToken);
                return result.Success
                    ? TypedResults.Ok(new PageResponse<PublicConnection>(result.Page!.Items.Select(PublicConnection.Of).ToList(), result.Page.NextCursor))
                    : Problems.From(result, http);
            })
            .WithName("ListConnections").WithSummary("A board's connections (paged)");

        connections.MapPost("/", async Task<Results<Created<PublicConnection>, ProblemHttpResult>> (
                Guid boardId, CreateConnectionRequest request, IAddConnectionCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(
                    new AddConnectionCommand(Actors.From(http.User), boardId, request.From ?? Guid.Empty, request.To ?? Guid.Empty, request.Label),
                    cancellationToken);
                if (!result.Success)
                {
                    return Problems.From(result, http);
                }

                var connection = result.Changes!.Connections[0];
                return TypedResults.Created($"/api/v1/boards/{boardId}/connections/{connection.Id}", PublicConnection.Of(connection));
            })
            .RequireAuthorization(ApiKeyAuthenticationHandler.WritePolicy)
            .WithName("CreateConnection").WithSummary("Draw an arrow from one element to another");

        connections.MapDelete("/{connectionId:guid}", async Task<Results<NoContent, ProblemHttpResult>> (
                Guid boardId, Guid connectionId, IDeleteConnectionsCommandHandler handler, HttpContext http, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new DeleteConnectionsCommand(Actors.From(http.User), boardId, [connectionId]), cancellationToken);
                return result.Success ? TypedResults.NoContent() : Problems.From(result, http);
            })
            .RequireAuthorization(ApiKeyAuthenticationHandler.WritePolicy)
            .WithName("DeleteConnection").WithSummary("Delete a connection");
    }
}
