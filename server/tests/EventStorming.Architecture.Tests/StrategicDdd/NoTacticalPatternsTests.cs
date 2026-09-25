using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using EventStorming.Architecture.Tests.Foundation;
using Xunit;

namespace EventStorming.Architecture.Tests.StrategicDdd;

/// <summary>
/// Strategic DDD only. The core has plain data records, use-case handlers and narrow ports - no
/// aggregates, entities, value objects, domain events, domain services, factories or repositories, and no
/// service or manager layer. ("Aggregate" and "Domain Event" do exist - as element-type ids, which are
/// data in the registry, not C# types.)
/// </summary>
public sealed partial class NoTacticalPatternsTests
{
    public static TheoryData<string> Core => Solution.Data(Solution.Core);

    [Theory]
    [MemberData(nameof(Core))]
    public void No_type_is_named_after_a_tactical_pattern_or_a_service_layer(string assembly)
    {
        var offenders = Solution.TypesIn([assembly])
            .Where(type => TacticalName().IsMatch(type.Name))
            .Select(type => type.FullName)
            .ToList();
        Assert.True(offenders.Count == 0, "Tactical-DDD or layer names in the core: " + string.Join(", ", offenders));
    }

    [Theory]
    [MemberData(nameof(Core))]
    public void No_type_derives_from_a_tactical_base_type(string assembly)
    {
        var offenders = Solution.TypesIn([assembly])
            .Where(type => type is Class @class && Bases(@class).Any(baseName => TacticalName().IsMatch(baseName))
                           || type.ImplementedInterfaces.Any(@interface => TacticalName().IsMatch(@interface.Name.TrimStart('I'))))
            .Select(type => type.FullName)
            .ToList();
        Assert.True(offenders.Count == 0, string.Join(", ", offenders));
    }

    [Theory]
    [MemberData(nameof(Core))]
    public void There_are_no_domain_events_in_code(string assembly)
    {
        var offenders = Solution.TypesIn([assembly])
            .Where(type => type.Name.EndsWith("Event", StringComparison.Ordinal) || type.Name.EndsWith("Events", StringComparison.Ordinal)
                           || type.Name.EndsWith("EventHandler", StringComparison.Ordinal) || type.Name.Contains("Dispatcher", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ToList();
        Assert.True(offenders.Count == 0,
            "Side effects are explicit outbound ports called by the handler, never events raised and dispatched: " + string.Join(", ", offenders));
    }

    [Theory]
    [MemberData(nameof(Core))]
    public void The_model_is_plain_data(string assembly)
    {
        var offenders = Solution.TypesIn([assembly])
            .Where(type => type.Namespace.FullName.EndsWith(".Model", StringComparison.Ordinal))
            .Where(type => type is Class @class && @class.IsRecord != true && !(@class.IsAbstract == true && @class.IsSealed == true))
            .Select(type => type.FullName)
            .ToList();
        Assert.True(offenders.Count == 0, "Model types are records, enums or static helper classes: " + string.Join(", ", offenders));
    }

    private static IEnumerable<string> Bases(Class @class)
    {
        for (var current = @class.BaseClass; current is not null; current = current.BaseClass)
        {
            yield return current.Name;
        }
    }

    [GeneratedRegex("(Aggregate|AggregateRoot|Entity|ValueObject|DomainEvent|DomainService|Repository|Factory|Service|Manager)(`\\d+)?$")]
    private static partial Regex TacticalName();
}
