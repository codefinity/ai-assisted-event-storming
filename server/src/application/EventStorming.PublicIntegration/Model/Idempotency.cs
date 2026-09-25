namespace EventStorming.PublicIntegration.Model;

/// <summary>The response a write produced, kept so a retry with the same Idempotency-Key gets exactly the same answer.</summary>
public sealed record StoredResponse(int Status, string ContentType, string Body, string? Location);

/// <summary>
/// One Idempotency-Key as used by one API key. <see cref="RequestHash"/> fingerprints the request
/// (method, path and body), so reusing a key for a different request is detected rather than replayed.
/// </summary>
public sealed record IdempotencyRecord(
    string Id,
    string RequestHash,
    bool Completed,
    StoredResponse? Response,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public static class IdempotencyPolicy
{
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    /// <summary>A reservation this old that never completed belongs to a request that died; it may be taken over.</summary>
    public static readonly TimeSpan AbandonedAfter = TimeSpan.FromMinutes(2);

    public const int MaxKeyLength = 255;

    public static string RecordId(Guid apiKeyId, string key) => $"{apiKeyId:N}:{key}";
}
