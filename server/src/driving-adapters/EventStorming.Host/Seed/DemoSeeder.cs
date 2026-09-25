using System.Text.Json;
using EventStorming.BoardModelling.Slices.ImportBoardDocument;
using EventStorming.BoardModelling.Slices.ListBoards;
using EventStorming.Identity.Slices.RegisterAccount;
using EventStorming.Identity.Slices.SignIn;
using EventStorming.SharedKernel;
using EventStorming.Teams.Slices.CreateTeam;
using EventStorming.Teams.Slices.ListMyTeams;

namespace EventStorming.Host.Seed;

/// <summary>
/// `dotnet EventStorming.Host.dll seed` (or `docker compose run --rm api seed`): a demo account, a demo
/// team, and the sample "Online food ordering" board. It goes through the real use cases, so the demo
/// data is exactly what the app would have created, and it is safe to run twice.
/// </summary>
internal static class DemoSeeder
{
    public const string Email = "demo@eventstorming.local";
    private const string DefaultPassword = "eventstorming";
    private const string TeamName = "Demo team";

    public static async Task Run(IServiceProvider services, IConfiguration configuration, ILogger logger, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var password = configuration["SEED_DEMO_PASSWORD"] is { Length: > 0 } configured ? configured : DefaultPassword;

        var registered = await provider.GetRequiredService<IRegisterAccountCommandHandler>()
            .Handle(new RegisterAccountCommand(Email, "Demo User", password), cancellationToken);
        if (!registered.Success && registered.Failures[0].Code != "email-taken")
        {
            throw new InvalidOperationException("Could not create the demo account: " + registered.Failures[0].Message);
        }

        var signedIn = await provider.GetRequiredService<ISignInCommandHandler>().Handle(new SignInCommand(Email, password), cancellationToken);
        if (!signedIn.Success)
        {
            throw new InvalidOperationException($"The demo account exists but its password is not the configured one ({signedIn.Failures[0].Message}).");
        }

        var actor = Actor.Account(signedIn.Session!.Account.Id, signedIn.Session.Account.DisplayName);

        var teams = await provider.GetRequiredService<IListMyTeamsQueryHandler>().Handle(new ListMyTeamsQuery(actor), cancellationToken);
        var teamId = teams.Teams.FirstOrDefault(team => team.Name == TeamName)?.Id;
        if (teamId is null)
        {
            var created = await provider.GetRequiredService<ICreateTeamCommandHandler>().Handle(new CreateTeamCommand(actor, TeamName), cancellationToken);
            teamId = created.Team!.Id;
        }

        var sample = SampleDocument();
        var boards = await provider.GetRequiredService<IListBoardsQueryHandler>().Handle(new ListBoardsQuery(actor, teamId.Value, IncludeArchived: true, Limit: 200), cancellationToken);
        if (boards.Page!.Items.All(board => board.Name != sample.Board!.Name))
        {
            var imported = await provider.GetRequiredService<IImportBoardDocumentCommandHandler>()
                .Handle(new ImportBoardDocumentCommand(actor, sample, TeamId: teamId), cancellationToken);
            if (!imported.Success)
            {
                throw new InvalidOperationException("Could not import the sample board: " + string.Join("; ", imported.Failures.Select(failure => $"{failure.Field}: {failure.Message}")));
            }
        }

        logger.LogInformation("Demo data is ready. Sign in as {Email} with password '{Password}'.", Email, password);
    }

    private static BoardDocument SampleDocument()
    {
        using var stream = typeof(DemoSeeder).Assembly.GetManifestResourceStream("food-ordering.board.json")
            ?? throw new InvalidOperationException("The embedded sample board is missing.");
        var dto = JsonSerializer.Deserialize<EventStorming.Api.Rest.Http.BoardDocumentDto>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("The embedded sample board is empty.");
        return dto.ToModel();
    }
}
