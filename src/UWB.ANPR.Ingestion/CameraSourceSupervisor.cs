using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UWB.ANPR.Abstractions;
using UWB.ANPR.Abstractions.Ports;

namespace UWB.ANPR.Ingestion;

/// <summary>
/// Khởi động đúng một Event Source cho mỗi camera đang bật, giám sát và khởi động lại khi crash.
/// Nhờ keyed DI, thêm một loại kết nối mới chỉ cần đăng ký thêm implementation
/// <see cref="IAnprEventSource"/> mà không phải sửa lớp này.
/// </summary>
internal sealed class CameraSourceSupervisor(
    IServiceProvider services,
    ICameraRegistry registry,
    ILogger<CameraSourceSupervisor> logger) : BackgroundService
{
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cameras = await registry.GetEnabledAsync(stoppingToken).ConfigureAwait(false);

        if (cameras.Count == 0)
        {
            logger.LogWarning("Không có camera nào được bật; supervisor không khởi động source nào");
            return;
        }

        var runners = cameras
            .Select(camera => RunCameraAsync(camera, stoppingToken))
            .ToArray();

        logger.LogInformation("Supervisor khởi động {Count} camera source", runners.Length);

        await Task.WhenAll(runners).ConfigureAwait(false);
    }

    private async Task RunCameraAsync(CameraDescriptor camera, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var source = services.GetRequiredKeyedService<IAnprEventSource>(camera.PrimarySource);

                await source.RunAsync(camera, ct).ConfigureAwait(false);

                // Source thụ động (Webhook) trả về ngay và không cần khởi động lại.
                if (camera.PrimarySource is AnprEventSourceKind.Webhook)
                {
                    return;
                }

                logger.LogWarning(
                    "Source {Kind} của camera {CameraId} đã kết thúc, sẽ khởi động lại",
                    camera.PrimarySource,
                    camera.Id);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Source {Kind} của camera {CameraId} dừng bất thường",
                    camera.PrimarySource,
                    camera.Id);
            }

            try
            {
                await Task.Delay(RestartDelay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
