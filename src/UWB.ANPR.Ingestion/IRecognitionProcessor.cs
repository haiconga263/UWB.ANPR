using UWB.ANPR.Abstractions;

namespace UWB.ANPR.Ingestion;

/// <summary>
/// Phần xử lý nghiệp vụ sau khi sự kiện đã được tiếp nhận: chuẩn hóa biển số,
/// đối sánh dữ liệu xe và ghi kết quả. Được implement ở tầng nghiệp vụ.
/// </summary>
public interface IRecognitionProcessor
{
    Task ProcessAsync(CameraId cameraId, string eventId, CancellationToken ct);
}

/// <summary>
/// Implementation mặc định không làm gì, để host chạy được trước khi tầng nghiệp vụ hoàn thiện.
/// </summary>
public sealed class NoOpRecognitionProcessor : IRecognitionProcessor
{
    public Task ProcessAsync(CameraId cameraId, string eventId, CancellationToken ct) => Task.CompletedTask;
}
