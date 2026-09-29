using EventStorming.BoardModelling.Model;
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
using EventStorming.Collaboration.Slices.JoinBoard;
using EventStorming.Collaboration.Slices.LeaveBoard;
using EventStorming.Collaboration.Slices.MoveCursor;
using EventStorming.Collaboration.Slices.SetEditingFocus;
using EventStorming.Collaboration.Slices.ShareDragPreview;
using EventStorming.Identity.Model;
using EventStorming.Identity.Slices.GetMyAccount;
using EventStorming.Identity.Slices.RefreshSession;
using EventStorming.Identity.Slices.RegisterAccount;
using EventStorming.Identity.Slices.SignIn;
using EventStorming.Identity.Slices.SignOut;
using EventStorming.PublicIntegration.Slices.AuthenticateApiKey;
using EventStorming.PublicIntegration.Slices.CreateApiKey;
using EventStorming.PublicIntegration.Slices.ListApiKeys;
using EventStorming.PublicIntegration.Slices.RecordIdempotentResponse;
using EventStorming.PublicIntegration.Slices.ReserveIdempotencyKey;
using EventStorming.PublicIntegration.Slices.RevokeApiKey;
using EventStorming.SharedKernel;
using EventStorming.Specs.Support.Fakes;
using EventStorming.Teams.Model;
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
using Shouldly;

namespace EventStorming.Specs.Support;

/// <summary>
/// Everything one scenario needs, created fresh for each by Reqnroll's context injection: the in-memory
/// fakes behind every port, the people and keys in the story, and the last use case's result. Handlers
/// are built on demand from the real handler and validator classes, so each scenario exercises exactly
/// the code the host runs.
/// </summary>
public sealed class World
{
    public World()
    {
        AccessTokens = new FakeAccessTokenIssuer(Clock);
        Teams = new InMemoryTeams(Identity);
        Acl = new InMemoryAcl(Teams, Boards);
    }

    public FixedClock Clock { get; } = new();

    public FakePasswordHasher Hasher { get; } = new();

    public FakeAccessTokenIssuer AccessTokens { get; }

    public FakeSecrets Secrets { get; } = new();

    public InMemoryIdentity Identity { get; } = new();

    public InMemoryTeams Teams { get; }

    public RecordingMailer Mailer { get; } = new();

    public InMemoryBoards Boards { get; } = new();

    public InMemoryAcl Acl { get; }

    public FakeElementTypeRegistry Registry { get; } = new();

    public RecordingBroadcaster Broadcaster { get; } = new();

    public InMemoryPresence Presence { get; } = new();

    public InMemoryPublicIntegration PublicIntegration { get; } = new();

    public IUseCaseResult? LastResult { get; set; }

    public SessionTokens? Session { get; set; }

    public string? PreviousRefreshToken { get; set; }

    /// <summary>The invitation token the story last created.</summary>
    public string? InvitationToken { get; set; }

