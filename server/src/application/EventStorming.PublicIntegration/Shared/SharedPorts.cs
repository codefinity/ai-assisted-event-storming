namespace EventStorming.PublicIntegration.Shared;

/// <summary>API-key secrets are random, shown once, and stored only as a hash.</summary>
public interface IApiKeySecretGenerator
{
    string NewSecret();

    string HashOf(string secret);

    /// <summary>Constant-time comparison of a presented secret with a stored hash.</summary>
    bool Matches(string secret, string secretHash);
}

/// <summary>Anti-corruption layer over the Teams context: API keys are managed by a team's Owners.</summary>
public interface ITeamOwnership
{
    Task<bool> IsOwner(Guid teamId, Guid accountId, CancellationToken cancellationToken);
}
