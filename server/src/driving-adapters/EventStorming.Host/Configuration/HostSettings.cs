using System.Security.Cryptography;

namespace EventStorming.Host.Configuration;

public sealed class JwtSettings
{
    public string Issuer { get; init; } = "eventstorming";

    public string Audience { get; init; } = "eventstorming-web";

    /// <summary>Base64 of at least 32 random bytes. Empty outside Production means "generate one" (see <see cref="SigningKeys"/>).</summary>
    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 15;
}

public static class ConfigurationExtensions
{
    public static T Required<T>(this IConfiguration configuration, string section) =>
        configuration.GetSection(section).Get<T>()
        ?? throw new InvalidOperationException($"Configuration section '{section}' is missing.");
}

/// <summary>
/// Outside Production, a missing Jwt:SigningKey is replaced by a random key persisted under
/// Keys:Directory, so "docker compose up" works with no secrets in the repository and sessions survive
/// restarts. Production refuses to start without an explicitly configured key.
/// </summary>
public static class SigningKeys
{
    public static string Resolve(JwtSettings settings, IConfiguration configuration, IWebHostEnvironment environment, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(settings.SigningKey))
        {
            return settings.SigningKey;
        }

        if (environment.IsProduction())
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured in Production (a Base64 string of at least 32 random bytes).");
        }

        var directory = configuration["Keys:Directory"] is { Length: > 0 } configured
            ? configured
            : Path.Combine(environment.ContentRootPath, ".keys");
        var path = Path.Combine(directory, "jwt-signing.key");

        if (File.Exists(path))
        {
            return File.ReadAllText(path).Trim();
        }

        Directory.CreateDirectory(directory);
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        File.WriteAllText(path, key);
        logger.LogWarning("No Jwt:SigningKey configured; generated a development key at {Path}.", path);
        return key;
    }
}
