using EventStorming.BoardModelling.Slices.AddConnection;
using EventStorming.BoardModelling.Slices.AddElements;
using EventStorming.BoardModelling.Slices.ArchiveBoard;
using EventStorming.BoardModelling.Slices.CreateBoard;
using EventStorming.BoardModelling.Slices.DeleteBoard;
using EventStorming.BoardModelling.Slices.DeleteConnections;
using EventStorming.BoardModelling.Slices.DeleteElements;
using EventStorming.BoardModelling.Slices.DuplicateBoard;
using EventStorming.BoardModelling.Slices.ExportBoardDocument;
using EventStorming.BoardModelling.Slices.GetBoard;
using EventStorming.BoardModelling.Slices.GetBoardSnapshot;
using EventStorming.BoardModelling.Slices.GetElement;
using EventStorming.BoardModelling.Slices.ImportBoardDocument;
using EventStorming.BoardModelling.Slices.ListBoards;
using EventStorming.BoardModelling.Slices.ListConnections;
using EventStorming.BoardModelling.Slices.ListElements;
using EventStorming.BoardModelling.Slices.ListElementTypes;
using EventStorming.BoardModelling.Slices.MoveElements;
using EventStorming.BoardModelling.Slices.RenameBoard;
using EventStorming.BoardModelling.Slices.RestoreBoard;
using EventStorming.BoardModelling.Slices.UpdateElement;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EventStorming.BoardModelling;

public static class BoardModellingServiceExtensions
{
    public static IServiceCollection AddEventStormingBoardModelling(this IServiceCollection services)
    {
        // Catalog
        services.AddScoped<ICreateBoardCommandHandler, CreateBoardCommandHandler>();
        services.AddSingleton<IValidator<CreateBoardCommand>, CreateBoardCommandValidator>();
        services.AddScoped<IListBoardsQueryHandler, ListBoardsQueryHandler>();
        services.AddSingleton<IValidator<ListBoardsQuery>, ListBoardsQueryValidator>();
        services.AddScoped<IGetBoardQueryHandler, GetBoardQueryHandler>();
        services.AddScoped<IGetBoardSnapshotQueryHandler, GetBoardSnapshotQueryHandler>();
        services.AddScoped<IRenameBoardCommandHandler, RenameBoardCommandHandler>();
        services.AddSingleton<IValidator<RenameBoardCommand>, RenameBoardCommandValidator>();
        services.AddScoped<IDuplicateBoardCommandHandler, DuplicateBoardCommandHandler>();
        services.AddSingleton<IValidator<DuplicateBoardCommand>, DuplicateBoardCommandValidator>();
        services.AddScoped<IArchiveBoardCommandHandler, ArchiveBoardCommandHandler>();
        services.AddScoped<IRestoreBoardCommandHandler, RestoreBoardCommandHandler>();
        services.AddScoped<IDeleteBoardCommandHandler, DeleteBoardCommandHandler>();

        // Content
        services.AddScoped<IAddElementsCommandHandler, AddElementsCommandHandler>();
        services.AddSingleton<IValidator<AddElementsCommand>, AddElementsCommandValidator>();
        services.AddScoped<IUpdateElementCommandHandler, UpdateElementCommandHandler>();
        services.AddSingleton<IValidator<UpdateElementCommand>, UpdateElementCommandValidator>();
        services.AddScoped<IMoveElementsCommandHandler, MoveElementsCommandHandler>();
        services.AddSingleton<IValidator<MoveElementsCommand>, MoveElementsCommandValidator>();
        services.AddScoped<IDeleteElementsCommandHandler, DeleteElementsCommandHandler>();
        services.AddSingleton<IValidator<DeleteElementsCommand>, DeleteElementsCommandValidator>();
        services.AddScoped<IAddConnectionCommandHandler, AddConnectionCommandHandler>();
        services.AddSingleton<IValidator<AddConnectionCommand>, AddConnectionCommandValidator>();
        services.AddScoped<IDeleteConnectionsCommandHandler, DeleteConnectionsCommandHandler>();
        services.AddSingleton<IValidator<DeleteConnectionsCommand>, DeleteConnectionsCommandValidator>();
        services.AddScoped<IListElementsQueryHandler, ListElementsQueryHandler>();
        services.AddSingleton<IValidator<ListElementsQuery>, ListElementsQueryValidator>();
        services.AddScoped<IGetElementQueryHandler, GetElementQueryHandler>();
        services.AddScoped<IListConnectionsQueryHandler, ListConnectionsQueryHandler>();
        services.AddSingleton<IValidator<ListConnectionsQuery>, ListConnectionsQueryValidator>();

        // Documents and notation
        services.AddScoped<IImportBoardDocumentCommandHandler, ImportBoardDocumentCommandHandler>();
        services.AddSingleton<IValidator<BoardDocument>, BoardDocumentValidator>();
        services.AddScoped<IExportBoardDocumentQueryHandler, ExportBoardDocumentQueryHandler>();
        services.AddScoped<IListElementTypesQueryHandler, ListElementTypesQueryHandler>();

        return services;
    }
}
