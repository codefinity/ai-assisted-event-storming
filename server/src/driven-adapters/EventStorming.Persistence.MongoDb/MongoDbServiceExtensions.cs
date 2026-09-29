using EventStorming.BoardModelling.Shared;
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
using EventStorming.BoardModelling.Slices.MoveElements;
using EventStorming.BoardModelling.Slices.RenameBoard;
using EventStorming.BoardModelling.Slices.RestoreBoard;
using EventStorming.BoardModelling.Slices.UpdateElement;
using EventStorming.Collaboration.Shared;
using EventStorming.Identity.Slices.GetMyAccount;
using EventStorming.Identity.Slices.RefreshSession;
using EventStorming.Identity.Slices.RegisterAccount;
using EventStorming.Identity.Slices.SignIn;
using EventStorming.Identity.Slices.SignOut;
using EventStorming.Persistence.MongoDb.Acl;
using EventStorming.Persistence.MongoDb.BoardModelling;
using EventStorming.Persistence.MongoDb.Identity;
using EventStorming.Persistence.MongoDb.Infrastructure;
using EventStorming.Persistence.MongoDb.PublicIntegration;
using EventStorming.Persistence.MongoDb.Teams;
using EventStorming.PublicIntegration.Shared;
using EventStorming.PublicIntegration.Slices.AuthenticateApiKey;
using EventStorming.PublicIntegration.Slices.CreateApiKey;
using EventStorming.PublicIntegration.Slices.ListApiKeys;
using EventStorming.PublicIntegration.Slices.RecordIdempotentResponse;
using EventStorming.PublicIntegration.Slices.ReserveIdempotencyKey;
using EventStorming.PublicIntegration.Slices.RevokeApiKey;
using EventStorming.Teams.Slices.AcceptInvitation;
using EventStorming.Teams.Slices.ChangeMemberRole;
using EventStorming.Teams.Slices.CreateInvitation;
using EventStorming.Teams.Slices.CreateTeam;
using EventStorming.Teams.Slices.GetTeam;
using EventStorming.Teams.Slices.ListInvitations;
using EventStorming.Teams.Slices.ListMyTeams;
using EventStorming.Teams.Slices.PreviewInvitation;
using EventStorming.Teams.Slices.RemoveMember;
using EventStorming.Teams.Slices.RevokeInvitation;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace EventStorming.Persistence.MongoDb;

public static class MongoDbServiceExtensions
{
    /// <summary>
    /// Registers a store for every persistence port of every bounded context, the anti-corruption
    /// layer over team membership, and the startup index bootstrap. Each context's stores live in their
    /// own folder and touch only that context's collections; the Acl folder is the one place that reads
    /// across contexts, on purpose.
    /// </summary>
    public static IServiceCollection AddEventStormingMongoDb(this IServiceCollection services, MongoOptions options)
    {
        MongoConventions.Register();

        services.AddSingleton(options);
        services.AddSingleton<IMongoClient>(_ => new MongoClient(options.ConnectionString));
        services.AddSingleton<MongoDatabase>();
        services.AddHostedService<MongoIndexes>();

        // Identity
        services.AddScoped<IRegisterAccountStore, RegisterAccountStore>();
        services.AddScoped<ISignInStore, SignInStore>();
        services.AddScoped<IRefreshSessionStore, RefreshSessionStore>();
        services.AddScoped<ISignOutStore, SignOutStore>();
        services.AddScoped<IGetMyAccountStore, GetMyAccountStore>();

        // Teams
        services.AddScoped<ICreateTeamStore, CreateTeamStore>();
        services.AddScoped<IListMyTeamsStore, ListMyTeamsStore>();
        services.AddScoped<IGetTeamStore, GetTeamStore>();
        services.AddScoped<ICreateInvitationStore, CreateInvitationStore>();
        services.AddScoped<IListInvitationsStore, ListInvitationsStore>();
        services.AddScoped<IRevokeInvitationStore, RevokeInvitationStore>();
        services.AddScoped<IPreviewInvitationStore, PreviewInvitationStore>();
        services.AddScoped<IAcceptInvitationStore, AcceptInvitationStore>();
        services.AddScoped<IChangeMemberRoleStore, ChangeMemberRoleStore>();
        services.AddScoped<IRemoveMemberStore, RemoveMemberStore>();

        // Board Modelling
        services.AddScoped<ICreateBoardStore, CreateBoardStore>();
        services.AddScoped<IListBoardsStore, ListBoardsStore>();
        services.AddScoped<IGetBoardStore, GetBoardStore>();
        services.AddScoped<GetBoardSnapshotStore>();
        services.AddScoped<IGetBoardSnapshotStore>(provider => provider.GetRequiredService<GetBoardSnapshotStore>());
        services.AddScoped<IExportBoardDocumentStore>(provider => provider.GetRequiredService<GetBoardSnapshotStore>());
        services.AddScoped<IRenameBoardStore, RenameBoardStore>();
        services.AddScoped<IDuplicateBoardStore, DuplicateBoardStore>();
        services.AddScoped<ArchiveBoardStore>();
        services.AddScoped<IArchiveBoardStore>(provider => provider.GetRequiredService<ArchiveBoardStore>());
        services.AddScoped<IRestoreBoardStore>(provider => provider.GetRequiredService<ArchiveBoardStore>());
        services.AddScoped<IDeleteBoardStore, DeleteBoardStore>();
        services.AddScoped<IImportBoardDocumentStore, ImportBoardDocumentStore>();
        services.AddScoped<IAddElementsStore, AddElementsStore>();
        services.AddScoped<UpdateElementStore>();
        services.AddScoped<IUpdateElementStore>(provider => provider.GetRequiredService<UpdateElementStore>());
        services.AddScoped<IGetElementStore>(provider => provider.GetRequiredService<UpdateElementStore>());
        services.AddScoped<IMoveElementsStore, MoveElementsStore>();
        services.AddScoped<IDeleteElementsStore, DeleteElementsStore>();
        services.AddScoped<IAddConnectionStore, AddConnectionStore>();
        services.AddScoped<IDeleteConnectionsStore, DeleteConnectionsStore>();
        services.AddScoped<IListElementsStore, ListElementsStore>();
        services.AddScoped<IListConnectionsStore, ListConnectionsStore>();

        // Public Integration
        services.AddScoped<ApiKeyStores>();
        services.AddScoped<ICreateApiKeyStore>(provider => provider.GetRequiredService<ApiKeyStores>());
        services.AddScoped<IListApiKeysStore>(provider => provider.GetRequiredService<ApiKeyStores>());
        services.AddScoped<IRevokeApiKeyStore>(provider => provider.GetRequiredService<ApiKeyStores>());
        services.AddScoped<IAuthenticateApiKeyStore>(provider => provider.GetRequiredService<ApiKeyStores>());
        services.AddScoped<IdempotencyStores>();
        services.AddScoped<IReserveIdempotencyKeyStore>(provider => provider.GetRequiredService<IdempotencyStores>());
        services.AddScoped<IRecordIdempotentResponseStore>(provider => provider.GetRequiredService<IdempotencyStores>());

        // Anti-corruption layer over Teams, for Board Modelling, Collaboration and Public Integration
        services.AddScoped<TeamMembershipAcl>();
        services.AddScoped<IBoardAccess>(provider => provider.GetRequiredService<TeamMembershipAcl>());
        services.AddScoped<IBoardViewAccess>(provider => provider.GetRequiredService<TeamMembershipAcl>());
        services.AddScoped<ITeamOwnership>(provider => provider.GetRequiredService<TeamMembershipAcl>());

        return services;
    }
}
