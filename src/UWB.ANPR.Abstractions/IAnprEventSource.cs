namespace UWB.ANPR.Abstractions;

/// <summary>
/// Một kênh nhận sự kiện ANPR. Mỗi <see cref="AnprEventSourceKind"/> có một implementation,
/// được đăng ký bằng keyed DI nên thêm loại kết nối mới không phải sửa code điều phối.
/// </summary>
public interface IAnprEventSource
{
    AnprEventSourceKind Kind { get; }

    /// <summary>
    /// Chạy kênh nhận sự kiện cho <paramref name="camera"/> tới khi <paramref name="ct"/> bị cancel.
    /// Source thụ động (Webhook) trả về ngay vì dữ liệu do HTTP endpoint đẩy vào.
    /// Source chủ động (AlarmStream, Polling) chạy vòng lặp và tự phục hồi khi lỗi.
    /// </summary>
    Task RunAsync(CameraDescriptor camera, CancellationToken ct);
}
