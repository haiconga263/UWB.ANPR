using UWB.ANPR.Abstractions;

namespace UWB.ANPR.Hikvision;

/// <summary>Giao tiếp HTTP với camera Hikvision qua ISAPI.</summary>
public interface IHikvisionCameraClient
{
    /// <summary>Đọc thông tin thiết bị, dùng cho health probe và kiểm tra kết nối.</summary>
    Task<DeviceInfo> GetDeviceInfoAsync(CameraDescriptor camera, CancellationToken ct);

    /// <summary>Cấu hình camera POST sự kiện về URL của hệ thống (chế độ Webhook).</summary>
    Task ConfigureHttpListeningAsync(CameraDescriptor camera, Uri callbackUrl, CancellationToken ct);

    /// <summary>Mở alarm stream. Stream chỉ kết thúc khi bị cancel hoặc kết nối đứt.</summary>
    IAsyncEnumerable<RawCameraEvent> StreamAlarmsAsync(CameraDescriptor camera, CancellationToken ct);

    /// <summary>Tìm sự kiện trong khoảng thời gian, dùng cho Polling và Backfill.</summary>
    Task<IReadOnlyList<RawCameraEvent>> SearchEventsAsync(
        CameraDescriptor camera,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int pageSize,
        CancellationToken ct);

    Task<CameraImage> DownloadImageAsync(CameraDescriptor camera, Uri imageUri, CancellationToken ct);
}
