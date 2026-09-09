namespace UWB.ANPR.Abstractions.Ports;

/// <summary>
/// Cung cấp credential đã giải mã cho từng camera.
/// Giá trị trả về không được ghi log hoặc trả ra API.
/// </summary>
public interface ICameraCredentialResolver
{
    Task<CameraCredential?> ResolveAsync(CameraId cameraId, CancellationToken ct);
}

public sealed record CameraCredential(string Username, string Password);
