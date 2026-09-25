namespace EventStorming.BoardModelling.Model;

public enum ElementCategory
{
    Sticky,
    Structure,
}

/// <summary>
/// How auto-layout treats a type. This is a closed set of layout semantics, independent of the open
/// set of types: a new "Phase" type could simply declare itself a <see cref="Boundary"/>.
/// </summary>
public enum LayoutRole
{
    Item,
    Lane,
    Boundary,
}

/// <summary>
/// One entry of the element-type registry: everything the tool knows about a kind of sticky or
/// structure, defined as data. Rendering, palettes, shortcuts, tooltips and validation all read it, so a
/// new type needs a registry entry and nothing else on the server.
/// </summary>
public sealed record ElementType(
    string Id,
    string Name,
    ElementCategory Category,
    string Renderer,
    string Color,
    string TextColor,
    string Icon,
    Size DefaultSize,
    IReadOnlyList<BoardLevel> Levels,
    string? Shortcut,
    int MaxTextLength,
    bool CanBePivotal,
    LayoutRole LayoutRole,
    string Description,
    string WhenToUse,
    string? WritingRule,
    IReadOnlyList<string> Examples);

public sealed record LevelDescription(BoardLevel Level, string Name, string Description);
