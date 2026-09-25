using EventStorming.SharedKernel;

namespace EventStorming.Specs.Support.Fakes;

public sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan by) => UtcNow += by;
}
