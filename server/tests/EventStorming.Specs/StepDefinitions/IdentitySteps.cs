using EventStorming.Identity.Model;
using EventStorming.Identity.Slices.GetMyAccount;
using EventStorming.Identity.Slices.RefreshSession;
using EventStorming.Identity.Slices.RegisterAccount;
using EventStorming.Identity.Slices.SignIn;
using EventStorming.Identity.Slices.SignOut;
using EventStorming.SharedKernel;
using EventStorming.Specs.Support;
using Reqnroll;
using Shouldly;

namespace EventStorming.Specs.StepDefinitions;

[Binding]
public sealed class IdentitySteps(World world)
{
    [Given("{string} has registered as {string} with password {string}")]
    public async Task GivenHasRegistered(string email, string name, string password)
    {
        var result = await world.RegisterAccount.Handle(new RegisterAccountCommand(email, name, password), CancellationToken.None);
        result.Success.ShouldBeTrue();
    }

    [Given("{word} has an account")]
    public void GivenHasAnAccount(string name) => world.EnsurePerson(name);

    [When("{string} registers as {string} with password {string}")]
    public async Task WhenRegisters(string email, string name, string password) =>
        world.LastResult = await world.RegisterAccount.Handle(new RegisterAccountCommand(email, name, password), CancellationToken.None);

    [Then("an account exists for {string} named {string}")]
    public void ThenAnAccountExists(string email, string name)
    {
        var account = world.Identity.Accounts.SingleOrDefault(candidate => candidate.Email == email);
        account.ShouldNotBeNull();
        account.DisplayName.ShouldBe(name);
        account.CreatedAt.ShouldBe(world.Clock.UtcNow);
    }

    [Then("the stored password for {string} is not {string}")]
    public void ThenTheStoredPasswordIsNot(string email, string password)
    {
        var account = world.Identity.Accounts.Single(candidate => candidate.Email == email);
        account.PasswordHash.ShouldNotBe(password);
        world.Hasher.Verify(password, account.PasswordHash).ShouldBeTrue();
    }

    [Given("{string} is signed in with password {string}")]
    [When("{string} signs in with password {string}")]
    public async Task WhenSignsIn(string email, string password)
    {
        var result = await world.SignIn.Handle(new SignInCommand(email, password), CancellationToken.None);
        world.LastResult = result;
        if (result.Success)
        {
            world.Session = result.Session;
        }
    }

    [Then("a session is started for {string}")]
    public void ThenASessionIsStartedFor(string email)
    {
        var session = world.Last<SignInResult>().Session.ShouldNotBeNull();
        var account = world.Identity.Accounts.Single(candidate => candidate.Email == email);

        session.Account.Id.ShouldBe(account.Id);
        session.AccessToken.ShouldBe($"access-for:{account.Id}");
        session.RefreshTokenExpiresAt.ShouldBe(world.Clock.UtcNow.AddDays(30));
    }

    [Then("only the hash of the refresh token is stored")]
    public void ThenOnlyTheHashOfTheRefreshTokenIsStored()
    {
        var session = world.Session.ShouldNotBeNull();
        world.Identity.RefreshTokens.ShouldNotContain(token => token.TokenHash == session.RefreshToken);
        world.Identity.RefreshTokens.ShouldContain(token => token.TokenHash == world.Secrets.HashOf(session.RefreshToken));
    }

    [When("the session is refreshed")]
    [Given("the session has been refreshed")]
    public async Task WhenTheSessionIsRefreshed()
    {
        var session = world.Session.ShouldNotBeNull();
        var result = await world.RefreshSession.Handle(new RefreshSessionCommand(session.RefreshToken), CancellationToken.None);
        world.LastResult = result;
        if (result.Success)
        {
            world.PreviousRefreshToken = session.RefreshToken;
            world.Session = result.Session;
        }
    }

    [When("the previous refresh token is presented again")]
    public async Task WhenThePreviousRefreshTokenIsPresentedAgain() =>
        world.LastResult = await world.RefreshSession.Handle(
            new RefreshSessionCommand(world.PreviousRefreshToken.ShouldNotBeNull()), CancellationToken.None);

    [When("the refresh token {string} is presented")]
    public async Task WhenTheRefreshTokenIsPresented(string token) =>
        world.LastResult = await world.RefreshSession.Handle(new RefreshSessionCommand(token), CancellationToken.None);

    [Then("a new refresh token replaces the old one")]
    public void ThenANewRefreshTokenReplacesTheOldOne()
    {
        var session = world.Last<RefreshSessionResult>().Session.ShouldNotBeNull();
        session.RefreshToken.ShouldNotBe(world.PreviousRefreshToken);

        var previous = world.Identity.RefreshTokens.Single(token => token.TokenHash == world.Secrets.HashOf(world.PreviousRefreshToken!));
        var current = world.Identity.RefreshTokens.Single(token => token.TokenHash == world.Secrets.HashOf(session.RefreshToken));
        previous.ReplacedById.ShouldBe(current.Id);
        current.FamilyId.ShouldBe(previous.FamilyId);
    }

    [Then("every refresh token of the session is revoked")]
    public void ThenEveryRefreshTokenOfTheSessionIsRevoked()
    {
        world.Identity.RefreshTokens.ShouldNotBeEmpty();
        world.Identity.RefreshTokens.ShouldAllBe(token => token.RevokedAt != null);
    }

    [Then("the current session can no longer be refreshed")]
    public async Task ThenTheCurrentSessionCanNoLongerBeRefreshed()
    {
        var result = await world.RefreshSession.Handle(new RefreshSessionCommand(world.Session!.RefreshToken), CancellationToken.None);
        result.Success.ShouldBeFalse();
        result.Failures[0].Code.ShouldBe("session-expired");
    }

    [Then("the current session can still be refreshed")]
    public async Task ThenTheCurrentSessionCanStillBeRefreshed()
    {
        var result = await world.RefreshSession.Handle(new RefreshSessionCommand(world.Session!.RefreshToken), CancellationToken.None);
        result.Success.ShouldBeTrue();
    }

    [When("they sign out")]
    public async Task WhenTheySignOut() =>
        world.LastResult = await world.SignOut.Handle(new SignOutCommand(world.Session?.RefreshToken), CancellationToken.None);

    [When("someone signs out without a session")]
    public async Task WhenSomeoneSignsOutWithoutASession() =>
        world.LastResult = await world.SignOut.Handle(new SignOutCommand(null), CancellationToken.None);

    [When("{word} asks for their account")]
    public async Task WhenAsksForTheirAccount(string name) =>
        world.LastResult = await world.GetMyAccount.Handle(new GetMyAccountQuery(world.Person(name)), CancellationToken.None);

    [When("an API key asks for its account")]
    public async Task WhenAnApiKeyAsksForItsAccount() =>
        world.LastResult = await world.GetMyAccount.Handle(
            new GetMyAccountQuery(Actor.ApiKey(Guid.NewGuid(), "CI key", Guid.NewGuid(), ["read"])), CancellationToken.None);

    [Then("the account shown is {word}'s")]
    public void ThenTheAccountShownIs(string name)
    {
        var account = world.Last<GetMyAccountResult>().Account.ShouldNotBeNull();
        account.DisplayName.ShouldBe(name);
    }
}
