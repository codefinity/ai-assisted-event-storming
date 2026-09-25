using System.Text.RegularExpressions;
using EventStorming.BoardModelling.Model;
using EventStorming.SharedKernel;

namespace EventStorming.BoardModelling.Shared;

/// <summary>
/// Checks several slices apply the same way. Pure functions over plain data - there is no state here
/// and nothing to inject.
/// </summary>
public static partial class BoardRules
{
    public const string EditFix = "Ask a team Owner for the Editor role, or use an API key with the 'write' scope.";

    public static Failure? RequireView(BoardAccess access) =>
        access.Permission == BoardPermission.None ? Failures.NotFound("The board", "Check the board id; list boards with GET /api/v1/boards.") : null;

    public static Failure? RequireEdit(BoardAccess access) =>
        access.Permission switch
        {
            BoardPermission.None => Failures.NotFound("The board", "Check the board id; list boards with GET /api/v1/boards."),
            BoardPermission.View => Failures.Forbidden("You can view this board but not change it.", EditFix),
            _ when access.Archived => Failures.Conflict("board-archived", "The board is archived, so it cannot be changed.", fix: "Restore the board first."),
            _ => null,
        };

    public static Failure? RequireTeamEdit(BoardPermission permission) =>
        permission switch
        {
            BoardPermission.None => Failures.NotFound("The team"),
            BoardPermission.View => Failures.Forbidden("You can view this team's boards but not create or change them.", EditFix),
            _ => null,
        };

    public static bool IsCoordinate(double value) => double.IsFinite(value) && Math.Abs(value) <= BoardLimits.MaxCoordinate;

    public static bool IsExtent(double value) => double.IsFinite(value) && value >= BoardLimits.MinSize && value <= BoardLimits.MaxSize;

    public static bool IsColor(string? color) => color is null || color.Length == 0 || HexColor().IsMatch(color);

    public static string TypeList(IElementTypeRegistry registry) => string.Join(", ", registry.Types.Select(type => type.Id));

    public static Failure UnknownType(string field, string type, IElementTypeRegistry registry) =>
        Failures.Invalid(field, "unknown-element-type", $"'{type}' is not an element type.",
            $"Use one of: {TypeList(registry)}. See GET /api/v1/element-types.");

    public static Failure TextTooLong(string field, ElementType type) =>
        Failures.Invalid(field, "too-long", $"A {type.Name} holds at most {type.MaxTextLength} characters.",
            "Shorten the text; a sticky should say one thing.");

    public static Failure NotPivotal(string field, ElementType type) =>
        Failures.Invalid(field, "not-pivotal-type", $"A {type.Name} cannot be pivotal; only Domain Events can.",
            "Set 'pivotal' to false, or use type 'domain-event'.");

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
