namespace EventStorming.Identity.Shared;

public static class SessionPolicy
{
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// Two tabs refreshing at the same moment both present the same token; the loser of that race is
    /// told to retry rather than having its whole session revoked as a stolen-token replay.
    /// </summary>
    public static readonly TimeSpan RotationGracePeriod = TimeSpan.FromSeconds(20);
}
