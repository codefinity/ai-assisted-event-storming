using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EventStorming.Host.IntegrationTests.Support;
using Shouldly;
using Xunit;

namespace EventStorming.Host.IntegrationTests;

public sealed class SessionAndErrorTests(MongoServer mongo) : IAsyncLifetime
{
    private ApiHost host = null!;

    public ValueTask InitializeAsync()
    {
        host = new ApiHost(mongo.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await host.DisposeAsync();

    [Fact]
    public async Task Signing_up_starts_a_session_that_the_refresh_cookie_renews_until_sign_out()
    {
        var ana = await host.SignUp("Ana");
        (await ana.Get("/api/app/me")).GetProperty("displayName").GetString().ShouldBe("Ana");

        var withoutHeader = await ana.Client.PostAsync("/api/app/auth/refresh", null);
        withoutHeader.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Problem(withoutHeader)).GetProperty("code").GetString().ShouldBe("missing-client-header");

        var refreshed = await Refresh(ana.Client);
        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await refreshed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString().ShouldNotBe(ana.Token);

        var signOut = new HttpRequestMessage(HttpMethod.Post, "/api/app/auth/sign-out");
        signOut.Headers.Add("X-Requested-With", "fetch");
        (await ana.Client.SendAsync(signOut)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterSignOut = await Refresh(ana.Client);
        afterSignOut.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Problem(afterSignOut)).GetProperty("code").GetString().ShouldBe("session-expired");
    }

    [Fact]
    public async Task The_refresh_cookie_is_http_only_and_scoped_to_the_session_endpoints()
    {
        var client = host.Browser();
        var response = await client.PostAsJsonAsync("/api/app/auth/sign-up", new { email = $"c-{Guid.NewGuid():N}@example.com", displayName = "Cy", password = "correct horse battery" });
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("es_refresh=", StringComparison.Ordinal));
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("samesite=strict", Case.Insensitive);
        cookie.ShouldContain("path=/api/app/auth", Case.Insensitive);
    }

    [Fact]
    public async Task Validation_failures_name_every_field_with_a_pointer_and_a_fix()
    {
        var response = await host.CreateClient().PostAsJsonAsync("/api/app/auth/sign-up", new { email = "nope", displayName = "", password = "short" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await Problem(response);
        problem.GetProperty("type").GetString().ShouldBe("/api/v1/problems/validation-failed");
        var errors = problem.GetProperty("errors").EnumerateArray().ToList();
        errors.Select(error => error.GetProperty("pointer").GetString()).ShouldBe(["#/email", "#/displayName", "#/password"], ignoreOrder: true);
        errors.ShouldAllBe(error => error.GetProperty("fix").GetString()!.Length > 0);
    }

    [Fact]
    public async Task Unreadable_json_is_a_400_that_points_at_where_it_broke()
    {
        var response = await host.CreateClient().PostAsync("/api/app/auth/sign-in", new StringContent("{\"email\": 42}", Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await Problem(response);
        problem.GetProperty("code").GetString().ShouldBe("malformed-request");
        problem.GetProperty("pointer").GetString().ShouldBe("#/email");
    }

    [Fact]
    public async Task Missing_credentials_and_unknown_routes_are_problems_too()
    {
        var unauthenticated = await host.CreateClient().GetAsync("/api/app/me");
        unauthenticated.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Problem(unauthenticated)).GetProperty("type").GetString().ShouldBe("/api/v1/problems/unauthenticated");

        var missing = await host.CreateClient().GetAsync("/api/app/nothing-here");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Problem(missing)).GetProperty("type").GetString().ShouldBe("/api/v1/problems/not-found");
    }

    [Fact]
    public async Task Cors_admits_the_web_app_with_credentials_and_nobody_else()
    {
        var client = host.CreateClient();

        var allowed = new HttpRequestMessage(HttpMethod.Options, "/api/app/teams");
        allowed.Headers.Add("Origin", ApiHost.WebOrigin);
        allowed.Headers.Add("Access-Control-Request-Method", "POST");
        allowed.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        var preflight = await client.SendAsync(allowed);
        preflight.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([ApiHost.WebOrigin]);
        preflight.Headers.GetValues("Access-Control-Allow-Credentials").ShouldBe(["true"]);

        var other = new HttpRequestMessage(HttpMethod.Options, "/api/app/teams");
        other.Headers.Add("Origin", "https://evil.example");
        other.Headers.Add("Access-Control-Request-Method", "POST");
        (await client.SendAsync(other)).Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task A_member_sees_the_team_and_its_boards_and_an_outsider_sees_nothing()
    {
        var ana = await host.SignUp("Ana");
        var team = await ana.CreateTeam("Food delivery");
        var board = await ana.CreateBoard(team, "Ordering", "process-modelling");

        var list = await ana.Get($"/api/app/teams/{team}/boards");
        list.GetProperty("canEdit").GetBoolean().ShouldBeTrue();
        list.GetProperty("items")[0].GetProperty("level").GetString().ShouldBe("process-modelling");

        var snapshot = await ana.Get($"/api/app/boards/{board}");
        snapshot.GetProperty("permission").GetString().ShouldBe("edit");

        var bo = await host.SignUp("Bo");
        (await bo.Client.GetAsync($"/api/app/boards/{board}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await bo.Client.GetAsync($"/api/app/teams/{team}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_invitation_link_brings_someone_into_the_team()
    {
        var ana = await host.SignUp("Ana");
        var team = await ana.CreateTeam("Food delivery");
        var token = (await ana.Post($"/api/app/teams/{team}/invitations", new { role = "editor" })).GetProperty("token").GetString()!;

        var anonymous = await host.CreateClient().GetFromJsonAsync<JsonElement>($"/api/app/invitations/{token}");
        anonymous.GetProperty("teamName").GetString().ShouldBe("Food delivery");
        anonymous.GetProperty("status").GetString().ShouldBe("valid");

        var bo = await host.SignUp("Bo");
        (await bo.Post($"/api/app/invitations/{token}/accept", new { }, HttpStatusCode.OK)).GetProperty("myRole").GetString().ShouldBe("editor");
        (await bo.Get($"/api/app/teams/{team}")).GetProperty("members").GetArrayLength().ShouldBe(2);
    }

    private static async Task<HttpResponseMessage> Refresh(HttpClient client)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/app/auth/refresh");
        request.Headers.Add("X-Requested-With", "fetch");
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> Problem(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
}
