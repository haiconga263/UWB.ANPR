using UWB.ANPR.Abstractions;

namespace UWB.ANPR.Hikvision;

/// <summary>
/// Khóa để truyền <see cref="CameraId"/> xuống <see cref="DigestAuthHandler"/>.
/// Cần thiết vì một HttpClient dùng chung phải phục vụ nhiều camera có credential khác nhau.
/// </summary>
public static class HikvisionRequestOptions
{
    public const string HttpClientName = "hikvision-isapi";

    public static readonly HttpRequestOptionsKey<CameraId> CameraIdKey = new("uwb.anpr.cameraId");
}
