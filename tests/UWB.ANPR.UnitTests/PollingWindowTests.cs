using UWB.ANPR.Abstractions;
using UWB.ANPR.Ingestion.Sources;
using Xunit;

namespace UWB.ANPR.UnitTests;

public sealed class PollingWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    private static readonly PollingSettings Settings = new()
    {
        SafetyLag = TimeSpan.FromSeconds(5),
        MaxWindow = TimeSpan.FromMinutes(15),
        InitialLookback = TimeSpan.FromMinutes(5),
    };

    [Fact]
    public void Lan_dau_khong_co_watermark_thi_lui_theo_InitialLookback()
    {
        var (from, to) = PollingEventSource.ComputeWindow(watermark: null, Now, Settings);

        Assert.Equal(Now - Settings.InitialLookback, from);
        Assert.Equal(Now - Settings.SafetyLag, to);
    }

    [Fact]
    public void Moc_ket_thuc_luon_lui_lai_SafetyLag()
    {
        var watermark = Now - TimeSpan.FromMinutes(1);

        var (_, to) = PollingEventSource.ComputeWindow(watermark, Now, Settings);

        // Không bao giờ đọc sát hiện tại, tránh bỏ sót sự kiện camera ghi trễ.
        Assert.Equal(Now - Settings.SafetyLag, to);
        Assert.True(to < Now);
    }

    [Fact]
    public void Watermark_qua_cu_thi_cat_theo_MaxWindow()
    {
        // Mô phỏng downtime 10 giờ.
        var watermark = Now - TimeSpan.FromHours(10);

        var (from, to) = PollingEventSource.ComputeWindow(watermark, Now, Settings);

        Assert.Equal(watermark, from);
        Assert.Equal(watermark + Settings.MaxWindow, to);
        Assert.Equal(Settings.MaxWindow, to - from);
    }

    [Fact]
    public void Watermark_moi_hon_SafetyLag_cho_cua_so_rong_khong_duong()
    {
        // Vừa poll xong: to <= from nên caller phải bỏ lượt này.
        var watermark = Now - TimeSpan.FromSeconds(1);

        var (from, to) = PollingEventSource.ComputeWindow(watermark, Now, Settings);

        Assert.True(to <= from);
    }

    [Fact]
    public void Cua_so_khong_bao_gio_vuot_MaxWindow()
    {
        foreach (var minutesAgo in new[] { 0.1, 1, 14, 15, 16, 120, 6000 })
        {
            var watermark = Now - TimeSpan.FromMinutes(minutesAgo);
            var (from, to) = PollingEventSource.ComputeWindow(watermark, Now, Settings);

            Assert.True(to - from <= Settings.MaxWindow, $"minutesAgo={minutesAgo}");
        }
    }
}
