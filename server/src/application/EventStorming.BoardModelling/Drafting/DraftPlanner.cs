using EventStorming.BoardModelling.Layout;
using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Drafting;

/// <summary>
/// An element as a caller describes it before it exists. <see cref="Path"/> is where the draft sat in
/// the caller's request (e.g. "elements[3]"), so every failure can name the exact field.
/// References (<see cref="Swimlane"/>, <see cref="Boundary"/>, <see cref="Anchor"/>) are the key of
/// another draft in the same request, or the id of an element already on the board.
/// </summary>
public sealed record ElementDraft(
    string Path,
    Guid? Id,
    string? Key,
    string? Type,
    string? Text,
    Position? Position,
    Size? Size,
    string? Swimlane,
    string? Boundary,
    string? Anchor,
    bool Pivotal,
    string? Color);

/// <summary><see cref="From"/> and <see cref="To"/> are draft keys or existing element ids.</summary>
public sealed record ConnectionDraft(string Path, Guid? Id, string? From, string? To, string? Label);

/// <param name="Existing">Elements already on the board that drafts refer to, by id.</param>
/// <param name="Origin">Where auto-layout starts placing drafts that have no position.</param>
public sealed record DraftContext(Guid BoardId, ActorRef Actor, DateTimeOffset Now, Position Origin, IReadOnlyDictionary<Guid, Element> Existing);

/// <param name="Grown">Swimlanes and boundaries already on the board, resized to hold the new elements that name them.</param>
public sealed record PlannedContent(IReadOnlyList<Element> Elements, IReadOnlyList<Connection> Connections, IReadOnlyDictionary<string, Guid> KeyedIds, IReadOnlyList<Element> Grown);

public sealed record DraftPlan(PlannedContent? Content, IReadOnlyList<Failure> Failures);

/// <summary>
/// Turns drafts into elements and connections ready to store: checks every draft against the type
/// registry, resolves every reference, and auto-lays-out whatever has no position. Pure: it reads
/// only its arguments, and it reports all problems at once so a caller can fix them in one go.
/// </summary>
public static class DraftPlanner
{
    public static DraftPlan Plan(
        IReadOnlyList<ElementDraft> drafts,
        IReadOnlyList<ConnectionDraft> connectionDrafts,
        IElementTypeRegistry registry,
        DraftContext context)
    {
        var failures = new List<Failure>();
        var types = new ElementType?[drafts.Count];
        var keyIndex = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var index = 0; index < drafts.Count; index++)
        {
            var draft = drafts[index];
            if (draft.Key is { Length: > 0 } key)
            {
                keyIndex.TryAdd(key, index);
            }

            var type = draft.Type is null ? null : registry.Find(draft.Type);
            types[index] = type;
            if (type is null)
            {
                if (draft.Type is not null)
                {
                    failures.Add(BoardRules.UnknownType($"{draft.Path}.type", draft.Type, registry));
                }

                continue;
            }

            if ((draft.Text ?? string.Empty).Trim().Length > type.MaxTextLength)
            {
                failures.Add(BoardRules.TextTooLong($"{draft.Path}.text", type));
            }

            if (draft.Pivotal && !type.CanBePivotal)
            {
                failures.Add(BoardRules.NotPivotal($"{draft.Path}.pivotal", type));
            }
        }

        LayoutRole? RoleOf(string reference)
        {
            if (keyIndex.TryGetValue(reference, out var index))
            {
                return types[index]?.LayoutRole;
            }

            return Guid.TryParse(reference, out var id) && context.Existing.TryGetValue(id, out var element)
                ? registry.Find(element.Type)?.LayoutRole ?? LayoutRole.Item
                : null;
        }

        void CheckReference(string? reference, string field, LayoutRole expected, string expectedName, string? self)
        {
            if (reference is null)
            {
                return;
            }

            if (reference == self)
            {
                failures.Add(Failures.Invalid(field, "self-reference", "An element cannot refer to itself.", $"Remove '{field}' or point it at another element."));
                return;
            }

            var role = RoleOf(reference);
            if (role is null)
            {
                failures.Add(Failures.Invalid(field, "unknown-reference", $"No element in this request has key '{reference}', and no element on the board has that id.",
                    SuggestKey(reference, keyIndex.Keys) is { } suggestion
                        ? $"Did you mean '{suggestion}'? Use the key of an element defined in this request, or the id of an existing element."
                        : "Use the key of an element defined in this request, or the id of an existing element."));
            }
            else if (role != expected)
            {
                failures.Add(Failures.Invalid(field, $"not-a-{expectedName}", $"'{reference}' is not a {expectedName}.",
                    $"Refer to an element whose type has the {expectedName} role (see GET /api/v1/element-types)."));
            }
        }

        for (var index = 0; index < drafts.Count; index++)
        {
            var draft = drafts[index];
            CheckReference(draft.Swimlane, $"{draft.Path}.swimlane", LayoutRole.Lane, "swimlane", draft.Key);
            CheckReference(draft.Boundary, $"{draft.Path}.boundary", LayoutRole.Boundary, "boundary", draft.Key);
            CheckReference(draft.Anchor, $"{draft.Path}.anchor", LayoutRole.Item, "sticky", draft.Key);
        }

        var ids = drafts.Select(draft => draft.Id ?? Guid.CreateVersion7()).ToArray();
        var requestIds = ids.Where((_, index) => drafts[index].Id is not null).ToHashSet();

