using Microsoft.Extensions.Logging;
using UWB.ANPR.Abstractions;
using UWB.ANPR.Abstractions.Ports;
using UWB.ANPR.Hikvision;

namespace UWB.ANPR.Ingestion.Sources;

/// <summary>
/// Định kỳ gọi search API của camera. Watermark chỉ được dịch sau khi toàn bộ sự kiện
/// trong cửa sổ đã ghi bền vững, nhờ vậy sự cố giữa chừng không làm mất sự kiện.
/// </summary>
internal sealed class PollingEventSource(
    IHikvisionCameraClient client,
    IEventIngestPipeline pipeline,
    IWatermarkStore watermarks,
    TimeProvider clock,
    ILogger<PollingEventSource> logger) : IAnprEventSource
{
    internal const string WatermarkPurpose = "polling";

    public AnprEventSourceKind Kind => AnprEventSourceKind.Polling;

    public async Task RunAsync(CameraDescriptor camera, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var settings = camera.Polling
            ?? throw new InvalidOperationException(
                $"Camera {camera.Id} chọn Polling nhưng thiếu PollingSettings.");

        using var timer = new PeriodicTimer(settings.Interval, clock);

        do
        {
            try
            {
                await PollOnceAsync(camera, settings, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (CameraAuthenticationException ex)
            {
                // Lỗi cấu hình: tiếp tục poll cũng không khỏi cho tới khi người vận hành sửa credential.
                logger.LogError(ex, "Camera {CameraId} lỗi xác thực, dừng polling", camera.Id);
                return;
            }
            catch (Exception ex)
            {
                // Không để một lần poll lỗi giết vòng lặp; watermark chưa dịch nên lần sau lấy lại.
                logger.LogError(ex, "Poll thất bại cho camera {CameraId}", camera.Id);
            }
        }
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
    }

    private async Task PollOnceAsync(
        CameraDescriptor camera,
        PollingSettings settings,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        var watermark = await watermarks.GetAsync(camera.Id, WatermarkPurpose, ct).ConfigureAwait(false);

        var (from, to) = ComputeWindow(watermark, now, settings);

        if (to <= from)
        {
            return;
        }

        var events = await client
            .SearchEventsAsync(camera, from, to, settings.PageSize, ct)
            .ConfigureAwait(false);

        foreach (var rawEvent in events)
        {
            var outcome = await pipeline.IngestAsync(rawEvent, ct).ConfigureAwait(false);

            if (outcome is IngestOutcome.PersistFailed)
            {
                logger.LogWarning(
                    "Giữ nguyên watermark của camera {CameraId} vì có sự kiện chưa ghi được",
                    camera.Id);

                return;
            }
        }

        await watermarks.SetAsync(camera.Id, WatermarkPurpose, to, ct).ConfigureAwait(false);

        logger.LogDebug(
            "Poll camera {CameraId}: {Count} sự kiện, watermark chuyển tới {To:O}",
            camera.Id,
            events.Count,
            to);
    }

    /// <summary>
    /// Tính cửa sổ truy vấn. Tách thành hàm thuần để kiểm thử được không cần timer.
    /// </summary>
    internal static (DateTimeOffset From, DateTimeOffset To) ComputeWindow(
        DateTimeOffset? watermark,
        DateTimeOffset now,
        PollingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var from = watermark ?? now - settings.InitialLookback;

        // SafetyLag: không đọc sát thời điểm hiện tại vì camera có thể ghi sự kiện trễ vài giây.
        var to = now - settings.SafetyLag;

        // Chặn cửa sổ quá rộng khi watermark đã cũ, ví dụ sau một khoảng downtime dài.
        if (to - from > settings.MaxWindow)
        {
            to = from + settings.MaxWindow;
        }

        return (from, to);
    }
}
