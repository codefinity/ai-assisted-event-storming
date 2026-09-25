using EventStorming.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace EventStorming.SystemClock;

public static class SystemClockServiceExtensions
{
    public static IServiceCollection AddEventStormingSystemClock(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
