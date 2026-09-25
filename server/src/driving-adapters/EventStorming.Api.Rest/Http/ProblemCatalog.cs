using EventStorming.SharedKernel;

namespace EventStorming.Api.Rest.Http;

/// <summary>One kind of problem the API can report, and what a caller should do about it.</summary>
public sealed record ProblemType(string Slug, int Status, string Title, string Description, string WhatToDo)
{
    public string Uri => ProblemCatalog.TypeBase + Slug;
}

/// <summary>
/// Every problem "type" the API returns. Each is dereferenceable at /api/v1/problems/{slug}, so a
/// client - human or LLM - that has never seen one before can look it up. The specific reason is
/// always in the problem's "code" (and each field error's "code"); the type only says which family
/// of mistake it is.
/// </summary>
public static class ProblemCatalog
{
    public const string TypeBase = "/api/v1/problems/";

    public static readonly ProblemType MalformedRequest = new(
        "malformed-request", 400, "The request could not be read.",
        "The body is not valid JSON, or a value has the wrong JSON type (for example a string where a number is expected).",
        "Send a JSON body matching the documented schema. Check quotes, commas and number fields.");

    public static readonly ProblemType Unauthenticated = new(
        "unauthenticated", 401, "Authentication is required.",
        "No credentials were sent, or the access token, API key or session is missing, malformed, expired or revoked.",
        "Send 'Authorization: Bearer <token-or-api-key>'. For the web app, sign in again; for the public API, check the key has not been revoked or expired.");

    public static readonly ProblemType Forbidden = new(
        "forbidden", 403, "You are not allowed to do this.",
        "The caller is authenticated but its role or API-key scope does not permit this operation.",
        "Ask a team Owner for the Editor role, or use an API key with the 'write' scope.");

    public static readonly ProblemType NotFound = new(
        "not-found", 404, "The resource does not exist.",
        "Nothing exists at this id, or it belongs to a team the caller cannot see (the two are deliberately indistinguishable).",
        "Check the id. List boards with GET /api/v1/boards to find valid ids.");

    public static readonly ProblemType Conflict = new(
        "conflict", 409, "The request conflicts with the current state.",
        "Something changed since the caller last read it, or the request would duplicate something that must be unique. The 'code' says which.",
        "Re-read the resource and retry. For 'version-conflict', resend with the current version; for 'idempotency-in-progress', wait and retry.");

    public static readonly ProblemType PayloadTooLarge = new(
        "payload-too-large", 413, "The request body is too large.",
        "Board documents are limited to 2 MB, and bulk requests to 500 elements.",
        "Split the request into smaller batches.");

    public static readonly ProblemType ValidationFailed = new(
        "validation-failed", 422, "The request has invalid fields.",
        "The request was readable but one or more fields break a rule. Every offending field is listed in 'errors', each with a JSON pointer, a code, and a fix.",
        "Apply each 'fix' to the field named by 'pointer' and resend. All problems are reported at once.");

    public static readonly ProblemType LimitExceeded = new(
        "limit-exceeded", 422, "A limit would be exceeded.",
        "The request is valid but would take a board past a fixed limit (5,000 elements or 10,000 connections).",
        "Remove elements you no longer need, or split the model across boards.");

    public static readonly ProblemType RateLimited = new(
        "rate-limited", 429, "Too many requests.",
        "The API key (or, for sign-in, the client address) has used up its request allowance for the current window.",
        "Wait for the number of seconds in the Retry-After header, then retry. Batch work with the bulk endpoints.");

    public static readonly ProblemType InternalError = new(
        "internal-error", 500, "Something went wrong on our side.",
        "An unexpected error occurred. Nothing about the request needs to change.",
        "Retry later. If it keeps happening, report the 'traceId'.");

    public static IReadOnlyList<ProblemType> All { get; } =
    [
        MalformedRequest, Unauthenticated, Forbidden, NotFound, Conflict, PayloadTooLarge,
        ValidationFailed, LimitExceeded, RateLimited, InternalError,
    ];

    public static ProblemType ForKind(FailureKind kind) => kind switch
    {
        FailureKind.Validation => ValidationFailed,
        FailureKind.NotFound => NotFound,
        FailureKind.Forbidden => Forbidden,
        FailureKind.Conflict => Conflict,
        FailureKind.Unauthenticated => Unauthenticated,
        FailureKind.LimitExceeded => LimitExceeded,
        _ => InternalError,
    };

    public static ProblemType? ForStatus(int status) => All.FirstOrDefault(type => type.Status == status);
}
