namespace EventStorming.SharedKernel;

/// <summary>
/// One page of a list. <see cref="NextCursor"/> is opaque to the core - a persistence adapter mints it
/// and reads it back - and null means there is nothing after this page.
/// </summary>
public sealed record Page<T>(IReadOnlyList<T> Items, string? NextCursor);

public static class Paging
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;
}
