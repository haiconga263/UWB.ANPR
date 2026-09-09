using UWB.ANPR.Abstractions;

namespace UWB.ANPR.Ingestion.Sources;

/// <summary>
/// Camera tự POST tới HTTP endpoint nên source này không cần vòng lặp.
/// Dữ liệu được endpoint webhook đẩy trực tiếp vào <see cref="IEventIngestPipeline"/>.
/// </summary>
internal sealed class WebhookEventSource : IAnprEventSource
{
    public AnprEventSourceKind Kind => AnprEventSourceKind.Webhook;

    public Task RunAsync(CameraDescriptor camera, CancellationToken ct) => Task.CompletedTask;
}
