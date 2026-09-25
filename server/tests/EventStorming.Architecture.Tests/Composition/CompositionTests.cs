using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using EventStorming.Architecture.Tests.Foundation;
using Xunit;

namespace EventStorming.Architecture.Tests.Composition;

/// <summary>
/// How the pieces are wired: every project but the Shared Kernel and the host publishes exactly one
/// public static *ServiceExtensions class, and every driven port of the core has an adapter.
/// </summary>
public sealed partial class CompositionTests
{
    public static TheoryData<string> Registering =>
        Solution.Data(Solution.All.Except(Solution.Hosts).Except([Solution.SharedKernel]));

    [Theory]
    [MemberData(nameof(Registering))]
    public void The_project_has_exactly_one_public_static_registration_class(string project)
    {
        var registrations = Solution.TypesIn([project])
            .OfType<Class>()
            .Where(type => type.Name.EndsWith("ServiceExtensions", StringComparison.Ordinal))
            .ToList();

        var registration = Assert.Single(registrations);
        Assert.Equal(Visibility.Public, registration.Visibility);
        Assert.True(registration.IsAbstract == true && registration.IsSealed == true, $"{registration.Name} should be static.");
    }

    [Fact]
    public void Every_driven_port_has_an_adapter()
    {
        var ports = Solution.TypesIn(Solution.Core)
            .OfType<Interface>()
            .Where(port => port.Namespace.FullName.EndsWith(".Shared", StringComparison.Ordinal) && port.Name != "IUseCaseResult"
                           || StorePort().IsMatch(port.Name)
                           || port.Name == "IClock")
            .ToList();
        Assert.NotEmpty(ports);

        var implemented = Solution.TypesIn(Solution.DrivenAdapters)
            .OfType<Class>()
            .SelectMany(type => type.ImplementedInterfaces)
            .Select(@interface => @interface.FullName)
            .ToHashSet(StringComparer.Ordinal);

        var unimplemented = ports.Where(port => !implemented.Contains(port.FullName)).Select(port => port.FullName).ToList();
        Assert.True(unimplemented.Count == 0, "Ports with no driven adapter: " + string.Join(", ", unimplemented));
    }

    [GeneratedRegex("^I[A-Za-z]+Store$")]
    private static partial Regex StorePort();
}
