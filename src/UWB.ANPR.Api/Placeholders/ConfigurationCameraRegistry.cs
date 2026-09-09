using Microsoft.Extensions.Options;
using UWB.ANPR.Abstractions;
using UWB.ANPR.Abstractions.Ports;
using UWB.ANPR.Api.Configuration;

namespace UWB.ANPR.Api.Placeholders;

/// <summary>
/// PLACEHOLDER — đọc cấu hình camera từ appsettings để chạy thử.
/// Thay bằng implementation đọc từ database hiện hữu.
/// </summary>
internal sealed class ConfigurationCameraRegistry(IOptionsMonitor<AnprCameraOptions> options) : ICameraRegistry
{
    public Task<IReadOnlyList<CameraDescriptor>> GetEnabledAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CameraDescriptor>>(
            [.. options.CurrentValue.Cameras.Where(c => c.IsEnabled).Select(c => c.ToDescriptor())]);

    public Task<CameraDescriptor?> FindAsync(CameraId cameraId, CancellationToken ct) =>
        Task.FromResult(options.CurrentValue.Cameras
            .FirstOrDefault(c => string.Equals(c.Id, cameraId.Value, StringComparison.OrdinalIgnoreCase))
            ?.ToDescriptor());

    public Task<CameraId?> ResolveByWebhookKeyAsync(string webhookKey, CancellationToken ct)
    {
        var camera = options.CurrentValue.Cameras.FirstOrDefault(c =>
            string.Equals(c.WebhookKey, webhookKey, StringComparison.Ordinal));

        return Task.FromResult(camera is null ? null : CameraId.From(camera.Id) as CameraId?);
    }
}

/// <summary>
/// PLACEHOLDER — đọc credential từ configuration/user-secrets.
/// Production nên dùng secret manager và mã hóa at-rest.
/// </summary>
internal sealed class ConfigurationCredentialResolver(IOptionsMonitor<AnprCameraOptions> options)
    : ICameraCredentialResolver
{
    public Task<CameraCredential?> ResolveAsync(CameraId cameraId, CancellationToken ct)
    {
        var camera = options.CurrentValue.Cameras.FirstOrDefault(c =>
            string.Equals(c.Id, cameraId.Value, StringComparison.OrdinalIgnoreCase));

        if (camera is null || string.IsNullOrEmpty(camera.Username))
        {
            return Task.FromResult<CameraCredential?>(null);
        }

        return Task.FromResult<CameraCredential?>(
            new CameraCredential(camera.Username, camera.Password ?? string.Empty));
    }
}
