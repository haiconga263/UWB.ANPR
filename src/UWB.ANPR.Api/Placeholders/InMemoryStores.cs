using System.Collections.Concurrent;
using UWB.ANPR.Abstractions;
using UWB.ANPR.Abstractions.Ports;

namespace UWB.ANPR.Api.Placeholders;

/// <summary>
/// PLACEHOLDER — thay bằng implementation của module database hiện hữu.
/// Chỉ giữ dữ liệu trong bộ nhớ nên mất khi restart, không dùng cho production.
/// Điểm quan trọng cần giữ lại khi thay: tính duy nhất theo cặp (CameraId, EventId).
/// </summary>
internal sealed class InMemoryRawEventStore : IRawEventStore
{
    private readonly ConcurrentDictionary<(string Camera, string Event), RawCameraEvent> _events = new();

    public Task<PersistResult> TryPersistAsync(RawCameraEvent rawEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rawEvent);

        var key = (rawEvent.CameraId.Value, rawEvent.EventId);

        return Task.FromResult(
            _events.TryAdd(key, rawEvent) ? PersistResult.Stored : PersistResult.Duplicate);
    }

    public Task<bool> ExistsAsync(CameraId cameraId, string eventId, CancellationToken ct) =>
        Task.FromResult(_events.ContainsKey((cameraId.Value, eventId)));

    public Task<IReadOnlySet<string>> GetKnownEventIdsAsync(
        CameraId cameraId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken ct)
    {
        var ids = _events.Values
            .Where(e => e.CameraId == cameraId
                && e.ReceivedAtUtc >= fromUtc
                && e.ReceivedAtUtc < toUtc)
            .Select(e => e.EventId)
            .ToHashSet(StringComparer.Ordinal);

        return Task.FromResult<IReadOnlySet<string>>(ids);
    }
}

/// <summary>PLACEHOLDER — thay bằng bảng watermark trong module database hiện hữu.</summary>
internal sealed class InMemoryWatermarkStore : IWatermarkStore
{
    private readonly ConcurrentDictionary<(string Camera, string Purpose), DateTimeOffset> _watermarks = new();

    public Task<DateTimeOffset?> GetAsync(CameraId cameraId, string purpose, CancellationToken ct) =>
        Task.FromResult(_watermarks.TryGetValue((cameraId.Value, purpose), out var value)
            ? value
            : null as DateTimeOffset?);

    public Task SetAsync(CameraId cameraId, string purpose, DateTimeOffset value, CancellationToken ct)
    {
        _watermarks[(cameraId.Value, purpose)] = value;
        return Task.CompletedTask;
    }
}
