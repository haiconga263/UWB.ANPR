using Microsoft.Extensions.Logging;
using UWB.ANPR.Abstractions;
using UWB.ANPR.Hikvision;

namespace UWB.ANPR.Ingestion.Sources;

/// <summary>
/// Giữ một kết nối HTTP dài hạn tới camera và tự kết nối lại theo backoff lũy thừa có jitter.
/// </summary>
internal sealed class AlarmStreamEventSource(
    IHikvisionCameraClient client,
    IEventIngestPipeline pipeline,
    IngestMetrics metrics,
    TimeProvider clock,
    ILogger<AlarmStreamEventSource> logger) : IAnprEventSource
{
    public AnprEventSourceKind Kind => AnprEventSourceKind.AlarmStream;

    public async Task RunAsync(CameraDescriptor camera, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var settings = camera.AlarmStream ?? new AlarmStreamSettings();
        var attempt = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Mở alarm stream tới camera {CameraId}", camera.Id);

                await foreach (var rawEvent in client.StreamAlarmsAsync(camera, ct).ConfigureAwait(false))
                {
                    // Stream đang khỏe nên đặt lại backoff.
                    attempt = 0;
                    await pipeline.IngestAsync(rawEvent, ct).ConfigureAwait(false);
                }

                logger.LogWarning(
                    "Alarm stream camera {CameraId} kết thúc, sẽ kết nối lại", camera.Id);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (CameraAuthenticationException ex)
            {
                // Reconnect vô nghĩa cho tới khi credential được sửa, và còn nguy cơ làm camera lock account.
                logger.LogError(ex, "Alarm stream camera {CameraId} lỗi xác thực, dừng kênh", camera.Id);
                metrics.AuthenticationFailure(camera.Id);
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Alarm stream camera {CameraId} gặp lỗi", camera.Id);
            }

            metrics.StreamReconnect(camera.Id);

            await Task.Delay(ComputeBackoff(++attempt, settings), clock, ct).ConfigureAwait(false);
        }
    }

    internal static TimeSpan ComputeBackoff(int attempt, AlarmStreamSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);

        var exponent = Math.Min(attempt - 1, 10);
        var delay = settings.ReconnectBaseDelay * Math.Pow(2, exponent);

        if (delay > settings.ReconnectMaxDelay)
        {
            delay = settings.ReconnectMaxDelay;
        }

        // Jitter 85%..115% để nhiều camera không cùng reconnect một lúc.
        var jitter = (Random.Shared.NextDouble() * 0.3) + 0.85;
        return delay * jitter;
    }
}
