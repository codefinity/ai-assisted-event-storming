using EventStorming.Identity.Model;
using EventStorming.Identity.Slices.RefreshSession;
using EventStorming.Identity.Slices.RegisterAccount;
using EventStorming.Identity.Slices.SignIn;
using EventStorming.Identity.Slices.SignOut;
using EventStorming.Teams.Model;
using EventStorming.Teams.Slices.AcceptInvitation;
using EventStorming.Teams.Slices.ChangeMemberRole;
using EventStorming.Teams.Slices.CreateInvitation;
using EventStorming.Teams.Slices.CreateTeam;
using EventStorming.Teams.Slices.GetTeam;
using EventStorming.Teams.Slices.ListMyTeams;
using EventStorming.Teams.Slices.PreviewInvitation;
using EventStorming.Teams.Slices.RemoveMember;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace EventStorming.Persistence.MongoDb.IntegrationTests;

public sealed class IdentityAndTeamStoreTests(MongoContainer mongo)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task An_email_can_be_registered_only_once()
    {
        await using var provider = await mongo.NewStores();
        var store = provider.CreateScope().ServiceProvider.GetRequiredService<IRegisterAccountStore>();

        (await store.TryInsert(Account("ana@example.com"), CancellationToken.None)).ShouldBeTrue();
        (await store.TryInsert(Account("ana@example.com"), CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_refresh_token_rotates_exactly_once_and_revoking_its_family_revokes_all()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var account = Account("ana@example.com");
        await scope.GetRequiredService<IRegisterAccountStore>().TryInsert(account, CancellationToken.None);

        var family = Guid.NewGuid();
        var first = new RefreshToken(Guid.NewGuid(), account.Id, family, "hash-1", Now, Now.AddDays(30));
        await scope.GetRequiredService<ISignInStore>().Save(first, CancellationToken.None);

        var store = scope.GetRequiredService<IRefreshSessionStore>();
        var second = first with { Id = Guid.NewGuid(), TokenHash = "hash-2" };
        (await store.TryRotate(first.Id, second, Now, CancellationToken.None)).ShouldBeTrue();
        (await store.TryRotate(first.Id, second with { Id = Guid.NewGuid(), TokenHash = "hash-3" }, Now, CancellationToken.None)).ShouldBeFalse();

        var rotated = (await store.FindByHash("hash-1", CancellationToken.None)).ShouldNotBeNull();
        rotated.ReplacedById.ShouldBe(second.Id);
        rotated.RotatedAt.ShouldBe(Now);

        await scope.GetRequiredService<ISignOutStore>().RevokeFamily(family, "sign-out", Now, CancellationToken.None);
        (await store.FindByHash("hash-2", CancellationToken.None))!.RevokedReason.ShouldBe("sign-out");
    }

    [Fact]
    public async Task Membership_changes_are_guarded_by_the_team_version()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var owner = Guid.NewGuid();
        var editor = Guid.NewGuid();
        var team = new Team(Guid.NewGuid(), "Checkout squad", Now, owner,
            [new Membership(owner, TeamRole.Owner, Now), new Membership(editor, TeamRole.Editor, Now)], Version: 1);
        await scope.GetRequiredService<ICreateTeamStore>().Insert(team, CancellationToken.None);

        var roles = scope.GetRequiredService<IChangeMemberRoleStore>();
        (await roles.SetRole(team.Id, 1, editor, TeamRole.Viewer, CancellationToken.None)).ShouldBeTrue();
        (await roles.SetRole(team.Id, 1, editor, TeamRole.Owner, CancellationToken.None)).ShouldBeFalse("the version moved on");

        var stored = (await roles.FindTeam(team.Id, CancellationToken.None)).ShouldNotBeNull();
        stored.Version.ShouldBe(2);
        stored.RoleOf(editor).ShouldBe(TeamRole.Viewer);

        (await scope.GetRequiredService<IRemoveMemberStore>().Remove(team.Id, 2, editor, CancellationToken.None)).ShouldBeTrue();
        (await scope.GetRequiredService<IListMyTeamsStore>().TeamsOf(editor, CancellationToken.None)).ShouldBeEmpty();
        (await scope.GetRequiredService<IListMyTeamsStore>().TeamsOf(owner, CancellationToken.None)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Accepting_an_email_invitation_adds_the_member_and_consumes_the_invitation_together()
    {
        await using var provider = await mongo.NewStores();
        var scope = provider.CreateScope().ServiceProvider;
        var owner = Account("ana@example.com");
        var guest = Account("bo@example.com");
        await scope.GetRequiredService<IRegisterAccountStore>().TryInsert(owner, CancellationToken.None);
        await scope.GetRequiredService<IRegisterAccountStore>().TryInsert(guest, CancellationToken.None);

        var team = new Team(Guid.NewGuid(), "Checkout squad", Now, owner.Id, [new Membership(owner.Id, TeamRole.Owner, Now)], 1);
        await scope.GetRequiredService<ICreateTeamStore>().Insert(team, CancellationToken.None);
        var invitation = new Invitation(Guid.NewGuid(), team.Id, InvitationKind.Email, guest.Email, TeamRole.Editor, "token-hash", owner.Id, Now, Now.AddDays(7));
        await scope.GetRequiredService<ICreateInvitationStore>().Insert(invitation, CancellationToken.None);

        var accept = scope.GetRequiredService<IAcceptInvitationStore>();
        (await accept.EmailOf(guest.Id, CancellationToken.None)).ShouldBe("bo@example.com");
        (await accept.AddMember(team.Id, new Membership(guest.Id, TeamRole.Editor, Now), invitation.Id, Now, CancellationToken.None)).ShouldBeTrue();
        (await accept.AddMember(team.Id, new Membership(guest.Id, TeamRole.Editor, Now), invitation.Id, Now, CancellationToken.None)).ShouldBeFalse("already a member");

        (await accept.FindByHash("token-hash", CancellationToken.None))!.AcceptedBy.ShouldBe(guest.Id);

        var preview = (await scope.GetRequiredService<IPreviewInvitationStore>().FindByHash("token-hash", CancellationToken.None)).ShouldNotBeNull();
        preview.TeamName.ShouldBe("Checkout squad");
        preview.InvitedBy.ShouldBe("ana");

        var details = scope.GetRequiredService<IGetTeamStore>();
        var profiles = await details.Profiles([owner.Id, guest.Id], CancellationToken.None);
        profiles[guest.Id].Email.ShouldBe("bo@example.com");
    }

    private static Account Account(string email) => new(Guid.NewGuid(), email, email.Split('@')[0], "hashed", Now);
}
