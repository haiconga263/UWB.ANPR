using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly.Registry;

namespace UWB.ANPR.Hikvision;

public static class HikvisionServiceCollectionExtensions
{
    public static IServiceCollection AddHikvisionCameraClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new ResiliencePipelineRegistry<string>());
        services.TryAddTransient<DigestAuthHandler>();
        services.TryAddSingleton<IHikvisionCameraClient, HikvisionCameraClient>();

        services
            .AddHttpClient(HikvisionRequestOptions.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // Connection pooling: tái sử dụng kết nối tới camera, giới hạn tuổi và thời gian nhàn rỗi.
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 8,
                AutomaticDecompression = DecompressionMethods.All,
                ConnectTimeout = TimeSpan.FromSeconds(5),

                // Camera thường dùng self-signed certificate. Chỉ nới lỏng khi đã được phê duyệt
                // và nên pin theo thumbprint thay vì chấp nhận mọi certificate.
            })
            .AddHttpMessageHandler<DigestAuthHandler>();

        return services;
    }
}
