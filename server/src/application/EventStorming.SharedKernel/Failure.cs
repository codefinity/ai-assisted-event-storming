namespace EventStorming.SharedKernel;

/// <summary>What kind of refusal a <see cref="Failure"/> is. Driving adapters map this to a status code.</summary>
public enum FailureKind
{
    Validation,
    NotFound,
    Forbidden,
    Conflict,
    Unauthenticated,
    LimitExceeded,
}

/// <summary>
/// Why a use case refused a request, precise enough for a caller to correct it without asking anyone:
/// <see cref="Field"/> is the camelCase path of the offending input (e.g. "elements[3].type") and
/// <see cref="Fix"/> says what to send instead. Both are optional because not every failure is about
/// one field.
/// </summary>
public sealed record Failure(FailureKind Kind, string Code, string Message, string? Field = null, string? Fix = null);

/// <summary>The failures every context raises the same way, so their wording stays consistent.</summary>
public static class Failures
{
    public static Failure NotFound(string what, string? fix = null) =>
        new(FailureKind.NotFound, "not-found", $"{what} was not found.", Fix: fix);

    public static Failure Forbidden(string message, string? fix = null) =>
        new(FailureKind.Forbidden, "forbidden", message, Fix: fix);

    public static Failure Invalid(string field, string code, string message, string? fix = null) =>
        new(FailureKind.Validation, code, message, field, fix);

    public static Failure Conflict(string code, string message, string? field = null, string? fix = null) =>
        new(FailureKind.Conflict, code, message, field, fix);
}
