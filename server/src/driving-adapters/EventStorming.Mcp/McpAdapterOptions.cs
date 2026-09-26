using System.Security.Claims;
using EventStorming.SharedKernel;

namespace EventStorming.Mcp;

/// <summary>What the host tells the MCP adapter. The adapter does not authenticate: the host does, before a request reaches it.</summary>
public sealed class McpAdapterOptions
{
    /// <summary>
    /// Turns the authenticated caller into the core's <see cref="Actor"/>. The host passes the same mapping
    /// the REST API uses, so both adapters see an API key the same way.
    /// </summary>
    public required Func<ClaimsPrincipal, Actor> ActorFrom { get; init; }

    /// <summary>Where people open boards, so tool results can link to what was drawn.</summary>
    public string WebAppBaseUrl { get; init; } = "http://localhost:3000";

    public string BoardUrl(Guid boardId) => $"{WebAppBaseUrl.TrimEnd('/')}/boards/{boardId}";
}
