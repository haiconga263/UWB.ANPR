using System.Diagnostics.Metrics;
using UWB.ANPR.Abstractions;

namespace UWB.ANPR.Ingestion;

/// <summary>Metric tối thiểu để vận hành theo dõi sức khỏe luồng tiếp nhận.</summary>
public sealed class IngestMetrics : IDisposable
{
    public const string MeterName = "UWB.ANPR.Ingestion";

    private readonly Meter _meter;
    private readonly Counter<long> _received;
    private readonly Counter<long> _duplicates;
    private readonly Counter<long> _persistFailures;
    private readonly Counter<long> _queueOverflows;
    private readonly Counter<long> _streamReconnects;
    private readonly Counter<long> _authFailures;

    public IngestMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        _meter = meterFactory.Create(MeterName);
        _received = _meter.CreateCounter<long>("anpr.events.received");
        _duplicates = _meter.CreateCounter<long>("anpr.events.duplicate");
        _persistFailures = _meter.CreateCounter<long>("anpr.events.persist_failed");
        _queueOverflows = _meter.CreateCounter<long>("anpr.queue.overflow");
        _streamReconnects = _meter.CreateCounter<long>("anpr.stream.reconnect");
        _authFailures = _meter.CreateCounter<long>("anpr.camera.auth_failed");
    }

    public void EventReceived(CameraId cameraId, AnprEventSourceKind source) =>
        _received.Add(1, Tag(cameraId), new KeyValuePair<string, object?>("source", source.ToString()));

    public void Duplicate(CameraId cameraId) => _duplicates.Add(1, Tag(cameraId));

    public void PersistFailed(CameraId cameraId) => _persistFailures.Add(1, Tag(cameraId));

    public void QueueOverflow(CameraId cameraId) => _queueOverflows.Add(1, Tag(cameraId));

    public void StreamReconnect(CameraId cameraId) => _streamReconnects.Add(1, Tag(cameraId));

    public void AuthenticationFailure(CameraId cameraId) => _authFailures.Add(1, Tag(cameraId));

    public void Dispose() => _meter.Dispose();

    private static KeyValuePair<string, object?> Tag(CameraId cameraId) => new("camera_id", cameraId.Value);
}
