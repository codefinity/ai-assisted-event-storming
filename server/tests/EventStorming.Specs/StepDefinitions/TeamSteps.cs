using EventStorming.SharedKernel;
using EventStorming.Specs.Support;
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
using Reqnroll;
using Shouldly;

namespace EventStorming.Specs.StepDefinitions;

[Binding]
public sealed class TeamSteps(World world)
{
    [Given("{word} owns the team {string}")]
    public void GivenOwnsTheTeam(string person, string team)
    {
        var owner = world.EnsurePerson(person);
        world.Teams.Teams.Add(new Team(Guid.CreateVersion7(), team, world.Clock.UtcNow, owner.Id, [new Membership(owner.Id, TeamRole.Owner, world.Clock.UtcNow)], 1));
    }

    [Given("{word} is an {word} of {string}")]
    [Given("{word} is a {word} of {string}")]
    public void GivenIsAMemberOf(string person, string role, string team)
    {
        var member = world.EnsurePerson(person);
        var index = world.Teams.Teams.FindIndex(candidate => candidate.Name == team);
        var current = world.Teams.Teams[index];
        world.Teams.Teams[index] = current with
        {
            Members = [.. current.Members.Where(existing => existing.AccountId != member.Id), new Membership(member.Id, TeamRoles.Parse(role), world.Clock.UtcNow)],
            Version = current.Version + 1,
        };
    }

    [When("{word} creates a team called {string}")]
    public async Task WhenCreatesATeam(string person, string name) =>
        world.LastResult = await world.CreateTeam.Handle(new CreateTeamCommand(world.Person(person), name), CancellationToken.None);

    [Then("{word} is the {word} of {string}")]
    [Then("{word} is an {word} of {string}")]
    [Then("{word} is a {word} of {string}")]
    public void ThenHasTheRole(string person, string role, string team) =>
        world.Team(team).RoleOf(world.Person(person).Id).ShouldBe(TeamRoles.Parse(role));

    [Then("{word} is not a member of {string}")]
    public void ThenIsNotAMember(string person, string team) =>
        world.Team(team).RoleOf(world.Person(person).Id).ShouldBeNull();

    [When("{word} lists their teams")]
    public async Task WhenListsTheirTeams(string person) =>
        world.LastResult = await world.ListMyTeams.Handle(new ListMyTeamsQuery(world.Person(person)), CancellationToken.None);

    [Then("the teams listed are:")]
    public void ThenTheTeamsListedAre(DataTable table) =>
        world.Last<ListMyTeamsResult>().Teams.Select(team => (team.Name, TeamRoles.Name(team.MyRole)))
            .ShouldBe(table.Rows.Select(row => (row["team"], row["role"])));

    [When("{word} opens the team {string}")]
    public async Task WhenOpensTheTeam(string person, string team) =>
        world.LastResult = await world.GetTeam.Handle(new GetTeamQuery(world.Person(person), world.Team(team).Id), CancellationToken.None);

    [Then("the team's members are:")]
    public void ThenTheMembersAre(DataTable table) =>
        world.Last<GetTeamResult>().Team.ShouldNotBeNull().Members.Select(member => (member.DisplayName, TeamRoles.Name(member.Role)))
            .ShouldBe(table.Rows.Select(row => (row["name"], row["role"])));

    [When("{word} invites {string} to {string} as {word}")]
    [Given("{word} has invited {string} to {string} as {word}")]
    public async Task WhenInvitesByEmail(string person, string email, string team, string role) =>
        await Invite(person, team, role, email);

    [When("{word} creates an invitation link to {string} as {word}")]
    [Given("{word} has created an invitation link to {string} as {word}")]
    public async Task WhenCreatesAnInvitationLink(string person, string team, string role) =>
        await Invite(person, team, role, null);

    [Then("an invitation email is sent to {string} from {word}")]
    public void ThenAnInvitationEmailIsSent(string email, string inviter)
    {
        var mail = world.Mailer.Sent.ShouldHaveSingleItem();
        mail.To.ShouldBe(email);
        mail.InvitedBy.ShouldBe(inviter);
        mail.Token.ShouldBe(world.InvitationToken);
    }

