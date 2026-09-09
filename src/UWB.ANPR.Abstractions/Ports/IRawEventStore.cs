namespace UWB.ANPR.Abstractions.Ports;

/// <summary>
/// Cổng lưu trữ sự kiện thô. Module database hiện hữu implement interface này.
/// </summary>
public interface IRawEventStore
{
    /// <summary>
    /// Ghi bền vững sự kiện. Implementation <b>phải</b> dựa trên unique constraint
    /// <c>(CameraId, EventId)</c> để bảo đảm idempotency, và trả
    /// <see cref="PersistResult.Duplicate"/> thay vì throw khi trùng.
    /// </summary>
    Task<PersistResult> TryPersistAsync(RawCameraEvent rawEvent, CancellationToken ct);

    Task<bool> ExistsAsync(CameraId cameraId, string eventId, CancellationToken ct);

    /// <summary>Các EventId đã có trong khoảng thời gian, dùng cho Backfill phát hiện khoảng trống.</summary>
    Task<IReadOnlySet<string>> GetKnownEventIdsAsync(
        CameraId cameraId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken ct);
}

public readonly record struct PersistResult(bool Persisted, bool IsDuplicate)
{
    public static PersistResult Stored { get; } = new(true, false);

    public static PersistResult Duplicate { get; } = new(false, true);
}
