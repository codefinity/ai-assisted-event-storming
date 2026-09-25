using EventStorming.PublicIntegration.Model;
using EventStorming.PublicIntegration.Slices.AuthenticateApiKey;
using EventStorming.PublicIntegration.Slices.CreateApiKey;
using EventStorming.PublicIntegration.Slices.ListApiKeys;
using EventStorming.PublicIntegration.Slices.RecordIdempotentResponse;
using EventStorming.PublicIntegration.Slices.ReserveIdempotencyKey;
using EventStorming.PublicIntegration.Slices.RevokeApiKey;
using EventStorming.SharedKernel;
using EventStorming.Specs.Support;
using Reqnroll;
using Shouldly;

namespace EventStorming.Specs.StepDefinitions;

[Binding]
public sealed class PublicIntegrationSteps(World world)
{
    private static readonly Guid CallerKey = Guid.Parse("11111111-2222-3333-4444-555555555555");

    /// <summary>An API key as an actor, for scenarios about what a key may do rather than about keys.</summary>
    [Given("{string} has an API key {word} with scopes {string}")]
    public void GivenTheTeamHasAnApiKey(string team, string name, string scopes) =>
        world.ApiKeys[name] = Actor.ApiKey(Guid.CreateVersion7(), name, world.Team(team).Id, ApiScopes.Normalize(Split(scopes)));

    [When("{word} creates an API key {string} for {string} with scopes {string}")]
    [Given("{word} has created an API key {string} for {string} with scopes {string}")]
    public async Task WhenCreatesAnApiKey(string person, string name, string team, string scopes)
    {
        var result = await world.CreateApiKey.Handle(new CreateApiKeyCommand(world.Person(person), world.Team(team).Id, name, Split(scopes)), CancellationToken.None);
        world.LastResult = result;
        if (result.Success)
        {
            world.PresentedKeys[name] = result.Created!.Key;
        }
    }

    [When("{word} creates an API key {string} for {string} that expired yesterday")]
    public async Task WhenCreatesAnExpiredApiKey(string person, string name, string team) =>
        world.LastResult = await world.CreateApiKey.Handle(
            new CreateApiKeyCommand(world.Person(person), world.Team(team).Id, name, ["read"], world.Clock.UtcNow.AddDays(-1)), CancellationToken.None);

    [Then("the key has the scopes {string}")]
    public void ThenTheKeyHasTheScopes(string scopes) =>
        world.Last<CreateApiKeyResult>().Created!.Summary.Scopes.ShouldBe(Split(scopes));

    [Then("the full key is shown once, and only the hash of its secret is kept")]
    public void ThenTheFullKeyIsShownOnce()
    {
        var created = world.Last<CreateApiKeyResult>().Created.ShouldNotBeNull();
        created.Key.ShouldStartWith("es_");
        ApiKeyFormat.TryParse(created.Key, out var id, out var secret).ShouldBeTrue();
        id.ShouldBe(created.Summary.Id);
        world.PublicIntegration.Keys.Single().SecretHash.ShouldBe(world.Secrets.HashOf(secret));
    }

    [When("{word} lists the API keys of {string}")]
    public async Task WhenListsTheApiKeys(string person, string team) =>
        world.LastResult = await world.ListApiKeys.Handle(new ListApiKeysQuery(world.Person(person), world.Team(team).Id), CancellationToken.None);

    [Then("{int} API key(s) is/are listed")]
    public void ThenApiKeysAreListed(int count) => world.Last<ListApiKeysResult>().Keys.Count.ShouldBe(count);

    [When("{word} revokes the API key {string}")]
    [Given("{word} has revoked the API key {string}")]
    public async Task WhenRevokes(string person, string name)
    {
        var key = world.PublicIntegration.Keys.Single(candidate => candidate.Name == name);
        world.LastResult = await world.RevokeApiKey.Handle(new RevokeApiKeyCommand(world.Person(person), key.TeamId, key.Id), CancellationToken.None);
    }

    [When("the API key {string} is presented")]
    public async Task WhenTheKeyIsPresented(string name) =>
        world.LastResult = await world.AuthenticateApiKey.Handle(new AuthenticateApiKeyCommand(world.PresentedKeys[name]), CancellationToken.None);

    [When("the text {string} is presented as an API key")]
    public async Task WhenTextIsPresented(string text) =>
        world.LastResult = await world.AuthenticateApiKey.Handle(new AuthenticateApiKeyCommand(text), CancellationToken.None);

    [When("the API key {string} is presented with a wrong secret")]
    public async Task WhenPresentedWithAWrongSecret(string name)
    {
        var key = world.PresentedKeys[name];
        world.LastResult = await world.AuthenticateApiKey.Handle(new AuthenticateApiKeyCommand(key[..^4] + "XXXX"), CancellationToken.None);
    }

    [Then("it stands for {string} with the scopes {string}")]
    public void ThenItStandsFor(string team, string scopes)
    {
        var key = world.Last<AuthenticateApiKeyResult>().Key.ShouldNotBeNull();
        key.TeamId.ShouldBe(world.Team(team).Id);
        key.Scopes.ShouldBe(Split(scopes));
    }

    [Then("the API key {string} was last used just now")]
    public void ThenTheKeyWasLastUsed(string name) =>
        world.PublicIntegration.Keys.Single(key => key.Name == name).LastUsedAt.ShouldBe(world.Clock.UtcNow);

    [When("a write is reserved with the idempotency key {string} for the request {string}")]
    [Given("a write has been reserved with the idempotency key {string} for the request {string}")]
    public async Task WhenAWriteIsReserved(string key, string request) =>
        world.LastResult = await world.ReserveIdempotencyKey.Handle(new ReserveIdempotencyKeyCommand(CallerKey, key, request), CancellationToken.None);

    [Given("its response {int} {string} has been recorded for the idempotency key {string}")]
    public async Task GivenItsResponseWasRecorded(int status, string body, string key) =>
        await world.RecordIdempotentResponse.Handle(
            new RecordIdempotentResponseCommand(CallerKey, key, new StoredResponse(status, "application/json", body, null)), CancellationToken.None);

    [Given("the write with idempotency key {string} failed on the server")]
    public async Task GivenTheWriteFailed(string key) =>
        await world.RecordIdempotentResponse.Handle(new RecordIdempotentResponseCommand(CallerKey, key, null), CancellationToken.None);

    [Then("the write may run")]
    public void ThenTheWriteMayRun() => world.Last<ReserveIdempotencyKeyResult>().Reservation.ShouldNotBeNull().Replay.ShouldBeNull();

    [Then("the stored response {int} {string} is replayed")]
    public void ThenTheStoredResponseIsReplayed(int status, string body)
    {
        var replay = world.Last<ReserveIdempotencyKeyResult>().Reservation.ShouldNotBeNull().Replay.ShouldNotBeNull();
        (replay.Status, replay.Body).ShouldBe((status, body));
    }

    private static IReadOnlyList<string> Split(string values) =>
        values.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
