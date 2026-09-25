using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace EventStorming.Persistence.MongoDb.Infrastructure;

/// <summary>
/// Process-wide BSON settings: camelCase field names, enums as strings, UUIDs in the standard binary
/// subtype, and DateTimeOffset stored as a plain UTC date so it can be indexed, sorted and TTL-expired.
/// </summary>
internal static class MongoConventions
{
    private static readonly Lock Gate = new();
    private static bool registered;

    public static void Register()
    {
        lock (Gate)
        {
            if (registered)
            {
                return;
            }

            ConventionRegistry.Register(
                "EventStorming",
                new ConventionPack
                {
                    new CamelCaseElementNameConvention(),
                    new IgnoreExtraElementsConvention(true),
                    new EnumRepresentationConvention(BsonType.String),
                },
                type => type.Namespace?.StartsWith("EventStorming.Persistence.MongoDb", StringComparison.Ordinal) == true);

            BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            BsonSerializer.TryRegisterSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));
            registered = true;
        }
    }
}
