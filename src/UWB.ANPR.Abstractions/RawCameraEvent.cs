namespace UWB.ANPR.Abstractions;

/// <summary>
/// Sự kiện thô từ camera: đã tách khỏi giao thức vận chuyển nhưng chưa xử lý nghiệp vụ.
/// Payload gốc được giữ nguyên bytes để có thể replay và tra soát.
/// </summary>
public sealed record RawCameraEvent
{
    public required CameraId CameraId { get; init; }

    /// <summary>Ổn định và duy nhất theo camera. Dùng làm khóa chống trùng.</summary>
    public required string EventId { get; init; }

    public required AnprEventSourceKind Source { get; init; }

    public required ReadOnlyMemory<byte> RawPayload { get; init; }

    public required string PayloadContentType { get; init; }

    public string? PlateText { get; init; }

    /// <summary>Đã chuẩn hóa về miền [0,1] tại lớp parse.</summary>
    public decimal? Confidence { get; init; }

    public DateTimeOffset? CameraTimestampUtc { get; init; }

    public required DateTimeOffset ReceivedAtUtc { get; init; }

    public IReadOnlyList<CameraImage> Images { get; init; } = [];

    public string? CorrelationId { get; init; }
}

public sealed record CameraImage(CameraImageKind Kind, ReadOnlyMemory<byte> Bytes, string ContentType);

public enum CameraImageKind
{
    FullScene = 1,
    PlateCrop = 2,
}
