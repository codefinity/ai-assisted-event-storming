namespace EventStorming.PublicIntegration.Model;

/// <summary>
/// A team's credential for the public API. Only the hash of its secret is kept; the full key is shown
/// once, when it is created. A revoked or expired key stops working immediately.
/// </summary>
public sealed record ApiKey(
    Guid Id,
    Guid TeamId,
    string Name,
    string DisplayPrefix,
    string SecretHash,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset? ExpiresAt = null,
    DateTimeOffset? LastUsedAt = null,
    DateTimeOffset? RevokedAt = null);

public sealed record ApiKeySummary(
    Guid Id,
    string Name,
    string DisplayPrefix,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt)
{
    public static ApiKeySummary From(ApiKey key) =>
        new(key.Id, key.Name, key.DisplayPrefix, key.Scopes, key.CreatedAt, key.ExpiresAt, key.LastUsedAt, key.RevokedAt);
}

/// <summary>"read" allows every GET; "write" allows every change and implies "read".</summary>
public static class ApiScopes
{
    public const string Read = "read";
    public const string Write = "write";

    public static readonly IReadOnlyList<string> All = [Read, Write];

    public static IReadOnlyList<string> Normalize(IEnumerable<string> scopes)
    {
        var set = scopes.Select(scope => scope.Trim().ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        if (set.Contains(Write))
        {
            set.Add(Read);
        }

        return All.Where(set.Contains).ToList();
    }
}

/// <summary>
/// The presented key is "es_&lt;key id as 32 hex digits&gt;_&lt;secret&gt;". The id makes lookup a
/// single indexed read; only the secret is secret.
/// </summary>
public static class ApiKeyFormat
{
    public const string Prefix = "es_";

    public static string Compose(Guid keyId, string secret) => $"{Prefix}{keyId:N}_{secret}";

    public static string DisplayPrefix(Guid keyId) => $"{Prefix}{keyId.ToString("N")[..8]}…";

    public static bool TryParse(string? presented, out Guid keyId, out string secret)
    {
        keyId = Guid.Empty;
        secret = string.Empty;
        if (presented is null || !presented.StartsWith(Prefix, StringComparison.Ordinal) || presented.Length < Prefix.Length + 34)
        {
            return false;
        }

        var idPart = presented.Substring(Prefix.Length, 32);
        if (presented[Prefix.Length + 32] != '_' || !Guid.TryParseExact(idPart, "N", out keyId))
        {
            return false;
        }

        secret = presented[(Prefix.Length + 33)..];
        return secret.Length > 0;
    }
}
