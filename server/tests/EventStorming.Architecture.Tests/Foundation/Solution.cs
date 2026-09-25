using Xunit;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchModel = ArchUnitNET.Domain.Architecture;

namespace EventStorming.Architecture.Tests.Foundation;

public enum Ring
{
    Core,
    DrivingAdapter,
    DrivenAdapter,
}

/// <param name="IsHost">An executable composition root (Sdk.Web or OutputType Exe).</param>
public sealed record Project(string Name, Ring Ring, string CsprojPath, bool IsHost)
{
    /// <summary>A shared wire-format project, the one allowed exception to "adapters do not reference each other".</summary>
    public bool IsTransport => Name.EndsWith(".Transport", StringComparison.Ordinal);
}

/// <summary>
/// The system under test, discovered rather than listed: every project under server/src, classified by
/// the ring folder it lives in, and its assembly loaded for ArchUnitNET. A new project or bounded context
/// is covered the moment it exists. Discovery never throws; problems land in <see cref="Anomalies"/>,
/// which Diagnostics reports, so no rule can pass vacuously because something failed to load.
/// </summary>
public static class Solution
{
    public const string SolutionFile = "EventStorming.slnx";
    public const string SharedKernel = "EventStorming.SharedKernel";

    private static readonly (string Folder, Ring Ring)[] RingFolders =
    [
        ("application", Ring.Core),
        ("driving-adapters", Ring.DrivingAdapter),
        ("driven-adapters", Ring.DrivenAdapter),
    ];

    static Solution()
    {
        var anomalies = new List<string>();
        var projects = new List<Project>();
        ServerRoot = FindServerRoot(anomalies);
        if (ServerRoot is not null)
        {
            foreach (var (folder, ring) in RingFolders)
            {
                var ringDirectory = Path.Combine(ServerRoot, "src", folder);
                if (!Directory.Exists(ringDirectory))
                {
                    anomalies.Add($"The ring folder src/{folder} does not exist.");
                    continue;
                }

                foreach (var directory in Directory.EnumerateDirectories(ringDirectory))
                {
                    var csproj = Directory.GetFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly);
                    if (csproj.Length != 1)
                    {
                        anomalies.Add($"'{directory}' should contain exactly one .csproj, found {csproj.Length}.");
                        continue;
                    }

                    projects.Add(new Project(Path.GetFileNameWithoutExtension(csproj[0]), ring, csproj[0], IsHostProject(csproj[0])));
                }
            }
        }

        Projects = projects;
        Anomalies = anomalies;
        Architecture = Load(projects);
    }

    public static string? ServerRoot { get; }

    public static IReadOnlyList<Project> Projects { get; }

    public static IReadOnlyList<string> Anomalies { get; }

    public static ArchModel Architecture { get; }

    public static IReadOnlyList<string> Core => Names(project => project.Ring == Ring.Core);

    /// <summary>The bounded contexts: every core assembly except the Shared Kernel.</summary>
    public static IReadOnlyList<string> Contexts => Names(project => project.Ring == Ring.Core && project.Name != SharedKernel);

    public static IReadOnlyList<string> DrivingAdapters => Names(project => project.Ring == Ring.DrivingAdapter);

    public static IReadOnlyList<string> DrivenAdapters => Names(project => project.Ring == Ring.DrivenAdapter);

    public static IReadOnlyList<string> Hosts => Names(project => project.IsHost);

    public static IReadOnlyList<string> Transports => Names(project => project.IsTransport);

    public static IReadOnlyList<string> Adapters => Names(project => project.Ring != Ring.Core);

    public static IReadOnlyList<string> All => Names(_ => true);

    public static bool IsIn(IType type, IEnumerable<string> assemblyNames) => assemblyNames.Contains(AssemblyName(type));

    public static string AssemblyName(IType type) => type.Assembly?.FullName.Split(',')[0] ?? string.Empty;

    public static TheoryData<string> Data(IEnumerable<string> values)
    {
        var data = new TheoryData<string>();
        foreach (var value in values.OrderBy(value => value, StringComparer.Ordinal))
        {
            data.Add(value);
        }

        return data;
    }

    /// <summary>Types written by hand - not compiler artefacts such as closures, state machines or the synthesized Program.</summary>
    public static bool IsHandWritten(IType type) =>
        !type.FullName.Contains('<') && !type.FullName.Contains('/') && !type.FullName.Contains('+') && type.Name != "Program"
        && !type.FullName.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)
        && !type.FullName.StartsWith("System.", StringComparison.Ordinal)
        && !type.Name.EndsWith("Attribute", StringComparison.Ordinal)
        && (type is not Class @class || @class.IsCompilerGenerated != true)
        && !Regex.IsMatch(type.Name, "^(RegexGenerator|Utilities)$");

    public static IEnumerable<IType> TypesIn(IEnumerable<string> assemblyNames)
    {
        var names = assemblyNames.ToHashSet(StringComparer.Ordinal);
        return Architecture.Types.Where(type => names.Contains(AssemblyName(type)) && IsHandWritten(type));
    }

    private static IReadOnlyList<string> Names(Func<Project, bool> predicate) =>
        Projects.Where(predicate).Select(project => project.Name).OrderBy(name => name, StringComparer.Ordinal).ToList();

    private static ArchModel Load(IReadOnlyList<Project> projects)
    {
        var loader = new ArchLoader();
        foreach (var project in projects)
        {
            loader = loader.LoadFilteredDirectory(AppContext.BaseDirectory, $"{project.Name}.dll");
        }

        return loader.Build();
    }

    private static string? FindServerRoot(List<string> anomalies)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
            {
                return directory.FullName;
            }
        }

        anomalies.Add($"Could not find {SolutionFile} above {AppContext.BaseDirectory}.");
        return null;
    }

    private static bool IsHostProject(string csprojPath)
    {
        var root = XDocument.Load(csprojPath).Root!;
        var sdk = (string?)root.Attribute("Sdk") ?? string.Empty;
        var outputType = root.Descendants("OutputType").Select(element => element.Value).FirstOrDefault();
        return sdk.Contains("Sdk.Web", StringComparison.OrdinalIgnoreCase) || outputType is "Exe" or "WinExe";
    }
}
