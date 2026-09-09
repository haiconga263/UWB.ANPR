using UWB.ANPR.Abstractions;

namespace UWB.ANPR.Ingestion;

/// <summary>Tham chiếu nhẹ tới sự kiện đã ghi bền vững, dùng trong hàng đợi nội bộ.</summary>
public readonly record struct QueuedEvent(CameraId CameraId, string EventId);
