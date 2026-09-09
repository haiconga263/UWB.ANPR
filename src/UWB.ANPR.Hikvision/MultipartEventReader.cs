using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;

namespace UWB.ANPR.Hikvision;

/// <summary>Một phần của multipart stream.</summary>
public sealed record MultipartPart(IReadOnlyDictionary<string, string> Headers, ReadOnlyMemory<byte> Body)
{
    public string ContentType => Headers.GetValueOrDefault("Content-Type", "application/octet-stream");
}

/// <summary>
/// Đọc multipart stream không kết thúc (alarm stream), cắt từng part theo boundary.
/// </summary>
public sealed class MultipartEventReader(Stream stream, string boundary, TimeSpan? readTimeout)
{
    private const int BufferSize = 64 * 1024;

    public static string ResolveBoundary(HttpContentHeaders headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var value = headers.ContentType?.Parameters
            .FirstOrDefault(parameter =>
                string.Equals(parameter.Name, "boundary", StringComparison.OrdinalIgnoreCase))?.Value;

        return value?.Trim('"')
            ?? throw new InvalidOperationException("Alarm stream không khai báo multipart boundary.");
    }

    public async IAsyncEnumerable<MultipartPart> ReadPartsAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var delimiter = Encoding.ASCII.GetBytes($"--{boundary}");
        var buffer = new byte[BufferSize];
        var pending = new List<byte>(BufferSize);

        while (!ct.IsCancellationRequested)
        {
            int read;

            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                if (readTimeout is { } timeout)
                {
                    timeoutCts.CancelAfter(timeout);
                }

                read = await stream.ReadAsync(buffer, timeoutCts.Token).ConfigureAwait(false);
            }

            if (read == 0)
            {
                // Kết nối đóng: thoát để AlarmStreamEventSource thực hiện reconnect.
                break;
            }

            pending.AddRange(buffer.AsSpan(0, read));

            while (TryExtractPart(pending, delimiter, out var part))
            {
                yield return part;
            }
        }
    }

    /// <summary>
    /// TODO: hoàn thiện khi có payload alarm stream thật. Cần xử lý ranh giới CRLF,
    /// part kết thúc dạng "--boundary--", và phần ảnh JPEG ở firmware không gửi Content-Length.
    /// </summary>
    private static bool TryExtractPart(
        List<byte> pending,
        ReadOnlySpan<byte> delimiter,
        out MultipartPart part)
    {
        part = null!;
        _ = pending;
        _ = delimiter;
        return false;
    }
}
