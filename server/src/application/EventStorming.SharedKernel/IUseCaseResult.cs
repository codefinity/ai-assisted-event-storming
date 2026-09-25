namespace EventStorming.SharedKernel;

/// <summary>
/// Implemented by every use case's {Name}Result, so a driving adapter can turn any refusal into one
/// error shape without knowing which use case it called.
/// </summary>
public interface IUseCaseResult
{
    bool Success { get; }

    IReadOnlyList<Failure> Failures { get; }
}
