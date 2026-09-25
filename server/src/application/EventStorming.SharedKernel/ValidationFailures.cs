using System.Text;
using FluentValidation;
using FluentValidation.Results;

namespace EventStorming.SharedKernel;

/// <summary>
/// Turns FluentValidation's output into <see cref="Failure"/>s. Property paths are rewritten to the
/// camelCase JSON path a caller actually sent ("Elements[3].Type" becomes "elements[3].type"), the
/// error code is whatever the rule set with WithErrorCode, and a rule's <see cref="WithFix{T,TProperty}"/>
/// becomes the failure's Fix.
/// </summary>
public static class ValidationFailures
{
    public static IReadOnlyList<Failure> ToFailures(this ValidationResult result) =>
        result.Errors
            .Select(error => new Failure(
                FailureKind.Validation,
                string.IsNullOrEmpty(error.ErrorCode) || error.ErrorCode.EndsWith("Validator", StringComparison.Ordinal)
                    ? "invalid"
                    : error.ErrorCode,
                error.ErrorMessage,
                JsonPath(error.PropertyName),
                error.CustomState as string))
            .ToList();

    public static IRuleBuilderOptions<T, TProperty> WithFix<T, TProperty>(this IRuleBuilderOptions<T, TProperty> rule, string fix) =>
        rule.WithState(_ => fix);

    public static string JsonPath(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
        {
            return propertyPath;
        }

        var builder = new StringBuilder(propertyPath.Length);
        var startOfSegment = true;
        foreach (var character in propertyPath)
        {
            builder.Append(startOfSegment ? char.ToLowerInvariant(character) : character);
            startOfSegment = character == '.';
        }

        return builder.ToString();
    }
}
