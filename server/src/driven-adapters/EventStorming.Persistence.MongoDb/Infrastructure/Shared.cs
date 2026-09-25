using System.Globalization;
using System.Text;
using EventStorming.SharedKernel;

namespace EventStorming.Persistence.MongoDb.Infrastructure;

internal sealed class MongoActorRef
{
    public ActorKind Kind { get; init; }

    public Guid Id { get; init; }

    public required string Name { get; init; }

    public ActorRef ToModel() => new(Kind, Id, Name);

    public static MongoActorRef From(ActorRef actor) => new() { Kind = actor.Kind, Id = actor.Id, Name = actor.Name };
}

/// <summary>
/// Opaque keyset cursors. A cursor is the sort key of the last item on a page, Base64url-encoded, so it
/// survives being passed around as a query-string value and cannot be mistaken for anything else.
/// </summary>
internal static class Cursors
{
    public static string Encode(params string[] parts) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join('|', parts))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string[]? Decode(string cursor, int expectedParts)
    {
        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split('|');
            return parts.Length == expectedParts ? parts : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static string Ticks(DateTimeOffset value) => value.UtcTicks.ToString(CultureInfo.InvariantCulture);

    public static DateTimeOffset? FromTicks(string value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) && ticks >= 0 && ticks <= DateTimeOffset.MaxValue.UtcTicks
            ? new DateTimeOffset(ticks, TimeSpan.Zero)
            : null;
}
