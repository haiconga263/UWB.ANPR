namespace UWB.ANPR.Abstractions.Ports;

/// <summary>Nguồn cấu hình camera.</summary>
public interface ICameraRegistry
{
    Task<IReadOnlyList<CameraDescriptor>> GetEnabledAsync(CancellationToken ct);

    Task<CameraDescriptor?> FindAsync(CameraId cameraId, CancellationToken ct);

    /// <summary>Ánh xạ khóa công khai trong URL webhook về CameraId nội bộ.</summary>
    Task<CameraId?> ResolveByWebhookKeyAsync(string webhookKey, CancellationToken ct);
}
