namespace EventStorming.SharedKernel;

public enum ActorKind
{
    Account,
    ApiKey,
}

/// <summary>
/// Who is making a request. Every driving adapter builds one from its own authentication (a JWT, an
/// API key, later an MCP session) and passes it inside the command, so the core never reads a
/// framework's notion of "the current user". An API key carries the team it belongs to and its scopes;
/// an account carries neither, because what it may do depends on its team memberships.
/// </summary>
public sealed record Actor(ActorKind Kind, Guid Id, string DisplayName, Guid? TeamId = null, IReadOnlyList<string>? Scopes = null)
{
    public ActorRef Ref => new(Kind, Id, DisplayName);

    public bool HasScope(string scope) => Scopes is not null && Scopes.Contains(scope);

    public static Actor Account(Guid accountId, string displayName) => new(ActorKind.Account, accountId, displayName);

    public static Actor ApiKey(Guid keyId, string name, Guid teamId, IReadOnlyList<string> scopes) =>
        new(ActorKind.ApiKey, keyId, name, teamId, scopes);
}

/// <summary>The stored "who did this" stamp: an actor without its scopes.</summary>
public sealed record ActorRef(ActorKind Kind, Guid Id, string Name);
