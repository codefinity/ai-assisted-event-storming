using System.Xml.Linq;
using EventStorming.Architecture.Tests.Foundation;
using Xunit;

namespace EventStorming.Architecture.Tests.Diagnostics;

/// <summary>
/// The suite checking that it is looking at the code. Every other rule filters the loaded architecture;
/// if discovery or loading silently failed, those rules would pass over nothing. These must be green
/// before any other result is trusted.
/// </summary>
public sealed class DiscoveryTests
{
    public static TheoryData<string> Projects => Solution.Data(Solution.All);

    public static TheoryData<string> Contexts => Solution.Data(Solution.Contexts);

    [Fact]
    public void Discovery_recorded_no_anomalies()
    {
        var anomalies = Solution.Anomalies.Concat(UseCases.Anomalies).ToList();
        Assert.True(anomalies.Count == 0, string.Join(Environment.NewLine, anomalies));
    }

    [Fact]
    public void Every_ring_has_projects_and_there_is_a_host()
    {
        Assert.NotEmpty(Solution.Contexts);
        Assert.NotEmpty(Solution.DrivingAdapters);
        Assert.NotEmpty(Solution.DrivenAdapters);
        Assert.NotEmpty(Solution.Hosts);
    }

    [Theory]
    [MemberData(nameof(Projects))]
    public void Every_project_was_loaded(string project) =>
        Assert.True(
            Solution.Architecture.Assemblies.Any(assembly => assembly.FullName.Split(',')[0] == project),
            $"{project}.dll is not in the test output. Rebuild; every project under src is a ProjectReference of this suite.");

    [Theory]
    [MemberData(nameof(Projects))]
    public void Every_project_is_listed_in_the_solution(string project)
    {
        var listed = XDocument.Load(Path.Combine(Solution.ServerRoot!, Solution.SolutionFile))
            .Descendants("Project")
            .Select(element => Path.GetFileNameWithoutExtension((string)element.Attribute("Path")!));
        Assert.Contains(project, listed);
    }

    [Theory]
    [MemberData(nameof(Contexts))]
    public void Every_bounded_context_has_use_cases(string context) =>
        Assert.Contains(UseCases.ByName.Values, useCase => useCase.Context == context);
}
