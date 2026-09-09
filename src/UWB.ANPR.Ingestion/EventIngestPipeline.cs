using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using UWB.ANPR.Abstractions;
using UWB.ANPR.Abstractions.Ports;

namespace UWB.ANPR.Ingestion;

/// <summary>
/// Thực thi bất biến persist-before-ack. Mọi Event Source đều đi qua đây, nhờ vậy quy tắc
/// "ghi bền vững trước khi ack" chỉ tồn tại ở một chỗ duy nhất.
/// </summary>
internal sealed class EventIngestPipeline(
    IRawEventStore store,
    ChannelWriter<QueuedEvent> queue,
    IngestMetrics metrics,
    ILogger<EventIngestPipeline> logger) : IEventIngestPipeline
{
    public async Task<IngestOutcome> IngestAsync(RawCameraEvent rawEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rawEvent);

        metrics.EventReceived(rawEvent.CameraId, rawEvent.Source);

        PersistResult persisted;

        try
        {
            // Bước 1: ghi bền vững trước. Chỉ sau bước này caller mới được phép ack.
            persisted = await store.TryPersistAsync(rawEvent, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Không ghi được sự kiện {EventId} của camera {CameraId}",
                rawEvent.EventId,
                rawEvent.CameraId);

            metrics.PersistFailed(rawEvent.CameraId);
            return IngestOutcome.PersistFailed;
        }

        if (persisted.IsDuplicate)
        {
            metrics.Duplicate(rawEvent.CameraId);
            return IngestOutcome.Duplicate;
        }

        if (!persisted.Persisted)
        {
            metrics.PersistFailed(rawEvent.CameraId);
            return IngestOutcome.PersistFailed;
        }

        // Bước 2: đưa vào hàng đợi. Thất bại ở đây không làm mất dữ liệu vì đã ghi bền vững.
        if (!queue.TryWrite(new QueuedEvent(rawEvent.CameraId, rawEvent.EventId)))
        {
            metrics.QueueOverflow(rawEvent.CameraId);

            logger.LogWarning(
                "Hàng đợi đầy; sự kiện {EventId} chờ Recovery Job xử lý lại",
                rawEvent.EventId);

            return IngestOutcome.PersistedButQueueFull;
        }

        return IngestOutcome.Accepted;
    }
}
