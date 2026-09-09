namespace UWB.ANPR.Abstractions;

/// <summary>
/// Điểm vào duy nhất cho mọi Event Source. Bảo đảm bất biến <b>persist-before-ack</b>:
/// sự kiện được ghi bền vững trước khi caller phát acknowledgement.
/// </summary>
public interface IEventIngestPipeline
{
    Task<IngestOutcome> IngestAsync(RawCameraEvent rawEvent, CancellationToken ct);
}

public enum IngestOutcome
{
    /// <summary>Đã ghi bền vững và đã vào hàng đợi xử lý.</summary>
    Accepted = 1,

    /// <summary>Cặp (CameraId, EventId) đã tồn tại nên không xử lý lại.</summary>
    Duplicate = 2,

    /// <summary>Đã ghi bền vững nhưng hàng đợi đầy; Recovery Job sẽ nhặt lại. Vẫn được ack.</summary>
    PersistedButQueueFull = 3,

    /// <summary>Payload sai định dạng nhưng raw bytes đã lưu để tra soát. Vẫn được ack.</summary>
    Discarded = 4,

    /// <summary>Chưa ghi được nên không được ack thành công.</summary>
    PersistFailed = 5,
}
