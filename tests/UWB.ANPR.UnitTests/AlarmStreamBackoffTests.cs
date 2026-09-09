using UWB.ANPR.Abstractions;
using UWB.ANPR.Ingestion.Sources;
using Xunit;

namespace UWB.ANPR.UnitTests;

public sealed class AlarmStreamBackoffTests
{
    private static readonly AlarmStreamSettings Settings = new()
    {
        ReconnectBaseDelay = TimeSpan.FromSeconds(2),
        ReconnectMaxDelay = TimeSpan.FromMinutes(2),
    };

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(50)]
    public void Backoff_khong_bao_gio_vuot_gioi_han(int attempt)
    {
        var delay = AlarmStreamEventSource.ComputeBackoff(attempt, Settings);

        // Jitter tối đa 115% nên cận trên là max * 1.15.
        Assert.InRange(delay, TimeSpan.Zero, Settings.ReconnectMaxDelay * 1.15);
    }

    [Fact]
    public void Backoff_tang_theo_so_lan_thu()
    {
        // So sánh trung bình để loại ảnh hưởng của jitter.
        var early = Enumerable.Range(0, 50)
            .Average(_ => AlarmStreamEventSource.ComputeBackoff(1, Settings).TotalMilliseconds);

        var later = Enumerable.Range(0, 50)
            .Average(_ => AlarmStreamEventSource.ComputeBackoff(4, Settings).TotalMilliseconds);

        Assert.True(later > early, $"kỳ vọng {later} > {early}");
    }

    [Fact]
    public void Attempt_khong_hop_le_thi_nem_loi()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AlarmStreamEventSource.ComputeBackoff(0, Settings));
    }
}
