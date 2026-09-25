using System.Xml.Linq;
using ArchUnitNET.Domain;
using EventStorming.Architecture.Tests.Foundation;
using Xunit;

namespace EventStorming.Architecture.Tests.Rings;

/// <summary>
/// The application core - every bounded context and the Shared Kernel - knows no framework, database,
/// transport or web technology. It may use the BCL, FluentValidation (a library for its validators)
/// and the DI abstractions (for its one registration class). Anything else is infrastructure leaking in.
/// </summary>
public sealed class CorePurityTests
{
    private static readonly string[] AllowedNamespacePrefixes = ["System", "FluentValidation", "Microsoft.Extensions.DependencyInjection"];

    public static TheoryData<string> Core => Solution.Data(Solution.Core);

    public static TheoryData<string> Contexts => Solution.Data(Solution.Contexts);

    [Theory]
    [MemberData(nameof(Core))]
    public void The_core_references_no_infrastructure(string assembly)
    {
        var violations = Solution.TypesIn([assembly])
            .SelectMany(type => type.Dependencies.Select(dependency => (Type: type, Target: dependency.Target)))
            .Where(pair => !Solution.IsIn(pair.Target, Solution.Core)
                           && !AllowedNamespacePrefixes.Any(prefix => pair.Target.Namespace.FullName.StartsWith(prefix, StringComparison.Ordinal)))
            .Select(pair => $"{pair.Type.FullName} -> {pair.Target.FullName}")
            .Distinct()
            .ToList();

        Assert.True(violations.Count == 0, "The core depends on infrastructure:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [MemberData(nameof(Core))]
    public void The_core_references_no_adapter(string assembly)
    {
        var violations = Violations(assembly, Solution.Adapters);
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [MemberData(nameof(Contexts))]
    public void A_bounded_context_never_references_another(string context)
    {
        var others = Solution.Contexts.Where(name => name != context).ToList();
        var violations = Violations(context, others);
        Assert.True(violations.Count == 0,
            "Contexts talk through ports their adapters implement, never directly:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [MemberData(nameof(Contexts))]
    public void A_bounded_context_project_references_only_the_shared_kernel(string context)
    {
        var project = Solution.Projects.Single(candidate => candidate.Name == context);
        var references = XDocument.Load(project.CsprojPath)
            .Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension((string)element.Attribute("Include")!))
            .ToList();
        Assert.All(references, reference => Assert.Equal(Solution.SharedKernel, reference));
    }

    private static List<string> Violations(string assembly, IReadOnlyList<string> forbidden) =>
        Solution.TypesIn([assembly])
            .SelectMany(type => type.Dependencies.Select(dependency => (Type: type, Target: dependency.Target)))
            .Where(pair => Solution.IsIn(pair.Target, forbidden))
            .Select(pair => $"{pair.Type.FullName} -> {pair.Target.FullName} ({Solution.AssemblyName(pair.Target)})")
            .Distinct()
            .ToList();
}
