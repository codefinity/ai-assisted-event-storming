using System.Globalization;
using EventStorming.SharedKernel;
using EventStorming.Specs.Support;
using Reqnroll;
using Shouldly;

namespace EventStorming.Specs.StepDefinitions;

/// <summary>Steps every feature shares: time, and the outcome of the last request.</summary>
[Binding]
public sealed class CommonSteps(World world)
{
    [Given("the time is {string}")]
    public void GivenTheTimeIs(string instant) =>
        world.Clock.UtcNow = DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    [Given("{int} seconds pass")]
    [When("{int} seconds pass")]
    public void SecondsPass(int seconds) => world.Clock.Advance(TimeSpan.FromSeconds(seconds));

    [Given("{int} minutes pass")]
    [When("{int} minutes pass")]
    public void MinutesPass(int minutes) => world.Clock.Advance(TimeSpan.FromMinutes(minutes));

    [Given("{int} days pass")]
    [When("{int} days pass")]
    public void DaysPass(int days) => world.Clock.Advance(TimeSpan.FromDays(days));

    [Then("the request succeeds")]
    public void ThenTheRequestSucceeds()
    {
        world.LastResult.ShouldNotBeNull();
        world.LastResult.Success.ShouldBeTrue(Describe(world.LastResult.Failures));
    }

    [Then("the request fails with {string}")]
    public void ThenTheRequestFailsWith(string code)
    {
        var failures = Refused();
        failures.Select(failure => failure.Code).ShouldContain(code, Describe(failures));
    }

    [Then("the request fails with {string} on {string}")]
    public void ThenTheRequestFailsWithOn(string code, string field)
    {
        var failures = Refused();
        failures.ShouldContain(failure => failure.Code == code && failure.Field == field, Describe(failures));
    }

    [Then("the request fails with these failures:")]
    public void ThenTheRequestFailsWithTheseFailures(DataTable table)
    {
        var failures = Refused();
        var expected = table.Rows.Select(row => (row["field"], row["code"])).OrderBy(pair => pair.Item1).ToList();
        var actual = failures.Select(failure => (failure.Field ?? string.Empty, failure.Code)).OrderBy(pair => pair.Item1).ToList();
        actual.ShouldBe(expected, Describe(failures));
    }

    [Then("the request is refused as {string}")]
    public void ThenTheRequestIsRefusedAs(string kind)
    {
        var failures = Refused();
        var expected = Enum.Parse<FailureKind>(kind.Replace("-", string.Empty, StringComparison.Ordinal), ignoreCase: true);
        failures[0].Kind.ShouldBe(expected, Describe(failures));
    }

    [Then("every failure says how to fix it")]
    public void ThenEveryFailureSaysHowToFixIt()
    {
        var failures = Refused();
        failures.ShouldAllBe(failure => !string.IsNullOrWhiteSpace(failure.Fix), Describe(failures));
    }

    private IReadOnlyList<Failure> Refused()
    {
        world.LastResult.ShouldNotBeNull();
        world.LastResult.Success.ShouldBeFalse("Expected the request to be refused, but it succeeded.");
        return world.LastResult.Failures;
    }

    private static string Describe(IReadOnlyList<Failure> failures) =>
        failures.Count == 0 ? "(no failures)" : string.Join("; ", failures.Select(failure => $"{failure.Kind}/{failure.Code} on '{failure.Field}': {failure.Message}"));
}
