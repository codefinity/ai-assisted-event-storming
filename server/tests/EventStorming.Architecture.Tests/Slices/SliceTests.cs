using ArchUnitNET.Domain;
using EventStorming.Architecture.Tests.Foundation;
using Xunit;

namespace EventStorming.Architecture.Tests.Slices;

/// <summary>
/// The shape of every vertical slice, fanned out over every use case discovered: one folder owning its
/// message, result, driving port, handler, validation and the narrow stores it needs - and reaching no
/// other slice.
/// </summary>
public sealed class SliceTests
{
    public static TheoryData<string> Names => UseCases.Names;

    [Theory]
    [MemberData(nameof(Names))]
    public void The_message_is_a_public_record(string name)
    {
        var useCase = UseCases.ByName[name];
        var message = Assert.Single(UseCases.TypesOf(useCase).OfType<Class>(), type => type.Name == useCase.Message);
        Assert.True(message.IsRecord == true && message.Visibility == Visibility.Public, $"{useCase.Message} should be a public record.");
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void The_result_reports_failures_and_cannot_be_constructed_directly(string name)
    {
        var useCase = UseCases.ByName[name];
        var result = Assert.Single(UseCases.TypesOf(useCase).OfType<Class>(), type => type.Name == useCase.Result);

        Assert.Contains(result.ImplementedInterfaces, @interface => @interface.Name == "IUseCaseResult");
        Assert.DoesNotContain(result.Constructors, constructor => constructor.Visibility == Visibility.Public);
        Assert.Contains(result.Members.OfType<MethodMember>(), method => method.IsStatic == true && method.Name.StartsWith("Succeeded(", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Exactly_one_sealed_handler_implements_the_port_inside_the_slice(string name)
    {
        var useCase = UseCases.ByName[name];
        var implementations = Solution.TypesIn(Solution.All)
            .OfType<Class>()
            .Where(type => type.ImplementedInterfaces.Any(@interface => @interface.FullName == $"{useCase.Namespace}.{useCase.HandlerInterface}"))
            .ToList();

        var handler = Assert.Single(implementations);
        Assert.Equal(useCase.HandlerClass, handler.Name);
        Assert.Equal(useCase.Namespace, handler.Namespace.FullName);
        Assert.True(handler.IsSealed == true, $"{handler.Name} should be sealed.");
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Every_other_port_in_the_slice_is_a_narrow_store(string name)
    {
        var useCase = UseCases.ByName[name];
        var ports = UseCases.TypesOf(useCase).OfType<Interface>().Where(@interface => @interface.Name != useCase.HandlerInterface).ToList();
        Assert.All(ports, port => Assert.Matches("^I[A-Za-z]+Store$", port.Name));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void The_slice_reaches_no_other_slice(string name)
    {
        var useCase = UseCases.ByName[name];
        var violations = UseCases.TypesOf(useCase)
            .SelectMany(type => type.Dependencies.Select(dependency => (Type: type, Target: dependency.Target)))
            .Where(pair => UseCases.IsSliceNamespace(pair.Target.Namespace.FullName) && pair.Target.Namespace.FullName != useCase.Namespace)
            .Select(pair => $"{pair.Type.FullName} -> {pair.Target.FullName}")
            .Distinct()
            .ToList();
        Assert.True(violations.Count == 0, "Slices share nothing but their context's Model and Shared folders:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void The_context_registers_the_handler(string name)
    {
        var useCase = UseCases.ByName[name];
        var registration = Assert.Single(
            Solution.TypesIn([useCase.Context]).OfType<Class>(),
            type => type.Name.EndsWith("ServiceExtensions", StringComparison.Ordinal));
        var targets = registration.Dependencies.Select(dependency => dependency.Target.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains(useCase.HandlerInterface, targets);
        Assert.Contains(useCase.HandlerClass, targets);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void A_validator_validates_its_own_slice(string name)
    {
        var useCase = UseCases.ByName[name];
        var validator = UseCases.TypesOf(useCase).OfType<Class>().SingleOrDefault(type => type.Name == $"{useCase.Message}Validator");
        if (validator is null)
        {
            return;
        }

        Assert.Contains(validator.Dependencies, dependency =>
            dependency.Target.Name.StartsWith("AbstractValidator", StringComparison.Ordinal)
            && dependency.TargetGenericArguments.Any(argument => argument.Type.Name == useCase.Message));
    }
}
