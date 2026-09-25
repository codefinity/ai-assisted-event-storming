using System.Diagnostics;
using EventStorming.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace EventStorming.Api.Rest.Http;

/// <summary>One field-level entry in a problem's "errors" array (RFC 9457 §3, extension member).</summary>
public sealed record FieldError(string? Pointer, string? Field, string Code, string Detail, string? Fix);

/// <summary>
/// The single place a use case's refusal becomes an RFC 9457 response. Every endpoint, internal or
/// public, goes through here, so there is exactly one error shape.
/// </summary>
public static class Problems
{
    public static ProblemHttpResult From(IUseCaseResult result, HttpContext http) => From(result.Failures, http);

    public static ProblemHttpResult From(IReadOnlyList<Failure> failures, HttpContext http)
    {
        var first = failures.Count > 0
            ? failures[0]
            : new Failure(FailureKind.Validation, "invalid", "The request was refused.");
        var type = ProblemCatalog.ForKind(first.Kind);

        var problem = Create(type, http, failures.Count == 1 && first.Field is null
            ? first.Message
            : failures.Count == 1
                ? $"'{first.Field}' needs attention: {first.Message}"
                : $"{failures.Count} fields need attention.");

        problem.Extensions["code"] = first.Code;
        if (failures.Count == 1 && first.Fix is not null)
        {
            problem.Extensions["fix"] = first.Fix;
        }

        if (failures.Any(failure => failure.Field is not null) || failures.Count > 1)
        {
            problem.Extensions["errors"] = failures
                .Select(failure => new FieldError(
                    failure.Field is null ? null : Pointer(failure.Field),
                    failure.Field,
                    failure.Code,
                    failure.Message,
                    failure.Fix))
                .ToList();
        }

        return TypedResults.Problem(problem);
    }

    public static ProblemHttpResult Of(ProblemType type, HttpContext http, string detail, string code, string? fix = null)
    {
        var problem = Create(type, http, detail);
        problem.Extensions["code"] = code;
        if (fix is not null)
        {
            problem.Extensions["fix"] = fix;
        }

        return TypedResults.Problem(problem);
    }

    public static ProblemDetails Create(ProblemType type, HttpContext http, string detail)
    {
        var problem = new ProblemDetails
        {
            Type = type.Uri,
            Title = type.Title,
            Status = type.Status,
            Detail = detail,
            Instance = http.Request.Path,
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;
        return problem;
    }

    /// <summary>"elements[3].position.x" becomes the JSON pointer "#/elements/3/position/x".</summary>
    public static string Pointer(string field) =>
        "#/" + field.Replace("[", "/", StringComparison.Ordinal).Replace("]", string.Empty, StringComparison.Ordinal).Replace('.', '/');
}
