namespace UWB.ANPR.Abstractions;

/// <summary>Loại kênh nhận sự kiện ANPR từ camera.</summary>
public enum AnprEventSourceKind
{
    /// <summary>Camera chủ động POST sự kiện tới hệ thống.</summary>
    Webhook = 1,

    /// <summary>Hệ thống mở kết nối HTTP dài hạn, camera stream liên tục.</summary>
    AlarmStream = 2,

    /// <summary>Hệ thống định kỳ gọi search API của camera.</summary>
    Polling = 3,
}
