using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using UWB.ANPR.Abstractions.Ports;

namespace UWB.ANPR.Hikvision;

/// <summary>
/// Xử lý HTTP Digest theo từng camera: gửi request, nếu nhận 401 kèm challenge thì
/// tính response và thử lại đúng một lần.
/// </summary>
internal sealed class DigestAuthHandler(ICameraCredentialResolver credentials) : DelegatingHandler
{
    private static readonly char[] ParameterSeparator = [','];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Buffer content trước vì có thể phải gửi lại request sau khi nhận challenge.
        byte[]? body = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is not HttpStatusCode.Unauthorized)
        {
            return response;
        }

        if (!request.Options.TryGetValue(HikvisionRequestOptions.CameraIdKey, out var cameraId))
        {
            return response;
        }

        var challenge = response.Headers.WwwAuthenticate
            .FirstOrDefault(header => string.Equals(header.Scheme, "Digest", StringComparison.OrdinalIgnoreCase));

        if (challenge?.Parameter is null)
        {
            return response;
        }

        var credential = await credentials.ResolveAsync(cameraId, cancellationToken).ConfigureAwait(false);
        if (credential is null)
        {
            return response;
        }

        var parameters = ParseChallenge(challenge.Parameter);
        var retry = CloneRequest(request, body);

        retry.Headers.Authorization = new AuthenticationHeaderValue(
            "Digest",
            BuildAuthorizationParameter(parameters, credential, retry.Method.Method, retry.RequestUri!));

        response.Dispose();
        return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }

    internal static Dictionary<string, string> ParseChallenge(string parameter)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in parameter.Split(ParameterSeparator, StringSplitOptions.TrimEntries))
        {
            var index = part.IndexOf('=', StringComparison.Ordinal);
            if (index <= 0)
            {
                continue;
            }

            var key = part[..index].Trim();
            var value = part[(index + 1)..].Trim().Trim('"');
            result[key] = value;
        }

        return result;
    }

    internal static string BuildAuthorizationParameter(
        Dictionary<string, string> challenge,
        CameraCredential credential,
        string method,
        Uri uri)
    {
        var realm = challenge.GetValueOrDefault("realm", string.Empty);
        var nonce = challenge.GetValueOrDefault("nonce", string.Empty);
        var algorithm = challenge.GetValueOrDefault("algorithm", "MD5");
        challenge.TryGetValue("opaque", out var opaque);
        challenge.TryGetValue("qop", out var qopRaw);

        var qop = qopRaw?
            .Split(ParameterSeparator, StringSplitOptions.TrimEntries)
            .FirstOrDefault(value => string.Equals(value, "auth", StringComparison.OrdinalIgnoreCase));

        var path = uri.IsAbsoluteUri ? uri.PathAndQuery : uri.OriginalString;
        var clientNonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        const string NonceCount = "00000001";

        var ha1 = Md5Hex($"{credential.Username}:{realm}:{credential.Password}");
        var ha2 = Md5Hex($"{method}:{path}");

        var responseHash = qop is null
            ? Md5Hex($"{ha1}:{nonce}:{ha2}")
            : Md5Hex($"{ha1}:{nonce}:{NonceCount}:{clientNonce}:{qop}:{ha2}");

        var builder = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"username=\"{credential.Username}\"")
            .Append(CultureInfo.InvariantCulture, $", realm=\"{realm}\"")
            .Append(CultureInfo.InvariantCulture, $", nonce=\"{nonce}\"")
            .Append(CultureInfo.InvariantCulture, $", uri=\"{path}\"")
            .Append(CultureInfo.InvariantCulture, $", algorithm={algorithm}")
            .Append(CultureInfo.InvariantCulture, $", response=\"{responseHash}\"");

        if (qop is not null)
        {
            builder.Append(CultureInfo.InvariantCulture, $", qop={qop}")
                .Append(CultureInfo.InvariantCulture, $", nc={NonceCount}")
                .Append(CultureInfo.InvariantCulture, $", cnonce=\"{clientNonce}\"");
        }

        if (opaque is not null)
        {
            builder.Append(CultureInfo.InvariantCulture, $", opaque=\"{opaque}\"");
        }

        return builder.ToString();
    }

    // MD5 là thuật toán bắt buộc theo đặc tả HTTP Digest của thiết bị,
    // không phải lựa chọn bảo mật của hệ thống này.
#pragma warning disable CA5351
    private static string Md5Hex(string value) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
#pragma warning restore CA5351

    private static HttpRequestMessage CloneRequest(HttpRequestMessage source, byte[]? body)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri)
        {
            Version = source.Version,
            VersionPolicy = source.VersionPolicy,
        };

        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in (IDictionary<string, object?>)source.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        if (body is not null && source.Content is not null)
        {
            clone.Content = new ByteArrayContent(body);
            foreach (var header in source.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
