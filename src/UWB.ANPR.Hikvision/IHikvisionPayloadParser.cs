using UWB.ANPR.Abstractions;

namespace UWB.ANPR.Hikvision;

/// <summary>
/// Cô lập phần phụ thuộc model/firmware. Grammar payload ANPR khác nhau giữa các dòng camera,
/// nên toàn bộ việc đọc/ghi payload nằm sau interface này để không phải sửa client khi
/// xác nhận được payload thật.
/// </summary>
public interface IHikvisionPayloadParser
{
    DeviceInfo ParseDeviceInfo(ReadOnlyMemory<byte> payload);

    string BuildHttpListeningRequest(Uri callbackUrl);

    string BuildEventSearchRequest(DateTimeOffset fromUtc, DateTimeOffset toUtc, int pageSize);

    IReadOnlyList<RawCameraEvent> ParseSearchResults(
        CameraId cameraId,
        ReadOnlyMemory<byte> payload,
        DateTimeOffset receivedAtUtc);

    bool TryParseStreamPart(
        CameraId cameraId,
        MultipartPart part,
        DateTimeOffset receivedAtUtc,
        out RawCameraEvent rawEvent);

    /// <summary>Parse payload webhook, có thể là multipart hoặc XML/JSON đơn.</summary>
    bool TryParseWebhookPayload(
        CameraId cameraId,
        ReadOnlyMemory<byte> body,
        string contentType,
        DateTimeOffset receivedAtUtc,
        out RawCameraEvent rawEvent);
}

public sealed record DeviceInfo(string Model, string FirmwareVersion, string SerialNumber);
