using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;

namespace EventStorming.ElementTypes.Json;

/// <summary>
/// The element-type registry, read from element-types.json: the copy embedded in this assembly, or a
/// file given in configuration, which lets a deployment tune colors and types without a rebuild. The
/// file is validated once at startup, so a broken registry stops the host instead of confusing users.
/// </summary>
internal sealed partial class JsonElementTypeRegistry : IElementTypeRegistry
{
    private readonly Dictionary<string, ElementType> byId;

    public JsonElementTypeRegistry(string json)
    {
        var file = JsonSerializer.Deserialize<RegistryFile>(json, Options)
            ?? throw new InvalidOperationException("element-types.json is empty.");

        Levels = file.Levels.Select(level => new LevelDescription(ParseLevel(level.Id), level.Name, level.Description)).ToList();
        Types = file.Types.Select(ToElementType).ToList();
        Validate(Types);
        byId = Types.ToDictionary(type => type.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<ElementType> Types { get; }

    public IReadOnlyList<LevelDescription> Levels { get; }

    public ElementType? Find(string typeId) => byId.GetValueOrDefault(typeId);

    public static string EmbeddedJson()
    {
        using var stream = typeof(JsonElementTypeRegistry).Assembly.GetManifestResourceStream("element-types.json")
            ?? throw new InvalidOperationException("The embedded element-types.json is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static ElementType ToElementType(TypeEntry entry) => new(
        entry.Id,
        entry.Name,
        entry.Category switch
        {
            "sticky" => ElementCategory.Sticky,
            "structure" => ElementCategory.Structure,
            _ => throw Invalid(entry.Id, $"category '{entry.Category}' must be sticky or structure"),
        },
        entry.Renderer,
        entry.Color,
        entry.TextColor,
        entry.Icon,
        new Size(entry.DefaultSize.Width, entry.DefaultSize.Height),
        entry.Levels.Select(ParseLevel).ToList(),
        entry.Shortcut,
        entry.MaxTextLength,
        entry.CanBePivotal,
        entry.LayoutRole switch
        {
            "item" => LayoutRole.Item,
            "lane" => LayoutRole.Lane,
            "boundary" => LayoutRole.Boundary,
            _ => throw Invalid(entry.Id, $"layoutRole '{entry.LayoutRole}' must be item, lane or boundary"),
        },
        entry.Description,
        entry.WhenToUse,
        entry.WritingRule,
        entry.Examples);

    private static void Validate(IReadOnlyList<ElementType> types)
    {
        foreach (var duplicate in types.GroupBy(type => type.Id).Where(group => group.Count() > 1))
        {
            throw Invalid(duplicate.Key, "the id is used more than once");
        }

        foreach (var duplicate in types.Where(type => type.Shortcut is not null).GroupBy(type => type.Shortcut!.ToUpperInvariant()).Where(group => group.Count() > 1))
        {
            throw Invalid(duplicate.First().Id, $"shortcut '{duplicate.Key}' is also used by {string.Join(", ", duplicate.Skip(1).Select(type => type.Id))}");
        }

        foreach (var type in types)
        {
            if (!KebabCase().IsMatch(type.Id))
            {
                throw Invalid(type.Id, "ids are kebab-case, e.g. \"domain-event\"");
            }

            if (!HexColor().IsMatch(type.Color) || !HexColor().IsMatch(type.TextColor))
            {
                throw Invalid(type.Id, "color and textColor must look like #RRGGBB");
            }

            if (type.MaxTextLength is < 1 or > BoardLimits.MaxTextLength)
            {
                throw Invalid(type.Id, $"maxTextLength must be between 1 and {BoardLimits.MaxTextLength}");
            }

            if (type.DefaultSize.Width < BoardLimits.MinSize || type.DefaultSize.Height < BoardLimits.MinSize)
            {
                throw Invalid(type.Id, $"defaultSize must be at least {BoardLimits.MinSize} in each direction");
            }
        }
    }

    private static BoardLevel ParseLevel(string level) =>
        BoardLevels.IsValid(level) ? BoardLevels.Parse(level) : throw new InvalidOperationException($"element-types.json: unknown level '{level}'.");

    private static InvalidOperationException Invalid(string typeId, string problem) =>
        new($"element-types.json: type '{typeId}' is invalid: {problem}.");

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$")]
    private static partial Regex KebabCase();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();

    private sealed record RegistryFile(IReadOnlyList<LevelEntry> Levels, IReadOnlyList<TypeEntry> Types);

    private sealed record LevelEntry(string Id, string Name, string Description);

    private sealed record SizeEntry(double Width, double Height);

    private sealed record TypeEntry(
        string Id,
        string Name,
        string Category,
        string Renderer,
        string Color,
        string TextColor,
        string Icon,
        SizeEntry DefaultSize,
        IReadOnlyList<string> Levels,
        string? Shortcut,
        int MaxTextLength,
        bool CanBePivotal,
        string LayoutRole,
        string Description,
        string WhenToUse,
        string? WritingRule,
        IReadOnlyList<string> Examples);
}
