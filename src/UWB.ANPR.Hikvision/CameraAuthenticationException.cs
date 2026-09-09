using System.Net;
using UWB.ANPR.Abstractions;

namespace UWB.ANPR.Hikvision;

/// <summary>
/// Camera từ chối xác thực (401/403). Đây là lỗi cấu hình, không phải lỗi tạm thời,
/// nên cố tình không nằm trong tập lỗi được retry để tránh làm camera lock account.
/// </summary>
public sealed class CameraAuthenticationException(CameraId cameraId, HttpStatusCode statusCode)
    : Exception($"Camera {cameraId} từ chối xác thực ({(int)statusCode}). Cần cập nhật credential.")
{
    public CameraId CameraId { get; } = cameraId;

    public HttpStatusCode StatusCode { get; } = statusCode;
}
