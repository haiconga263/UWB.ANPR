using System.Globalization;
using System.Text;
using System.Xml.Linq;
using UWB.ANPR.Abstractions;
using UWB.ANPR.Hikvision;

namespace UWB.ANPR.Api.Placeholders;

/// <summary>
/// PLACEHOLDER — parser tối giản để chạy thử pipeline.
/// Grammar payload ANPR khác nhau theo model/firmware nên phải thay bằng implementation
/// đúng theo payload mẫu thật của camera trước khi dùng production.
/// </summary>
internal sealed class StubPayloadParser : IHikvisionPayloadParser
{
    public DeviceInfo ParseDeviceInfo(ReadOnlyMemory<byte> payload)
    {
        var document = TryLoadXml(payload);

        return new DeviceInfo(
            Model: Value(document, "model") ?? "unknown",
            FirmwareVersion: Value(document, "firmwareVersion") ?? "unknown",
            SerialNumber: Value(document, "serialNumber") ?? "unknown");
    }

    public string BuildHttpListeningRequest(Uri callbackUrl)
    {
        ArgumentNullException.ThrowIfNull(callbackUrl);

        return $"""
            <HttpHostNotificationList>
              <HttpHostNotification>
                <id>1</id>
                <url>{callbackUrl.AbsoluteUri}</url>
                <protocolType>{callbackUrl.Scheme.ToUpperInvariant()}</protocolType>
                <addressingFormatType>hostname</addressingFormatType>
              </HttpHostNotification>
            </HttpHostNotificationList>
            """;
    }

    public string BuildEventSearchRequest(DateTimeOffset fromUtc, DateTimeOffset toUtc, int pageSize) =>
        $"""
        <AfterTime>
          <picTime>{fromUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)}</picTime>
          <endTime>{toUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)}</endTime>
          <maxResults>{pageSize.ToString(CultureInfo.InvariantCulture)}</maxResults>
        </AfterTime>
        """;

    public IReadOnlyList<RawCameraEvent> ParseSearchResults(
        CameraId cameraId,
        ReadOnlyMemory<byte> payload,
        DateTimeOffset receivedAtUtc)
    {
        var document = TryLoadXml(payload);
        if (document is null)
        {
            return [];
        }

        var results = new List<RawCameraEvent>();

        foreach (var plate in document.Descendants().Where(e => e.Name.LocalName == "Plate"))
        {
            var plateText = plate.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "plateNumber")?.Value;

            if (string.IsNullOrWhiteSpace(plateText))
            {
                continue;
            }

            var bytes = Encoding.UTF8.GetBytes(plate.ToString());

            results.Add(new RawCameraEvent
            {
                CameraId = cameraId,
                EventId = DeterministicEventId(cameraId, bytes),
                Source = AnprEventSourceKind.Polling,
                RawPayload = bytes,
                PayloadContentType = "application/xml",
                PlateText = plateText.Trim(),
                ReceivedAtUtc = receivedAtUtc,
            });
        }

        return results;
    }

    public bool TryParseStreamPart(
        CameraId cameraId,
        MultipartPart part,
        DateTimeOffset receivedAtUtc,
        out RawCameraEvent rawEvent)
    {
        ArgumentNullException.ThrowIfNull(part);

        return TryParseWebhookPayload(cameraId, part.Body, part.ContentType, receivedAtUtc, out rawEvent);
    }

    public bool TryParseWebhookPayload(
        CameraId cameraId,
        ReadOnlyMemory<byte> body,
        string contentType,
        DateTimeOffset receivedAtUtc,
        out RawCameraEvent rawEvent)
    {
        rawEvent = null!;

        var document = TryLoadXml(body);
        if (document is null)
        {
            return false;
        }

        var plateText = Value(document, "plateNumber");
        if (string.IsNullOrWhiteSpace(plateText))
        {
            return false;
        }

        rawEvent = new RawCameraEvent
        {
            CameraId = cameraId,
            EventId = Value(document, "eventId") is { Length: > 0 } id
                ? id
                : DeterministicEventId(cameraId, body.ToArray()),
            Source = AnprEventSourceKind.Webhook,
            RawPayload = body,
            PayloadContentType = contentType,
            PlateText = plateText.Trim(),
            Confidence = ParseConfidence(Value(document, "confidenceLevel")),
            CameraTimestampUtc = ParseTimestamp(Value(document, "dateTime")),
            ReceivedAtUtc = receivedAtUtc,
        };

        return true;
    }

    private static XDocument? TryLoadXml(ReadOnlyMemory<byte> payload)
    {
        try
        {
            return XDocument.Parse(Encoding.UTF8.GetString(payload.Span));
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or DecoderFallbackException)
        {
            return null;
        }
    }

    private static string? Value(XDocument? document, string localName) =>
        document?.Descendants()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            ?.Value;

    /// <summary>Chuẩn hóa confidence về miền [0,1]; camera thường trả thang 0..100.</summary>
    private static decimal? ParseConfidence(string? raw)
    {
        if (!decimal.TryParse(raw, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return value > 1m ? value / 100m : value;
    }

    private static DateTimeOffset? ParseTimestamp(string? raw) =>
        DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? value.ToUniversalTime()
            : null;

    private static string DeterministicEventId(CameraId cameraId, byte[] payload)
    {
        var prefix = Encoding.UTF8.GetBytes($"{cameraId.Value}|");
        var material = new byte[prefix.Length + payload.Length];
        prefix.CopyTo(material, 0);
        payload.CopyTo(material, prefix.Length);

        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(material))
            .ToLowerInvariant()[..32];
    }
}
