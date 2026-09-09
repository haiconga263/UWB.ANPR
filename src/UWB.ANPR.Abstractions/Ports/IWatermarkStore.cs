namespace UWB.ANPR.Abstractions.Ports;

/// <summary>Lưu mốc tiến độ của Polling và Backfill theo từng camera.</summary>
public interface IWatermarkStore
{
    Task<DateTimeOffset?> GetAsync(CameraId cameraId, string purpose, CancellationToken ct);

    /// <summary>
    /// Chỉ được gọi sau khi toàn bộ sự kiện tới mốc mới đã ghi bền vững,
    /// nếu không sẽ mất sự kiện khi tiến trình dừng giữa chừng.
    /// </summary>
    Task SetAsync(CameraId cameraId, string purpose, DateTimeOffset value, CancellationToken ct);
}
