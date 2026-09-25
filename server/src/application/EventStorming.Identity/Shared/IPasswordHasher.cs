namespace EventStorming.Identity.Shared;

/// <summary>
/// Hashes are self-describing strings (algorithm, cost, salt and digest in one value), so the adapter
/// can raise the cost later without a schema change.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
