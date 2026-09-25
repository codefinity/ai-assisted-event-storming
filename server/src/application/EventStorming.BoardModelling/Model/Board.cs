using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Model;

/// <summary>The three EventStorming levels. A board's level is chosen when it is created and never changes.</summary>
public enum BoardLevel
{
    BigPicture,
    ProcessModelling,
    SoftwareDesign,
}

/// <summary>
/// A board as the catalog knows it. <see cref="Revision"/> increases on every change to its content
/// and is how a reconnecting client tells whether it missed anything.
/// </summary>
public sealed record Board(
    Guid Id,
    Guid TeamId,
    string Name,
    BoardLevel Level,
    long Revision,
    int ElementCount,
    DateTimeOffset CreatedAt,
    ActorRef CreatedBy,
    DateTimeOffset UpdatedAt,
    ActorRef UpdatedBy,
    DateTimeOffset? ArchivedAt);

public static class BoardLevels
{
    public static readonly IReadOnlyList<string> Names = ["big-picture", "process-modelling", "software-design"];

    public static bool IsValid(string? level) => level is not null && Names.Contains(level);

    public static BoardLevel Parse(string level) => level switch
    {
        "big-picture" => BoardLevel.BigPicture,
        "process-modelling" => BoardLevel.ProcessModelling,
        "software-design" => BoardLevel.SoftwareDesign,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown board level."),
    };

    public static string Name(BoardLevel level) => level switch
    {
        BoardLevel.BigPicture => "big-picture",
        BoardLevel.ProcessModelling => "process-modelling",
        _ => "software-design",
    };
}

/// <summary>
/// What an actor may do with a board, as decided by the anti-corruption layer over the Teams context:
/// the core never sees team roles or API-key scopes, only this.
/// </summary>
public enum BoardPermission
{
    None,
    View,
    Edit,
}

public sealed record BoardAccess(BoardPermission Permission, bool Archived)
{
    public static readonly BoardAccess NoAccess = new(BoardPermission.None, false);
}

public static class BoardLimits
{
    public const int MaxElements = 5_000;
    public const int MaxConnections = 10_000;
    public const int MaxElementsPerRequest = 500;
    public const double MaxCoordinate = 1_000_000;
    public const double MinSize = 16;
    public const double MaxSize = 200_000;
    public const int MaxBoardNameLength = 120;
    public const int MaxTextLength = 1_000;
    public const int MaxLabelLength = 80;
    public const int MaxKeyLength = 64;
    public const int MaxOperationIdLength = 64;
}
