using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Retry;
using Polly.Timeout;
using UWB.ANPR.Abstractions;

namespace UWB.ANPR.Hikvision;

internal sealed class HikvisionCameraClient(
    IHttpClientFactory httpClientFactory,
    ResiliencePipelineRegistry<string> pipelineRegistry,
    IHikvisionPayloadParser parser,
    ILogger<HikvisionCameraClient> logger) : IHikvisionCameraClient
{
    private const string DeviceInfoPath = "/ISAPI/System/deviceInfo";
    private const string HttpHostsPath = "/ISAPI/Event/notification/httpHosts";
    private const string AlertStreamPath = "/ISAPI/Event/notification/alertStream";
    private const string PlateSearchPath = "/ISAPI/Traffic/channels/1/vehicleDetect/plates";

    public async Task<DeviceInfo> GetDeviceInfoAsync(CameraDescriptor camera, CancellationToken ct)
    {
        var payload = await SendAsync(camera, HttpMethod.Get, DeviceInfoPath, content: null, ct)
            .ConfigureAwait(false);

        return parser.ParseDeviceInfo(payload);
    }

    public async Task ConfigureHttpListeningAsync(CameraDescriptor camera, Uri callbackUrl, CancellationToken ct)
    {
        var body = parser.BuildHttpListeningRequest(callbackUrl);

        using var content = new StringContent(body, Encoding.UTF8, "application/xml");
        await SendAsync(camera, HttpMethod.Put, HttpHostsPath, content, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RawCameraEvent>> SearchEventsAsync(
        CameraDescriptor camera,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int pageSize,
        CancellationToken ct)
    {
        var body = parser.BuildEventSearchRequest(fromUtc, toUtc, pageSize);

        using var content = new StringContent(body, Encoding.UTF8, "application/xml");
        var payload = await SendAsync(camera, HttpMethod.Post, PlateSearchPath, content, ct)
            .ConfigureAwait(false);

        return parser.ParseSearchResults(camera.Id, payload, DateTimeOffset.UtcNow);
    }

    public async Task<CameraImage> DownloadImageAsync(CameraDescriptor camera, Uri imageUri, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(imageUri);

        var path = imageUri.IsAbsoluteUri ? imageUri.PathAndQuery : imageUri.OriginalString;
        var bytes = await SendAsync(camera, HttpMethod.Get, path, content: null, ct).ConfigureAwait(false);

        return new CameraImage(CameraImageKind.FullScene, bytes, "image/jpeg");
    }

    /// <summary>
    /// Alarm stream cố tình không đi qua retry/circuit-breaker pipeline: đây là kết nối dài hạn,
    /// việc reconnect do AlarmStreamEventSource quản lý bằng backoff riêng.
    /// </summary>
    public async IAsyncEnumerable<RawCameraEvent> StreamAlarmsAsync(
        CameraDescriptor camera,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var client = CreateClient(camera);

        // Stream sống lâu nên bỏ timeout tổng; dùng read timeout riêng trong MultipartEventReader.
        client.Timeout = Timeout.InfiniteTimeSpan;

        using var request = BuildRequest(camera, HttpMethod.Get, AlertStreamPath, content: null);
        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new CameraAuthenticationException(camera.Id, response.StatusCode);
        }

        response.EnsureSuccessStatusCode();

        var boundary = MultipartEventReader.ResolveBoundary(response.Content.Headers);
        var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        await using (stream.ConfigureAwait(false))
        {
            var reader = new MultipartEventReader(stream, boundary, camera.AlarmStream?.ReadTimeout);

            await foreach (var part in reader.ReadPartsAsync(ct).ConfigureAwait(false))
            {
                if (parser.TryParseStreamPart(camera.Id, part, DateTimeOffset.UtcNow, out var rawEvent))
                {
                    yield return rawEvent;
                }
                else
                {
                    logger.LogWarning(
                        "Bỏ qua alarm part không parse được từ camera {CameraId}", camera.Id);
                }
            }
        }
    }

    private async Task<byte[]> SendAsync(
        CameraDescriptor camera,
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var pipeline = GetPipeline(camera);

        return await pipeline.ExecuteAsync(
            async token =>
            {
                var client = CreateClient(camera);

                using var request = BuildRequest(camera, method, path, content);
                using var response = await client.SendAsync(request, token).ConfigureAwait(false);

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    throw new CameraAuthenticationException(camera.Id, response.StatusCode);
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);
    }

    private HttpClient CreateClient(CameraDescriptor camera)
    {
        var client = httpClientFactory.CreateClient(HikvisionRequestOptions.HttpClientName);
        client.BaseAddress = camera.BaseAddress;
        return client;
    }

    private static HttpRequestMessage BuildRequest(
        CameraDescriptor camera,
        HttpMethod method,
        string path,
        HttpContent? content)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Options.Set(HikvisionRequestOptions.CameraIdKey, camera.Id);
        return request;
    }

    /// <summary>
    /// Pipeline được phân vùng theo từng camera. Nếu dùng một pipeline dùng chung,
    /// circuit breaker sẽ gộp trạng thái của mọi camera và một thiết bị chết sẽ chặn tất cả.
    /// </summary>
    private ResiliencePipeline GetPipeline(CameraDescriptor camera) =>
        pipelineRegistry.GetOrAddPipeline(
            $"hikvision:{camera.Id}",
            builder =>
            {
                var settings = camera.Resilience;

                var handledErrors = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>();

                builder
                    .AddConcurrencyLimiter(settings.MaxConcurrentRequests)
                    .AddRetry(new RetryStrategyOptions
                    {
                        MaxRetryAttempts = settings.MaxRetryAttempts,
                        Delay = settings.RetryBaseDelay,
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                        ShouldHandle = handledErrors,
                    })
                    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                    {
                        FailureRatio = settings.CircuitBreakerFailureRatio,
                        MinimumThroughput = settings.CircuitBreakerMinimumThroughput,
                        BreakDuration = settings.CircuitBreakerDuration,
                        ShouldHandle = handledErrors,
                    })
                    .AddTimeout(settings.AttemptTimeout);
            });
}
