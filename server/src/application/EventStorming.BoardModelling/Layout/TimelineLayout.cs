using EventStorming.BoardModelling.Model;

namespace EventStorming.BoardModelling.Layout;

/// <summary>
/// One element to place. <see cref="Key"/> is how other items refer to it; <see cref="Swimlane"/>,
/// <see cref="Boundary"/> and <see cref="Anchor"/> are keys of other items, or of already-placed
/// elements passed in as "existing".
/// </summary>
public sealed record LayoutItem(
    string Key,
    LayoutRole Role,
    Size Size,
    Position? Position,
    string? Swimlane,
    string? Boundary,
    string? Anchor,
    bool Pivotal);

/// <summary>An element already on the board that a new item may anchor to or sit in.</summary>
public sealed record PlacedElement(string Key, LayoutRole Role, double X, double Y, double Width, double Height);

public sealed record LayoutPlacement(Position Position, Size Size);

/// <summary>
/// Places elements whose position the caller left out - typically an LLM, which is poor at choosing
/// coordinates - on a left-to-right timeline. Pure and deterministic: the same input always gives the
/// same layout.
///
/// <list type="bullet">
/// <item>Items are laid out in input order, which is timeline order. Each item starts a new column;
/// columns are shared by every swimlane, so time lines up vertically across lanes.</item>
/// <item>An item with an <see cref="LayoutItem.Anchor"/> is stacked below its anchor, in the same column.</item>
/// <item>A pivotal item gets extra space on both sides.</item>
/// <item>Swimlanes stack top to bottom in input order, each as tall as its tallest stack and as wide as
/// the timeline. Items without a swimlane go in a band above the lanes.</item>
/// <item>A boundary is drawn around its members.</item>
/// <item>Anything given an explicit position keeps it.</item>
/// </list>
/// </summary>
public static class TimelineLayout
{
    public const double ColumnGap = 48;
    public const double StackGap = 24;
    public const double LaneHeader = 180;
    public const double LanePadding = 40;
    public const double BoundaryPadding = 32;
    public const double BoundaryLabelSpace = 40;
    public const double PivotalGap = 96;
    private const double MinimumBandContent = 100;
    private const string DefaultBand = "";

    public static IReadOnlyDictionary<string, LayoutPlacement> Arrange(
        IReadOnlyList<LayoutItem> items,
        Position origin,
        IReadOnlyDictionary<string, PlacedElement> existing)
    {
        var placements = new Dictionary<string, LayoutPlacement>(StringComparer.Ordinal);
        var byKey = items.ToDictionary(item => item.Key, StringComparer.Ordinal);

        foreach (var item in items.Where(item => item.Position is not null))
        {
            placements[item.Key] = new LayoutPlacement(item.Position!, item.Size);
        }

        var autoLanes = items.Where(item => item.Role == LayoutRole.Lane && item.Position is null).ToList();
        var autoItems = items.Where(item => item.Role == LayoutRole.Item && item.Position is null).ToList();

        // 1. Columns, and which band each auto-placed item stacks in.
        var columns = new List<Column>();
        var columnOf = new Dictionary<string, int>(StringComparer.Ordinal);
        var bandOf = new Dictionary<string, string>(StringComparer.Ordinal);
        var belowFixed = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var item in autoItems)
        {
            if (item.Anchor is { } anchor && columnOf.TryGetValue(anchor, out var anchorColumn))
            {
                var band = item.Swimlane is null ? bandOf[anchor] : BandFor(item, byKey);
                columns[anchorColumn].Add(item, band);
                columnOf[item.Key] = anchorColumn;
                bandOf[item.Key] = band;
                continue;
            }

            if (item.Anchor is { } fixedAnchor && FixedRect(fixedAnchor, placements, existing) is { } rect)
            {
                // Anchored to something that already has a position: stack directly beneath it.
                var offset = belowFixed.GetValueOrDefault(fixedAnchor, rect.Y + rect.Height + StackGap);
                placements[item.Key] = new LayoutPlacement(new Position(Round(rect.X), Round(offset)), item.Size);
                belowFixed[fixedAnchor] = offset + item.Size.Height + StackGap;
                continue;
            }

            var column = new Column(item.Pivotal);
            column.Add(item, BandFor(item, byKey));
            columns.Add(column);
            columnOf[item.Key] = columns.Count - 1;
            bandOf[item.Key] = BandFor(item, byKey);
        }

