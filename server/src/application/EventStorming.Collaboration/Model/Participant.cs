namespace EventStorming.Collaboration.Model;

/// <summary>
/// One live connection to one board. A person with two tabs open is two participants with the same
/// <see cref="AccountId"/>. Nothing here is persisted: presence ends when the connection does.
/// </summary>
public sealed record Participant(
    string ConnectionId,
    Guid BoardId,
    Guid AccountId,
    string DisplayName,
    string Color,
    DateTimeOffset JoinedAt,
    Guid? EditingElementId = null);

/// <summary>Where an element would be if the drag in progress were dropped now.</summary>
public sealed record DragMove(Guid ElementId, double X, double Y);

/// <summary>
/// Cursor and avatar colors. A separate, deliberately desaturated set from the element colors, so
/// presence never reads as element meaning. The same person always gets the same color.
/// </summary>
public static class ParticipantColors
{
    public static readonly IReadOnlyList<string> Palette =
    [
        "#4C6EF5", "#12B886", "#F76707", "#AE3EC9", "#1098AD",
        "#E64980", "#5C940D", "#D6336C", "#7048E8", "#0B7285",
    ];

    public static string For(Guid accountId)
    {
        var bytes = accountId.ToByteArray();
        var hash = 17;
        foreach (var value in bytes)
        {
            hash = unchecked((hash * 31) + value);
        }

        return Palette[(int)((uint)hash % Palette.Count)];
    }
}

public static class PresenceLimits
{
    public const int MaxDragPreviewElements = 500;
    public const double MaxCoordinate = 1_000_000;
}
