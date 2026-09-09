using System.ComponentModel.DataAnnotations;
using UWB.ANPR.Abstractions;

namespace UWB.ANPR.Api.Configuration;

public sealed class AnprCameraOptions
{
    public const string SectionName = "Anpr";

    public IList<CameraConfigurationEntry> Cameras { get; init; } = [];
}

public sealed class CameraConfigurationEntry
{
    [Required]
    public string Id { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    [Required]
    public string BaseAddress { get; init; } = string.Empty;

    public AnprEventSourceKind PrimarySource { get; init; } = AnprEventSourceKind.Webhook;

    public AnprEventSourceKind? FallbackSource { get; init; }

    public bool IsEnabled { get; init; } = true;

    /// <summary>Khóa công khai xuất hiện trong URL webhook, không dùng CameraId nội bộ.</summary>
    public string? WebhookKey { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string? LaneCode { get; init; }

    public int? PollingIntervalSeconds { get; init; }

    public CameraDescriptor ToDescriptor() => new()
    {
        Id = CameraId.From(Id),
        DisplayName = DisplayName ?? Id,
        BaseAddress = new Uri(BaseAddress, UriKind.Absolute),
        PrimarySource = PrimarySource,
        FallbackSource = FallbackSource,
        IsEnabled = IsEnabled,
        LaneCode = LaneCode,
        Polling = PrimarySource is AnprEventSourceKind.Polling
            ? new PollingSettings
            {
                Interval = TimeSpan.FromSeconds(PollingIntervalSeconds ?? 15),
            }
            : null,
        AlarmStream = PrimarySource is AnprEventSourceKind.AlarmStream
            ? new AlarmStreamSettings()
            : null,
    };
}
