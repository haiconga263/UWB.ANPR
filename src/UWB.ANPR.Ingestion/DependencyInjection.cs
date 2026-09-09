using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UWB.ANPR.Abstractions;
using UWB.ANPR.Ingestion.Sources;

namespace UWB.ANPR.Ingestion;

public static class IngestionServiceCollectionExtensions
{
    public static IServiceCollection AddAnprIngestion(
        this IServiceCollection services,
        int queueCapacity = 10_000)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfLessThan(queueCapacity, 1);

        services.TryAddSingleton(TimeProvider.System);
        services.AddMetrics();
        services.TryAddSingleton<IngestMetrics>();
        services.TryAddSingleton<IRecognitionProcessor, NoOpRecognitionProcessor>();

        // Bounded channel chặn tràn bộ nhớ khi burst. DropWrite để tầng tiếp nhận không bị block;
        // sự kiện bị bỏ khỏi hàng đợi vẫn an toàn vì đã ghi bền vững trước đó.
        var channel = Channel.CreateBounded<QueuedEvent>(new BoundedChannelOptions(queueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = false,
            SingleWriter = false,
        });

        services.TryAddSingleton(channel.Reader);
        services.TryAddSingleton(channel.Writer);

        services.TryAddSingleton<IEventIngestPipeline, EventIngestPipeline>();

        // Thêm một loại kết nối mới chỉ cần thêm một dòng ở đây.
        services.AddKeyedSingleton<IAnprEventSource, WebhookEventSource>(AnprEventSourceKind.Webhook);
        services.AddKeyedSingleton<IAnprEventSource, AlarmStreamEventSource>(AnprEventSourceKind.AlarmStream);
        services.AddKeyedSingleton<IAnprEventSource, PollingEventSource>(AnprEventSourceKind.Polling);

        services.AddHostedService<CameraSourceSupervisor>();
        services.AddHostedService<EventProcessingWorker>();

        return services;
    }
}
