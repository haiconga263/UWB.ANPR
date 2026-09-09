namespace UWB.ANPR.Abstractions;

/// <summary>Cấu hình kết nối của một camera, do <see cref="Ports.ICameraRegistry"/> cung cấp.</summary>
public sealed record CameraDescriptor
{
    public required CameraId Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Ví dụ: https://10.0.0.21:443</summary>
    public required Uri BaseAddress { get; init; }

    public required AnprEventSourceKind PrimarySource { get; init; }

    public AnprEventSourceKind? FallbackSource { get; init; }

    public required bool IsEnabled { get; init; }

    public PollingSettings? Polling { get; init; }

    public AlarmStreamSettings? AlarmStream { get; init; }

    public ResilienceSettings Resilience { get; init; } = new();

    public string? LaneCode { get; init; }
}

public sealed record PollingSettings
{
    public TimeSpan Interval { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Lùi mốc kết thúc để tránh bỏ sót sự kiện camera ghi trễ.</summary>
    public TimeSpan SafetyLag { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Chặn cửa sổ truy vấn quá rộng khi watermark đã cũ.</summary>
    public TimeSpan MaxWindow { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan InitialLookback { get; init; } = TimeSpan.FromMinutes(5);

    public int PageSize { get; init; } = 100;
}

public sealed record AlarmStreamSettings
{
    public TimeSpan ReadTimeout { get; init; } = TimeSpan.FromSeconds(90);

    public TimeSpan ReconnectBaseDelay { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan ReconnectMaxDelay { get; init; } = TimeSpan.FromMinutes(2);
}

public sealed record ResilienceSettings
{
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public int MaxRetryAttempts { get; init; } = 3;

    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(400);

    public int CircuitBreakerMinimumThroughput { get; init; } = 10;

    public double CircuitBreakerFailureRatio { get; init; } = 0.5;

    public TimeSpan CircuitBreakerDuration { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxConcurrentRequests { get; init; } = 4;
}
