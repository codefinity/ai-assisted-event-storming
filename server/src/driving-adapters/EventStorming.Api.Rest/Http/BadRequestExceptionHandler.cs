using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace EventStorming.Api.Rest.Http;

/// <summary>
/// A body the framework cannot read (bad JSON, a string where a number belongs, an unknown field in
/// a Board Document, a body over the size limit) becomes a problem that says exactly where it broke,
/// so the caller can fix it from the response alone.
/// </summary>
internal sealed partial class BadRequestExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        http.Response.StatusCode = badRequest.StatusCode;
        if (badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            await Problems.Of(ProblemCatalog.PayloadTooLarge, http, "The request body is larger than this endpoint accepts.", "payload-too-large",
                ProblemCatalog.PayloadTooLarge.WhatToDo).ExecuteAsync(http);
            return true;
        }

        var json = badRequest.InnerException as JsonException;
        var detail = json is null
            ? badRequest.Message
            : $"The JSON body could not be read at '{json.Path ?? "$"}': {FirstSentence(json.Message)}";
        var problem = Problems.Create(ProblemCatalog.MalformedRequest, http, detail);
        problem.Extensions["code"] = "malformed-request";
        problem.Extensions["fix"] = ProblemCatalog.MalformedRequest.WhatToDo;
        if (json?.Path is { Length: > 1 } path)
        {
            problem.Extensions["pointer"] = "#" + path.TrimStart('$').Replace(".", "/", StringComparison.Ordinal).Replace("[", "/", StringComparison.Ordinal).Replace("]", string.Empty, StringComparison.Ordinal);
        }

        await TypedResults.Problem(problem).ExecuteAsync(http);
        return true;
    }

    private static string FirstSentence(string message)
    {
        // "The JSON property 'positon' could not be mapped to any .NET member contained in type '...'" names
        // an internal type; say what matters to the caller instead.
        if (UnmappedProperty().Match(message) is { Success: true } unmapped)
        {
            return $"'{unmapped.Groups[1].Value}' is not a field here. Check its spelling against the schema at /api/v1/schemas/board-document.json.";
        }

        var end = message.IndexOf(" Path:", StringComparison.Ordinal);
        return end > 0 ? message[..end] : message;
    }

    [GeneratedRegex("The JSON property '(.+?)' could not be mapped")]
    private static partial Regex UnmappedProperty();
}
