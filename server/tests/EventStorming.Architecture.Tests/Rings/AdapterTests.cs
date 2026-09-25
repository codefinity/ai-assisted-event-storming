using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using EventStorming.Architecture.Tests.Foundation;
using Xunit;

namespace EventStorming.Architecture.Tests.Rings;

/// <summary>
/// How the rings around the core relate: driven adapters are independent of each other (bar a shared
/// .Transport), driving adapters reach the core only through its driving ports, and nothing depends on
/// the host that composes them all.
/// </summary>
public sealed partial class AdapterTests
{
    public static TheoryData<string> DrivenAdapters => Solution.Data(Solution.DrivenAdapters.Except(Solution.Transports));

    public static TheoryData<string> Transports => Solution.Data(Solution.Transports);

    public static TheoryData<string> NonHostDrivingAdapters => Solution.Data(Solution.DrivingAdapters.Except(Solution.Hosts));

    public static TheoryData<string> Hosts => Solution.Data(Solution.Hosts);

    [Theory]
    [MemberData(nameof(DrivenAdapters))]
    public void A_driven_adapter_depends_on_no_other_adapter_but_its_transport(string adapter)
    {
        var forbidden = Solution.Adapters.Where(name => name != adapter && name != adapter + ".Transport").ToList();
        AssertNoDependency(adapter, forbidden);
    }

    [Theory]
    [MemberData(nameof(Transports))]
    public void A_transport_knows_nothing_of_the_core(string transport) =>
        AssertNoDependency(transport, Solution.Core);

    [Theory]
    [MemberData(nameof(NonHostDrivingAdapters))]
    public void A_driving_adapter_touches_no_driven_technology_or_other_driving_adapter(string adapter)
    {
        var forbidden = Solution.Adapters.Where(name => name != adapter && !Solution.Transports.Contains(name)).ToList();
        AssertNoDependency(adapter, forbidden);
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Nothing_depends_on_a_host(string host)
    {
        foreach (var other in Solution.All.Where(name => name != host))
        {
            AssertNoDependency(other, [host]);
        }
    }

    [Fact]
    public void Driven_adapters_never_call_use_cases()
    {
        var violations = Solution.TypesIn(Solution.DrivenAdapters)
            .SelectMany(type => type.Dependencies.Select(dependency => (Type: type, Target: dependency.Target)))
            .Where(pair => pair.Target is Interface && DrivingPort().IsMatch(pair.Target.Name) && Solution.IsIn(pair.Target, Solution.Contexts))
            .Select(pair => $"{pair.Type.FullName} -> {pair.Target.FullName}")
            .Distinct()
            .ToList();
        Assert.True(violations.Count == 0, "A driven adapter that invokes a use case is driving the core, not driven by it:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Adapters_reach_use_cases_through_their_ports_only()
    {
        var implementations = Solution.TypesIn(Solution.Contexts)
            .OfType<Class>()
            .Where(type => UseCases.IsSliceNamespace(type.Namespace.FullName)
                           && (type.ImplementedInterfaces.Any(port => DrivingPort().IsMatch(port.Name)) || type.Name.EndsWith("Validator", StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        var violations = Solution.TypesIn(Solution.Adapters)
            .SelectMany(type => type.Dependencies.Select(dependency => (Type: type, Target: dependency.Target)))
            .Where(pair => implementations.Contains(pair.Target.FullName))
            .Select(pair => $"{pair.Type.FullName} -> {pair.Target.FullName}")
            .Distinct()
            .ToList();
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static void AssertNoDependency(string assembly, IReadOnlyCollection<string> forbidden)
    {
        var violations = Solution.TypesIn([assembly])
            .SelectMany(type => type.Dependencies.Select(dependency => (Type: type, Target: dependency.Target)))
            .Where(pair => Solution.IsIn(pair.Target, forbidden))
            .Select(pair => $"{pair.Type.FullName} -> {pair.Target.FullName} ({Solution.AssemblyName(pair.Target)})")
            .Distinct()
            .ToList();
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [GeneratedRegex("^I[A-Za-z]+(Command|Query)Handler$")]
    private static partial Regex DrivingPort();
}