        // 2. Column x positions.
        var hasLanes = autoLanes.Count > 0;
        var x = origin.X + (hasLanes ? LaneHeader : 0);
        var columnX = new double[columns.Count];
        var afterPivotal = false;
        for (var index = 0; index < columns.Count; index++)
        {
            if (index > 0 && (columns[index].Pivotal || afterPivotal))
            {
                x += PivotalGap;
            }

            columnX[index] = x;
            x += columns[index].Width + ColumnGap;
            afterPivotal = columns[index].Pivotal;
        }

        var timelineRight = columns.Count == 0 ? origin.X + (hasLanes ? LaneHeader : 0) : x - ColumnGap;

        // 3. Band y positions: the lane-less band first (if anything is in it), then each new lane.
        var bands = new List<string>();
        if (!hasLanes || columns.Any(column => column.Stacks.ContainsKey(DefaultBand)))
        {
            bands.Add(DefaultBand);
        }

        bands.AddRange(autoLanes.Select(lane => lane.Key));

        var bandTop = new Dictionary<string, double>(StringComparer.Ordinal);
        var bandHeight = new Dictionary<string, double>(StringComparer.Ordinal);
        var y = origin.Y;
        foreach (var band in bands)
        {
            var content = columns
                .Select(column => column.StackHeight(band))
                .DefaultIfEmpty(0)
                .Max();
            bandTop[band] = y;
            bandHeight[band] = Math.Max(content, MinimumBandContent) + (2 * LanePadding);
            y += bandHeight[band];
        }

        // 4. Items.
        for (var index = 0; index < columns.Count; index++)
        {
            foreach (var (band, stack) in columns[index].Stacks)
            {
                var top = bandTop.TryGetValue(band, out var bandY)
                    ? bandY + LanePadding
                    : (FixedRect(band, placements, existing)?.Y ?? origin.Y) + LanePadding;

                foreach (var item in stack)
                {
                    var left = columnX[index] + ((columns[index].Width - item.Size.Width) / 2);
                    placements[item.Key] = new LayoutPlacement(new Position(Round(left), Round(top)), item.Size);
                    top += item.Size.Height + StackGap;
                }
            }
        }

        // 5. Lanes span the timeline.
        foreach (var lane in autoLanes)
        {
            var width = Math.Max(lane.Size.Width, timelineRight - origin.X + LanePadding);
            placements[lane.Key] = new LayoutPlacement(
                new Position(Round(origin.X), Round(bandTop[lane.Key])),
                new Size(Round(width), Round(bandHeight[lane.Key])));
        }

        // 6. Boundaries surround their members.
        var nextFreeX = Math.Max(timelineRight, origin.X) + ColumnGap;
        foreach (var boundary in items.Where(item => item.Role == LayoutRole.Boundary && item.Position is null))
        {
            var members = items
                .Where(item => item.Role == LayoutRole.Item && item.Boundary == boundary.Key && placements.ContainsKey(item.Key))
                .Select(item => placements[item.Key])
                .ToList();

            if (members.Count == 0)
            {
                placements[boundary.Key] = new LayoutPlacement(new Position(Round(nextFreeX), Round(origin.Y)), boundary.Size);
                nextFreeX += boundary.Size.Width + ColumnGap;
                continue;
            }

            var left = members.Min(member => member.Position.X) - BoundaryPadding;
            var top = members.Min(member => member.Position.Y) - BoundaryPadding - BoundaryLabelSpace;
            var right = members.Max(member => member.Position.X + member.Size.Width) + BoundaryPadding;
            var bottom = members.Max(member => member.Position.Y + member.Size.Height) + BoundaryPadding;
            placements[boundary.Key] = new LayoutPlacement(
                new Position(Round(left), Round(top)),
                new Size(Round(right - left), Round(bottom - top)));
        }

