namespace EventStorming.SharedKernel;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
