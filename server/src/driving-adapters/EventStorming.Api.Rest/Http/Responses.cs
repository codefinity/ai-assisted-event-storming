using System.Text.Json.Serialization;
using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Slices.ListElementTypes;
using EventStorming.SharedKernel;

namespace EventStorming.Api.Rest.Http;

public sealed record ActorResponse(string Kind, Guid Id, string Name)
{
    public static ActorResponse From(ActorRef actor) => new(actor.Kind == ActorKind.ApiKey ? "api-key" : "account", actor.Id, actor.Name);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PositionDto(double X, double Y)
{
    public Position ToModel() => new(X, Y);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SizeDto(double Width, double Height)
{
    public Size ToModel() => new(Width, Height);
}

/// <param name="Level">"big-picture", "process-modelling" or "software-design".</param>
public sealed record BoardResponse(
    Guid Id,
    Guid TeamId,
    string Name,
    string Level,
    long Revision,
    int ElementCount,
    DateTimeOffset CreatedAt,
    ActorResponse CreatedBy,
    DateTimeOffset UpdatedAt,
    ActorResponse UpdatedBy,
    DateTimeOffset? ArchivedAt)
{
    public static BoardResponse From(Board board) => new(
        board.Id, board.TeamId, board.Name, BoardLevels.Name(board.Level), board.Revision, board.ElementCount,
        board.CreatedAt, ActorResponse.From(board.CreatedBy), board.UpdatedAt, ActorResponse.From(board.UpdatedBy), board.ArchivedAt);
}

public sealed record ElementTypeResponse(
    string Id,
    string Name,
    string Category,
    string Renderer,
    string Color,
    string TextColor,
    string Icon,
    SizeDto DefaultSize,
    IReadOnlyList<string> Levels,
    string? Shortcut,
    int MaxTextLength,
    bool CanBePivotal,
    string LayoutRole,
    string Description,
    string WhenToUse,
    string? WritingRule,
    IReadOnlyList<string> Examples)
{
    public static ElementTypeResponse From(ElementType type) => new(
        type.Id,
        type.Name,
        type.Category == ElementCategory.Sticky ? "sticky" : "structure",
        type.Renderer,
        type.Color,
        type.TextColor,
        type.Icon,
        new SizeDto(type.DefaultSize.Width, type.DefaultSize.Height),
        type.Levels.Select(BoardLevels.Name).ToList(),
        type.Shortcut,
        type.MaxTextLength,
        type.CanBePivotal,
        type.LayoutRole.ToString().ToLowerInvariant(),
        type.Description,
        type.WhenToUse,
        type.WritingRule,
        type.Examples);
}

/// <param name="Palette">The type ids offered in the palette at this level, in order. Every other type stays available.</param>
public sealed record LevelResponse(string Id, string Name, string Description, IReadOnlyList<string> Palette);

public sealed record NotationResponse(IReadOnlyList<ElementTypeResponse> Types, IReadOnlyList<LevelResponse> Levels)
{
    public static NotationResponse From(Notation notation) => new(
        notation.Types.Select(ElementTypeResponse.From).ToList(),
        notation.Levels
            .Select(level => new LevelResponse(
                BoardLevels.Name(level.Level),
                level.Name,
                level.Description,
                notation.Types.Where(type => type.Levels.Contains(level.Level)).Select(type => type.Id).ToList()))
            .ToList());
}

public sealed record PageResponse<T>(IReadOnlyList<T> Items, string? NextCursor);
