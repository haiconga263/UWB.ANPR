using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UWB.ANPR.Ingestion;

/// <summary>
/// Consumer của hàng đợi nội bộ. Tách khỏi tầng tiếp nhận để webhook luôn ack nhanh,
/// còn phần xử lý nghiệp vụ chạy bất đồng bộ.
/// </summary>
internal sealed class EventProcessingWorker(
    ChannelReader<QueuedEvent> queue,
    IRecognitionProcessor processor,
    ILogger<EventProcessingWorker> logger) : BackgroundService
{
    private const int DegreeOfParallelism = 4;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable
            .Range(0, DegreeOfParallelism)
            .Select(_ => ConsumeAsync(stoppingToken));

        await Task.WhenAll(workers).ConfigureAwait(false);
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var queued in queue.ReadAllAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    await processor.ProcessAsync(queued.CameraId, queued.EventId, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Không rethrow: sự kiện đã ghi bền vững nên Recovery Job sẽ thử lại.
                    logger.LogError(
                        ex,
                        "Xử lý sự kiện {EventId} của camera {CameraId} thất bại",
                        queued.EventId,
                        queued.CameraId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host đang dừng.
        }
    }
}
