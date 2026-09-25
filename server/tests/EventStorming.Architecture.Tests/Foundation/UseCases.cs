using Xunit;
using System.Text.RegularExpressions;
using ArchUnitNET.Domain;

namespace EventStorming.Architecture.Tests.Foundation;

/// <summary>
/// One use case, discovered from its driving port: an interface "I{Name}{Kind}Handler" in the namespace
/// "{Context}.Slices.{Name}". Everything else about the slice is derived from those names.
/// </summary>
public sealed record UseCase(string Context, string Name, string Kind)
{
    public string Namespace => $"{Context}.Slices.{Name}";

    public string HandlerInterface => $"I{Name}{Kind}Handler";

    public string HandlerClass => $"{Name}{Kind}Handler";

    public string Message => $"{Name}{Kind}";

    public string Result => $"{Name}Result";
}

public static partial class UseCases
{
    static UseCases()
    {
        var anomalies = new List<string>();
        var found = new Dictionary<string, UseCase>(StringComparer.Ordinal);

        foreach (var port in Solution.TypesIn(Solution.Contexts).OfType<Interface>())
        {
            var slice = SliceNamespace().Match(port.Namespace.FullName);
            if (!slice.Success)
            {
                continue;
            }

            var handler = HandlerPort().Match(port.Name);
            if (!handler.Success)
            {
                continue;
            }

            if (handler.Groups["name"].Value != slice.Groups["name"].Value)
            {
                anomalies.Add($"{port.FullName} is in slice {slice.Groups["name"].Value} but is named for {handler.Groups["name"].Value}.");
                continue;
            }

            var useCase = new UseCase(slice.Groups["context"].Value, handler.Groups["name"].Value, handler.Groups["kind"].Value);
            if (!found.TryAdd(useCase.Name, useCase))
            {
                anomalies.Add($"Two slices are named {useCase.Name}.");
            }
        }

        ByName = found;
        Anomalies = anomalies;
    }

    public static IReadOnlyDictionary<string, UseCase> ByName { get; }

    public static IReadOnlyList<string> Anomalies { get; }

    public static TheoryData<string> Names => Solution.Data(ByName.Keys);

    /// <summary>Every type in a slice's namespace (a slice is exactly one namespace).</summary>
    public static IReadOnlyList<IType> TypesOf(UseCase useCase) =>
        Solution.TypesIn(Solution.Contexts).Where(type => type.Namespace.FullName == useCase.Namespace).ToList();

    public static bool IsSliceNamespace(string @namespace) => SliceNamespace().IsMatch(@namespace);

    [GeneratedRegex(@"^(?<context>EventStorming\.[A-Za-z]+)\.Slices\.(?<name>[A-Za-z]+)$")]
    private static partial Regex SliceNamespace();

    [GeneratedRegex("^I(?<name>[A-Za-z]+)(?<kind>Command|Query)Handler$")]
    private static partial Regex HandlerPort();
}