    /// <summary>Element keys used in the story, to the ids the elements were stored under.</summary>
    public Dictionary<string, Guid> Keys { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, Actor> ApiKeys { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> PresentedKeys { get; } = new(StringComparer.Ordinal);

    // Identity
    public IRegisterAccountCommandHandler RegisterAccount => new RegisterAccountCommandHandler(new RegisterAccountCommandValidator(), Identity, Hasher, Clock);

    public ISignInCommandHandler SignIn => new SignInCommandHandler(new SignInCommandValidator(), Identity, Hasher, AccessTokens, Secrets, Clock);

    public IRefreshSessionCommandHandler RefreshSession => new RefreshSessionCommandHandler(Identity, AccessTokens, Secrets, Clock);

    public ISignOutCommandHandler SignOut => new SignOutCommandHandler(Identity, Secrets, Clock);

    public IGetMyAccountQueryHandler GetMyAccount => new GetMyAccountQueryHandler(Identity);

    // Teams
    public ICreateTeamCommandHandler CreateTeam => new CreateTeamCommandHandler(new CreateTeamCommandValidator(), Teams, Clock);

    public IListMyTeamsQueryHandler ListMyTeams => new ListMyTeamsQueryHandler(Teams);

    public IGetTeamQueryHandler GetTeam => new GetTeamQueryHandler(Teams);

    public ICreateInvitationCommandHandler CreateInvitation => new CreateInvitationCommandHandler(new CreateInvitationCommandValidator(), Teams, Secrets, Mailer, Clock);

    public IListInvitationsQueryHandler ListInvitations => new ListInvitationsQueryHandler(Teams, Clock);

    public IRevokeInvitationCommandHandler RevokeInvitation => new RevokeInvitationCommandHandler(Teams, Clock);

    public IPreviewInvitationQueryHandler PreviewInvitation => new PreviewInvitationQueryHandler(Teams, Secrets, Clock);

    public IAcceptInvitationCommandHandler AcceptInvitation => new AcceptInvitationCommandHandler(Teams, Secrets, Clock);

    public IChangeMemberRoleCommandHandler ChangeMemberRole => new ChangeMemberRoleCommandHandler(new ChangeMemberRoleCommandValidator(), Teams);

    public IRemoveMemberCommandHandler RemoveMember => new RemoveMemberCommandHandler(Teams);

    // Board Modelling
    public ICreateBoardCommandHandler CreateBoard => new CreateBoardCommandHandler(new CreateBoardCommandValidator(), Acl, Boards, Clock);

    public IListBoardsQueryHandler ListBoards => new ListBoardsQueryHandler(new ListBoardsQueryValidator(), Acl, Boards);

    public IGetBoardQueryHandler GetBoard => new GetBoardQueryHandler(Acl, Boards);

    public IGetBoardSnapshotQueryHandler GetBoardSnapshot => new GetBoardSnapshotQueryHandler(Acl, Boards);

    public IRenameBoardCommandHandler RenameBoard => new RenameBoardCommandHandler(new RenameBoardCommandValidator(), Acl, Boards, Broadcaster, Clock);

    public IDuplicateBoardCommandHandler DuplicateBoard => new DuplicateBoardCommandHandler(new DuplicateBoardCommandValidator(), Acl, Boards, Clock);

    public IArchiveBoardCommandHandler ArchiveBoard => new ArchiveBoardCommandHandler(Acl, Boards, Broadcaster, Clock);

    public IRestoreBoardCommandHandler RestoreBoard => new RestoreBoardCommandHandler(Acl, Boards, Broadcaster, Clock);

    public IDeleteBoardCommandHandler DeleteBoard => new DeleteBoardCommandHandler(Acl, Boards, Broadcaster);

    public IAddElementsCommandHandler AddElements => new AddElementsCommandHandler(new AddElementsCommandValidator(), Acl, Registry, Boards, Broadcaster, Clock);

    public IUpdateElementCommandHandler UpdateElement => new UpdateElementCommandHandler(new UpdateElementCommandValidator(), Acl, Registry, Boards, Broadcaster, Clock);

    public IMoveElementsCommandHandler MoveElements => new MoveElementsCommandHandler(new MoveElementsCommandValidator(), Acl, Boards, Broadcaster, Clock);

    public IDeleteElementsCommandHandler DeleteElements => new DeleteElementsCommandHandler(new DeleteElementsCommandValidator(), Acl, Boards, Broadcaster, Clock);

    public IAddConnectionCommandHandler AddConnection => new AddConnectionCommandHandler(new AddConnectionCommandValidator(), Acl, Boards, Broadcaster, Clock);

    public IDeleteConnectionsCommandHandler DeleteConnections => new DeleteConnectionsCommandHandler(new DeleteConnectionsCommandValidator(), Acl, Boards, Broadcaster, Clock);

    public IListElementsQueryHandler ListElements => new ListElementsQueryHandler(new ListElementsQueryValidator(), Acl, Registry, Boards);

    public IGetElementQueryHandler GetElement => new GetElementQueryHandler(Acl, Boards);

    public IListConnectionsQueryHandler ListConnections => new ListConnectionsQueryHandler(new ListConnectionsQueryValidator(), Acl, Boards);

    public IImportBoardDocumentCommandHandler ImportBoardDocument => new ImportBoardDocumentCommandHandler(new BoardDocumentValidator(), Acl, Registry, Boards, Broadcaster, Clock);

    public IExportBoardDocumentQueryHandler ExportBoardDocument => new ExportBoardDocumentQueryHandler(Acl, Registry, Boards);

    public IListElementTypesQueryHandler ListElementTypes => new ListElementTypesQueryHandler(Registry);

    // Collaboration
    public IJoinBoardCommandHandler JoinBoard => new JoinBoardCommandHandler(Acl, Presence, Broadcaster, Clock);

    public ILeaveBoardCommandHandler LeaveBoard => new LeaveBoardCommandHandler(Presence, Broadcaster);

    public IMoveCursorCommandHandler MoveCursor => new MoveCursorCommandHandler(Presence, Broadcaster);

    public ISetEditingFocusCommandHandler SetEditingFocus => new SetEditingFocusCommandHandler(Presence, Broadcaster);

    public IShareDragPreviewCommandHandler ShareDragPreview => new ShareDragPreviewCommandHandler(Presence, Broadcaster);

    // Public Integration
    public ICreateApiKeyCommandHandler CreateApiKey => new CreateApiKeyCommandHandler(new CreateApiKeyCommandValidator(), Acl, Secrets, PublicIntegration, Clock);

    public IListApiKeysQueryHandler ListApiKeys => new ListApiKeysQueryHandler(Acl, PublicIntegration);

    public IRevokeApiKeyCommandHandler RevokeApiKey => new RevokeApiKeyCommandHandler(Acl, PublicIntegration, Clock);

    public IAuthenticateApiKeyCommandHandler AuthenticateApiKey => new AuthenticateApiKeyCommandHandler(PublicIntegration, Secrets, Clock);

    public IReserveIdempotencyKeyCommandHandler ReserveIdempotencyKey => new ReserveIdempotencyKeyCommandHandler(PublicIntegration, Clock);

    public IRecordIdempotentResponseCommandHandler RecordIdempotentResponse => new RecordIdempotentResponseCommandHandler(PublicIntegration);

    /// <summary>A person in the story is an account with that display name; an API key is registered by name.</summary>
    public Actor Person(string name)
    {
        if (ApiKeys.TryGetValue(name, out var key))
        {
            return key;
        }

        var account = Identity.Accounts.SingleOrDefault(candidate => candidate.DisplayName == name);
        account.ShouldNotBeNull($"No one named '{name}' has an account in this scenario. Add a 'Given {name} has an account' step.");
        return Actor.Account(account.Id, account.DisplayName);
    }

    /// <summary>Creates an account directly, for stories that are not about registering.</summary>
    public Actor EnsurePerson(string name)
    {
        var existing = Identity.Accounts.SingleOrDefault(candidate => candidate.DisplayName == name);
        if (existing is null)
        {
            existing = new Account(Guid.CreateVersion7(), $"{name.ToLowerInvariant().Replace(' ', '.')}@example.com", name, Hasher.Hash("correct horse"), Clock.UtcNow);
            Identity.Accounts.Add(existing);
        }

        return Actor.Account(existing.Id, existing.DisplayName);
    }

    public Team Team(string name) => Teams.Named(name);

    public Board Board(string name) => Boards.Named(name);

    public Guid Element(string key)
    {
        Keys.TryGetValue(key, out var id).ShouldBeTrue($"No element with key '{key}' in this scenario.");
        return id;
    }

    public Element ElementNamed(string key) => Boards.Elements.Single(element => element.Id == Element(key));

    public T Last<T>()
        where T : class, IUseCaseResult => LastResult.ShouldBeOfType<T>();
}