    [Then("no invitation email is sent")]
    public void ThenNoInvitationEmailIsSent() => world.Mailer.Sent.ShouldBeEmpty();

    [Then("only the hash of the invitation token is stored")]
    public void ThenOnlyTheHashIsStored()
    {
        var invitation = world.Teams.Invitations.ShouldHaveSingleItem();
        invitation.TokenHash.ShouldBe(world.Secrets.HashOf(world.InvitationToken!));
    }

    [When("{word} lists the invitations of {string}")]
    public async Task WhenListsTheInvitations(string person, string team) =>
        world.LastResult = await world.ListInvitations.Handle(new ListInvitationsQuery(world.Person(person), world.Team(team).Id), CancellationToken.None);

    [Then("{int} invitation(s) is/are open")]
    public void ThenInvitationsAreOpen(int count) => world.Last<ListInvitationsResult>().Invitations.Count.ShouldBe(count);

    [When("{word} revokes the invitation")]
    [Given("{word} has revoked the invitation")]
    public async Task WhenRevokesTheInvitation(string person)
    {
        var invitation = world.Teams.Invitations.Last();
        world.LastResult = await world.RevokeInvitation.Handle(new RevokeInvitationCommand(world.Person(person), invitation.TeamId, invitation.Id), CancellationToken.None);
    }

    [When("someone previews the invitation")]
    public async Task WhenSomeonePreviewsTheInvitation() =>
        world.LastResult = await world.PreviewInvitation.Handle(new PreviewInvitationQuery(world.InvitationToken!), CancellationToken.None);

    [When("someone previews the invitation {string}")]
    public async Task WhenSomeonePreviewsTheInvitationToken(string token) =>
        world.LastResult = await world.PreviewInvitation.Handle(new PreviewInvitationQuery(token), CancellationToken.None);

    [Then("the preview says {word} invited them to {string} as {word}")]
    public void ThenThePreviewSays(string inviter, string team, string role)
    {
        var preview = world.Last<PreviewInvitationResult>().Preview.ShouldNotBeNull();
        preview.InvitedBy.ShouldBe(inviter);
        preview.TeamName.ShouldBe(team);
        TeamRoles.Name(preview.Role).ShouldBe(role);
    }

    [Then("the invitation is {word}")]
    public void ThenTheInvitationIs(string status) =>
        world.Last<PreviewInvitationResult>().Preview.ShouldNotBeNull().Status.ToString().ToLowerInvariant().ShouldBe(status);

    [When("{word} accepts the invitation")]
    [Given("{word} has accepted the invitation")]
    public async Task WhenAcceptsTheInvitation(string person) =>
        world.LastResult = await world.AcceptInvitation.Handle(new AcceptInvitationCommand(world.EnsurePerson(person), world.InvitationToken!), CancellationToken.None);

    [When("{word} makes {word} a(n) {word} of {string}")]
    public async Task WhenChangesTheRole(string person, string member, string role, string team) =>
        world.LastResult = await world.ChangeMemberRole.Handle(
            new ChangeMemberRoleCommand(world.Person(person), world.Team(team).Id, world.Person(member).Id, role), CancellationToken.None);

    [When("{word} removes {word} from {string}")]
    public async Task WhenRemoves(string person, string member, string team) =>
        world.LastResult = await world.RemoveMember.Handle(new RemoveMemberCommand(world.Person(person), world.Team(team).Id, world.Person(member).Id), CancellationToken.None);

    [When("{word} leaves {string}")]
    [Given("{word} leaves {string}")]
    public async Task WhenLeaves(string person, string team) =>
        world.LastResult = await world.RemoveMember.Handle(new RemoveMemberCommand(world.Person(person), world.Team(team).Id, world.Person(person).Id), CancellationToken.None);

    private async Task Invite(string person, string team, string role, string? email)
    {
        var result = await world.CreateInvitation.Handle(new CreateInvitationCommand(world.Person(person), world.Team(team).Id, role, email), CancellationToken.None);
        world.LastResult = result;
        if (result.Success)
        {
            world.InvitationToken = result.Created!.Token;
        }
    }
}
