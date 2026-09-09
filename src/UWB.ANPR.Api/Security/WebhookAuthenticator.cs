using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace UWB.ANPR.Api.Security;

public interface IWebhookAuthenticator
{
    bool IsTrusted(HttpRequest request);
}

internal sealed class WebhookAuthenticator(IOptions<WebhookSecurityOptions> options) : IWebhookAuthenticator
{
    public bool IsTrusted(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = options.Value;

        if (settings.RequireHttps && !request.IsHttps)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(settings.SharedSecret) && !HasValidSecret(request, settings))
        {
            return false;
        }

        if (settings.AllowedIpRanges.Count > 0)
        {
            var remote = request.HttpContext.Connection.RemoteIpAddress;
            if (remote is null || !IsAllowed(remote, settings.AllowedIpRanges))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasValidSecret(HttpRequest request, WebhookSecurityOptions settings)
    {
        var provided = request.Headers[settings.SecretHeaderName].ToString();

        // So sánh trong thời gian cố định để không rò rỉ thông tin qua thời gian phản hồi.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(settings.SharedSecret!));
    }

    private static bool IsAllowed(IPAddress address, IEnumerable<string> ranges) =>
        ranges.Any(range => IPNetwork.TryParse(range, out var network) && network.Contains(address));
}
