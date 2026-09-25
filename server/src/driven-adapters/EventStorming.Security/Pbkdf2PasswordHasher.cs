using System.Globalization;
using System.Security.Cryptography;
using EventStorming.Identity.Shared;

namespace EventStorming.Security;

/// <summary>
/// PBKDF2-SHA256 in a self-describing format: "pbkdf2-sha256$&lt;iterations&gt;$&lt;salt&gt;$&lt;hash&gt;".
/// Verification reads the iteration count from the stored value, so raising the cost only affects
/// passwords hashed afterwards.
/// </summary>
internal sealed class Pbkdf2PasswordHasher(SecurityOptions options) : IPasswordHasher
{
    private const string Scheme = "pbkdf2-sha256";
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, options.PasswordHashIterations, HashAlgorithmName.SHA256, HashBytes);
        return string.Join('$', Scheme, options.PasswordHashIterations.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('$');
        if (parts.Length != 4
            || parts[0] != Scheme
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