        return placements;
    }

    /// <summary>
    /// The swimlanes and boundaries already on the board that placed items name, grown to hold them: a
    /// lane widens (or deepens) to contain its members with padding, a boundary extends to surround its
    /// members. Only structures that have to change are returned, keyed as in <paramref name="existing"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, LayoutPlacement> Enclose(
        IReadOnlyList<LayoutItem> items,
        IReadOnlyDictionary<string, LayoutPlacement> placements,
        IReadOnlyDictionary<string, PlacedElement> existing)
    {
        var memberships = items
            .Where(item => item.Role == LayoutRole.Item && placements.ContainsKey(item.Key))
            .SelectMany(item => new[] { (Structure: item.Swimlane, Item: item), (Structure: item.Boundary, Item: item) })
            .Where(membership => membership.Structure is not null && existing.ContainsKey(membership.Structure))
            .GroupBy(membership => membership.Structure!, StringComparer.OrdinalIgnoreCase);

        var grown = new Dictionary<string, LayoutPlacement>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in memberships)
        {
            var structure = existing[group.Key];
            var members = group.Select(membership => placements[membership.Item.Key]).ToList();
            var (left, top, right, bottom) = (structure.X, structure.Y, structure.X + structure.Width, structure.Y + structure.Height);
            var membersRight = members.Max(member => member.Position.X + member.Size.Width);
            var membersBottom = members.Max(member => member.Position.Y + member.Size.Height);

            if (structure.Role == LayoutRole.Lane)
            {
                right = Math.Max(right, membersRight + LanePadding);
                bottom = Math.Max(bottom, membersBottom + LanePadding);
            }
            else if (structure.Role == LayoutRole.Boundary)
            {
                left = Math.Min(left, members.Min(member => member.Position.X) - BoundaryPadding);
                top = Math.Min(top, members.Min(member => member.Position.Y) - BoundaryPadding - BoundaryLabelSpace);
                right = Math.Max(right, membersRight + BoundaryPadding);
                bottom = Math.Max(bottom, membersBottom + BoundaryPadding);
            }
            else
            {
                continue;
            }

            if (left != structure.X || top != structure.Y || right != structure.X + structure.Width || bottom != structure.Y + structure.Height)
            {
                grown[group.Key] = new LayoutPlacement(new Position(Round(left), Round(top)), new Size(Round(right - left), Round(bottom - top)));
            }
        }

        return grown;
    }

    private static string BandFor(LayoutItem item, IReadOnlyDictionary<string, LayoutItem> byKey) =>
        item.Swimlane is null
            ? DefaultBand
            : byKey.TryGetValue(item.Swimlane, out var lane) && lane.Position is null
                ? lane.Key
                : item.Swimlane;

    private static PlacedElement? FixedRect(string key, IReadOnlyDictionary<string, LayoutPlacement> placements, IReadOnlyDictionary<string, PlacedElement> existing)
    {
        if (existing.TryGetValue(key, out var placed))
        {
            return placed;
        }

        return placements.TryGetValue(key, out var placement)
            ? new PlacedElement(key, LayoutRole.Item, placement.Position.X, placement.Position.Y, placement.Size.Width, placement.Size.Height)
            : null;
    }

    private static double Round(double value) => Math.Round(value, MidpointRounding.AwayFromZero);

    private sealed class Column(bool pivotal)
    {
        public bool Pivotal { get; } = pivotal;

        public Dictionary<string, List<LayoutItem>> Stacks { get; } = new(StringComparer.Ordinal);

        public double Width { get; private set; }

        public void Add(LayoutItem item, string band)
        {
            if (!Stacks.TryGetValue(band, out var stack))
            {
                Stacks[band] = stack = [];
            }

            stack.Add(item);
            Width = Math.Max(Width, item.Size.Width);
        }

        public double StackHeight(string band) =>
            Stacks.TryGetValue(band, out var stack)
                ? stack.Sum(item => item.Size.Height) + (StackGap * (stack.Count - 1))
                : 0;
    }
}
