using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using UWB.ANPR.Abstractions;
using UWB.ANPR.Abstractions.Ports;
using UWB.ANPR.Ingestion;
using Xunit;

namespace UWB.ANPR.UnitTests;

public sealed class EventIngestPipelineTests
{
    [Fact]
    public async Task Ghi_thanh_cong_thi_tra_Accepted()
    {
        var store = Substitute.For<IRawEventStore>();
        store.TryPersistAsync(Arg.Any<RawCameraEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PersistResult.Stored));

        var (pipeline, _) = Build(store, capacity: 4);

        var outcome = await pipeline.IngestAsync(AnyEvent(), CancellationToken.None);

        Assert.Equal(IngestOutcome.Accepted, outcome);
    }

    [Fact]
    public async Task Trung_khoa_thi_tra_Duplicate()
    {
        var store = Substitute.For<IRawEventStore>();
        store.TryPersistAsync(Arg.Any<RawCameraEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PersistResult.Duplicate));

        var (pipeline, reader) = Build(store, capacity: 4);

        var outcome = await pipeline.IngestAsync(AnyEvent(), CancellationToken.None);

        Assert.Equal(IngestOutcome.Duplicate, outcome);

        // Trùng thì không được đưa vào hàng đợi để tránh xử lý lặp.
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public async Task Khong_ghi_duoc_thi_tra_PersistFailed_de_khong_ack()
    {
        var store = Substitute.For<IRawEventStore>();
        store.TryPersistAsync(Arg.Any<RawCameraEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database offline"));

        var (pipeline, _) = Build(store, capacity: 4);

        var outcome = await pipeline.IngestAsync(AnyEvent(), CancellationToken.None);

        Assert.Equal(IngestOutcome.PersistFailed, outcome);
    }

    [Fact]
    public async Task Hang_doi_day_van_bao_da_persist_de_duoc_ack()
    {
        var store = Substitute.For<IRawEventStore>();
        store.TryPersistAsync(Arg.Any<RawCameraEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PersistResult.Stored));

        // capacity 1 + DropWrite: ghi lần thứ hai sẽ bị loại khỏi hàng đợi.
        var (pipeline, _) = Build(store, capacity: 1, fillQueue: true);

        var outcome = await pipeline.IngestAsync(AnyEvent(), CancellationToken.None);

        Assert.Equal(IngestOutcome.PersistedButQueueFull, outcome);
    }

    private static (IEventIngestPipeline Pipeline, ChannelReader<QueuedEvent> Reader) Build(
        IRawEventStore store,
        int capacity,
        bool fillQueue = false)
    {
        var channel = Channel.CreateBounded<QueuedEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
        });

        if (fillQueue)
        {
            for (var i = 0; i < capacity; i++)
            {
                channel.Writer.TryWrite(new QueuedEvent(CameraId.From("filler"), $"e{i}"));
            }
        }

        var metrics = new IngestMetrics(new SimpleMeterFactory());

        var pipeline = new EventIngestPipeline(
            store,
            channel.Writer,
            metrics,
            NullLogger<EventIngestPipeline>.Instance);

        return (pipeline, channel.Reader);
    }

    private static RawCameraEvent AnyEvent() => new()
    {
        CameraId = CameraId.From("cam-01"),
        EventId = "evt-001",
        Source = AnprEventSourceKind.Webhook,
        RawPayload = new byte[] { 1, 2, 3 },
        PayloadContentType = "application/xml",
        ReceivedAtUtc = DateTimeOffset.UnixEpoch,
    };
}
