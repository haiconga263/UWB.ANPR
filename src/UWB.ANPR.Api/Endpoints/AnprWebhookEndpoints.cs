using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using UWB.ANPR.Abstractions;
using UWB.ANPR.Abstractions.Ports;
using UWB.ANPR.Api.Security;
using UWB.ANPR.Hikvision;

namespace UWB.ANPR.Api.Endpoints;

internal static class AnprWebhookEndpoints
{
    public static IEndpointRouteBuilder MapAnprWebhookEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapPost("/api/anpr/events/{webhookKey}", HandleAsync)
            .WithName("ReceiveAnprEvent")
            .WithSummary("Nhận sự kiện ANPR do camera đẩy tới")
            .DisableAntiforgery();

        return routes;
    }

    private static async Task<IResult> HandleAsync(
        string webhookKey,
        HttpRequest request,
        IWebhookAuthenticator authenticator,
        IOptions<WebhookSecurityOptions> options,
        ICameraRegistry registry,
        IHikvisionPayloadParser parser,
        IEventIngestPipeline pipeline,
        TimeProvider clock,
        ILogger<WebhookLogCategory> logger,
        CancellationToken ct)
    {
        // Bước 1: xác thực nguồn gửi trước khi đọc body.
        if (!authenticator.IsTrusted(request))
        {
            logger.LogWarning(
                "Từ chối webhook không xác thực từ {RemoteIp}",
                request.HttpContext.Connection.RemoteIpAddress);

            return Results.Unauthorized();
        }

        // Bước 2: ánh xạ khóa công khai về CameraId nội bộ.
        var cameraId = await registry.ResolveByWebhookKeyAsync(webhookKey, ct).ConfigureAwait(false);
        if (cameraId is null)
        {
            return Results.NotFound();
        }

        // Bước 3: đọc body với giới hạn kích thước.
        var maxBytes = options.Value.MaxBodyBytes;
        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer, ct).ConfigureAwait(false);

        if (buffer.Length > maxBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var body = buffer.ToArray();
        var contentType = request.ContentType ?? "application/octet-stream";
        var receivedAt = clock.GetUtcNow();

        // Bước 4: parse. Payload lỗi vẫn phải lưu raw bytes để tra soát.
        if (!parser.TryParseWebhookPayload(cameraId.Value, body, contentType, receivedAt, out var rawEvent))
        {
            logger.LogWarning(
                "Payload webhook của camera {CameraId} không parse được, lưu ở dạng thô",
                cameraId.Value);

            rawEvent = new RawCameraEvent
            {
                CameraId = cameraId.Value,
                EventId = BuildFallbackEventId(cameraId.Value, body, receivedAt),
                Source = AnprEventSourceKind.Webhook,
                RawPayload = body,
                PayloadContentType = contentType,
                ReceivedAtUtc = receivedAt,
                CorrelationId = request.HttpContext.TraceIdentifier,
            };
        }

        // Bước 5: persist-before-ack.
        var outcome = await pipeline.IngestAsync(rawEvent, ct).ConfigureAwait(false);

        return outcome switch
        {
            // Đã ghi bền vững nên ack 200 để camera không retry vô ích.
            IngestOutcome.Accepted
                or IngestOutcome.Duplicate
                or IngestOutcome.PersistedButQueueFull
                or IngestOutcome.Discarded => Results.Ok(),

            // Chưa ghi được nên không ack thành công, để camera hoặc Backfill thử lại.
            _ => Results.StatusCode(StatusCodes.Status503ServiceUnavailable),
        };
    }

    /// <summary>
    /// Hash xác định để camera retry cùng payload không tạo thêm bản ghi.
    /// Cố tình không dùng thời gian xử lý hiện tại hay giá trị ngẫu nhiên.
    /// </summary>
    private static string BuildFallbackEventId(CameraId cameraId, byte[] body, DateTimeOffset receivedAt)
    {
        var prefix = Encoding.UTF8.GetBytes(
            $"{cameraId.Value}|{receivedAt.ToUnixTimeSeconds()}|{body.Length}|");

        var material = new byte[prefix.Length + body.Length];
        prefix.CopyTo(material, 0);
        body.CopyTo(material, prefix.Length);

        return Convert.ToHexString(SHA256.HashData(material)).ToLowerInvariant()[..32];
    }

    /// <summary>Category cố định cho logger của endpoint webhook.</summary>
    internal sealed class WebhookLogCategory;
}