        // A connection may name its ends by a key in this request, by the id given to an element in
        // this request (how the web app pastes a group), or by the id of an element already on the board.
        Guid? Resolve(string? reference, string field)
        {
            if (string.IsNullOrEmpty(reference))
            {
                return null;
            }

            if (keyIndex.TryGetValue(reference, out var index))
            {
                return ids[index];
            }

            if (Guid.TryParse(reference, out var id) && (requestIds.Contains(id) || context.Existing.ContainsKey(id)))
            {
                return id;
            }

            failures.Add(Failures.Invalid(field, "unknown-reference", $"No element in this request has key or id '{reference}', and no element on the board has that id.",
                SuggestKey(reference, keyIndex.Keys) is { } suggestion
                    ? $"Did you mean '{suggestion}'? Use a key defined in 'elements', or an existing element id."
                    : "Use a key defined in 'elements', or an existing element id."));
            return null;
        }

        var resolvedConnections = new List<(ConnectionDraft Draft, Guid From, Guid To)>();
        var joined = new HashSet<(Guid, Guid)>();
        foreach (var draft in connectionDrafts)
        {
            var from = Resolve(draft.From, $"{draft.Path}.from");
            var to = Resolve(draft.To, $"{draft.Path}.to");
            if (from is null || to is null)
            {
                continue;
            }

            if (from == to)
            {
                failures.Add(Failures.Invalid($"{draft.Path}.to", "self-connection", "A connection must join two different elements.", "Point 'to' at a different element than 'from'."));
                continue;
            }

            // Two elements are joined at most once in each direction; a repeat is simply dropped.
            if (joined.Add((from.Value, to.Value)))
            {
                resolvedConnections.Add((draft, from.Value, to.Value));
            }
        }

        if (failures.Count > 0 || types.Any(type => type is null))
        {
            return new DraftPlan(null, failures);
        }

        var layoutKeys = drafts.Select((draft, index) => draft.Key is { Length: > 0 } key ? key : $"#{index}").ToArray();
        var existing = context.Existing.Values
            .ToDictionary(
                element => element.Id.ToString(),
                element => new PlacedElement(element.Id.ToString(), registry.Find(element.Type)?.LayoutRole ?? LayoutRole.Item, element.X, element.Y, element.Width, element.Height),
                StringComparer.OrdinalIgnoreCase);

        var layoutItems = drafts
            .Select((draft, index) => new LayoutItem(
                layoutKeys[index],
                types[index]!.LayoutRole,
                draft.Size ?? types[index]!.DefaultSize,
                draft.Position,
                NormalizeReference(draft.Swimlane, keyIndex),
                NormalizeReference(draft.Boundary, keyIndex),
                NormalizeReference(draft.Anchor, keyIndex),
                draft.Pivotal))
            .ToList();

        var placements = TimelineLayout.Arrange(layoutItems, context.Origin, existing);
        var grown = TimelineLayout.Enclose(layoutItems, placements, existing)
            .Select(pair => context.Existing[Guid.Parse(pair.Key)] with
            {
                X = pair.Value.Position.X,
                Y = pair.Value.Position.Y,
                Width = pair.Value.Size.Width,
                Height = pair.Value.Size.Height,
                UpdatedAt = context.Now,
                UpdatedBy = context.Actor,
            })
            .ToList();

        var elements = drafts
            .Select((draft, index) =>
            {
                var placement = placements[layoutKeys[index]];
                return new Element(
                    ids[index],
                    context.BoardId,
                    types[index]!.Id,
                    (draft.Text ?? string.Empty).Trim(),
                    placement.Position.X,
                    placement.Position.Y,
                    placement.Size.Width,
                    placement.Size.Height,
                    draft.Pivotal,
                    string.IsNullOrEmpty(draft.Color) ? null : draft.Color,
                    Version: 1,
                    context.Now,
                    context.Actor,
                    context.Now,
                    context.Actor);
            })
            .ToList();

        var connections = resolvedConnections
            .Select(resolved => new Connection(
                resolved.Draft.Id ?? Guid.CreateVersion7(),
                context.BoardId,
                resolved.From,
                resolved.To,
                string.IsNullOrWhiteSpace(resolved.Draft.Label) ? null : resolved.Draft.Label.Trim(),
                Version: 1,
                context.Now,
                context.Actor))
            .ToList();

        var keyed = keyIndex.ToDictionary(pair => pair.Key, pair => ids[pair.Value], StringComparer.Ordinal);
        return new DraftPlan(new PlannedContent(elements, connections, keyed, grown), []);
    }

    /// <summary>Existing ids are passed to the layout in their canonical form, so "ABC…" and "abc…" match.</summary>
    private static string? NormalizeReference(string? reference, IReadOnlyDictionary<string, int> keyIndex) =>
        reference is null || keyIndex.ContainsKey(reference) ? reference
        : Guid.TryParse(reference, out var id) ? id.ToString()
        : reference;

    /// <summary>The closest key by edit distance, so a typo like "plced" can be answered with "placed".</summary>
    private static string? SuggestKey(string reference, IEnumerable<string> keys) =>
        keys
            .Select(key => (Key: key, Distance: Distance(reference, key)))
            .Where(candidate => candidate.Distance <= Math.Max(2, reference.Length / 3))
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Key)
            .FirstOrDefault();

    private static int Distance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
