using System.Diagnostics.Metrics;

namespace UWB.ANPR.UnitTests;

/// <summary>MeterFactory tối giản cho unit test, tránh thêm package phụ.</summary>
internal sealed class SimpleMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];

    public Meter Create(MeterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var meter = new Meter(options.Name, options.Version);
        _meters.Add(meter);
        return meter;
    }

    public void Dispose()
    {
        foreach (var meter in _meters)
        {
            meter.Dispose();
        }

        _meters.Clear();
    }
}
