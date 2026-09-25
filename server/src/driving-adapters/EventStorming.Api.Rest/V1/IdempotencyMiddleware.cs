using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using EventStorming.Api.Rest.Http;
using EventStorming.PublicIntegration.Model;
using EventStorming.PublicIntegration.Slices.RecordIdempotentResponse;
using EventStorming.PublicIntegration.Slices.ReserveIdempotencyKey;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EventStorming.Api.Rest.V1;

/// <summary>
/// Makes public-API writes safe to retry. A write sent with an Idempotency-Key header runs once; a
/// retry with the same key and the same request gets the stored response back (marked
/// "Idempotent-Replayed: true") instead of running again. The bookkeeping - reservations, replays,
/// reuse detection - is the Public Integration context's; this middleware only carries bytes to and
/// from it.
/// </summary>
internal sealed class IdempotencyMiddleware(RequestDelegate next)
{
    public const string Header = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";

    public async Task InvokeAsync(HttpContext context)
    {
        var key = context.Request.Headers[Header].ToString();
        var apiKeyId = context.User.FindFirstValue(Actors.ApiKeyIdClaim);
        if (string.IsNullOrEmpty(key)
            || apiKeyId is null
            || HttpMethods.IsGet(context.Request.Method)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        var keyId = Guid.Parse(apiKeyId);
        var reserve = context.RequestServices.GetRequiredService<IReserveIdempotencyKeyCommandHandler>();
        var reservation = await reserve.Handle(new ReserveIdempotencyKeyCommand(keyId, key, await Fingerprint(context.Request)), context.RequestAborted);
        if (!reservation.Success)
        {
            await Problems.From(reservation, context).ExecuteAsync(context);
            return;
        }

        if (reservation.Reservation!.Replay is { } replay)
        {
            context.Response.StatusCode = replay.Status;
            context.Response.ContentType = replay.ContentType;
            context.Response.Headers[ReplayedHeader] = "true";
            if (replay.Location is not null)
            {
                context.Response.Headers.Location = replay.Location;
            }

            await context.Response.WriteAsync(replay.Body, context.RequestAborted);
            return;
        }

        var original = context.Response.Body;
        using var captured = new MemoryStream();
        context.Response.Body = captured;
        StoredResponse? stored = null;
        try
        {
            await next(context);

            captured.Position = 0;
            var body = await new StreamReader(captured, Encoding.UTF8).ReadToEndAsync(context.RequestAborted);
            // A server error is worth retrying for real, so its key is released rather than remembered.
            stored = context.Response.StatusCode >= 500
                ? null
                : new StoredResponse(context.Response.StatusCode, context.Response.ContentType ?? "application/json", body, context.Response.Headers.Location.ToString() is { Length: > 0 } location ? location : null);

            captured.Position = 0;
            await captured.CopyToAsync(original, context.RequestAborted);
        }
        finally
        {
            context.Response.Body = original;
            var record = context.RequestServices.GetRequiredService<IRecordIdempotentResponseCommandHandler>();
            await record.Handle(new RecordIdempotentResponseCommand(keyId, key, stored), CancellationToken.None);
        }
    }

    /// <summary>Method, path, query and body: two requests are "the same" only if all four match.</summary>
    private static async Task<string> Fingerprint(HttpRequest request)
    {
        request.EnableBuffering();
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, request.HttpContext.RequestAborted);
        request.Body.Position = 0;

        var head = Encoding.UTF8.GetBytes($"{request.Method} {request.Path}{request.QueryString}\n");
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(head);
        sha.AppendData(buffer.GetBuffer(), 0, (int)buffer.Length);
        return Convert.ToHexString(sha.GetHashAndReset());
    }
}
