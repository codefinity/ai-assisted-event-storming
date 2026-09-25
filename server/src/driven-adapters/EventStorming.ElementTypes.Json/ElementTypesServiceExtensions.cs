using EventStorming.BoardModelling.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace EventStorming.ElementTypes.Json;

public static class ElementTypesServiceExtensions
{
    /// <param name="registryPath">A replacement element-types.json; null or empty uses the built-in one.</param>
    public static IServiceCollection AddEventStormingElementTypes(this IServiceCollection services, string? registryPath = null)
    {
        var json = string.IsNullOrWhiteSpace(registryPath)
            ? JsonElementTypeRegistry.EmbeddedJson()
            : File.ReadAllText(registryPath);

        // Built eagerly, so an invalid registry fails the host at startup with a clear message.
        services.AddSingleton<IElementTypeRegistry>(new JsonElementTypeRegistry(json));
        return services;
    }
}
