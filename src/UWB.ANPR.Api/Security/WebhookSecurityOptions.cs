using System.ComponentModel.DataAnnotations;

namespace UWB.ANPR.Api.Security;

public sealed class WebhookSecurityOptions
{
    public const string SectionName = "Anpr:WebhookSecurity";

    /// <summary>Dải IP được phép gửi webhook. Rỗng nghĩa là không kiểm tra IP, chỉ nên dùng khi phát triển.</summary>
    public IList<string> AllowedIpRanges { get; init; } = [];

    [Required]
    [MinLength(1)]
    public string SecretHeaderName { get; init; } = "X-Uwb-Anpr-Secret";

    /// <summary>Lấy từ secret manager hoặc biến môi trường, không đặt trong appsettings.json.</summary>
    public string? SharedSecret { get; init; }

    public bool RequireHttps { get; init; } = true;

    /// <summary>Giới hạn kích thước body để một camera lỗi không làm cạn bộ nhớ.</summary>
    [Range(1024, 64 * 1024 * 1024)]
    public long MaxBodyBytes { get; init; } = 8 * 1024 * 1024;
}
