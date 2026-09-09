using UWB.ANPR.Abstractions.Ports;
using UWB.ANPR.Hikvision;
using Xunit;

namespace UWB.ANPR.UnitTests;

public sealed class DigestAuthHandlerTests
{
    [Fact]
    public void Parse_challenge_doc_dung_cac_tham_so()
    {
        const string Challenge =
            "realm=\"IP Camera\", qop=\"auth\", nonce=\"abc123\", opaque=\"xyz\", algorithm=MD5";

        var parsed = DigestAuthHandler.ParseChallenge(Challenge);

        Assert.Equal("IP Camera", parsed["realm"]);
        Assert.Equal("auth", parsed["qop"]);
        Assert.Equal("abc123", parsed["nonce"]);
        Assert.Equal("xyz", parsed["opaque"]);
        Assert.Equal("MD5", parsed["algorithm"]);
    }

    [Fact]
    public void Authorization_chua_du_thanh_phan_bat_buoc_khi_co_qop()
    {
        var challenge = DigestAuthHandler.ParseChallenge(
            "realm=\"IP Camera\", qop=\"auth\", nonce=\"abc123\"");

        var parameter = DigestAuthHandler.BuildAuthorizationParameter(
            challenge,
            new CameraCredential("admin", "secret"),
            "GET",
            new Uri("https://10.0.0.21/ISAPI/System/deviceInfo"));

        Assert.Contains("username=\"admin\"", parameter, StringComparison.Ordinal);
        Assert.Contains("uri=\"/ISAPI/System/deviceInfo\"", parameter, StringComparison.Ordinal);
        Assert.Contains("qop=auth", parameter, StringComparison.Ordinal);
        Assert.Contains("nc=00000001", parameter, StringComparison.Ordinal);
        Assert.Contains("cnonce=\"", parameter, StringComparison.Ordinal);

        // Không bao giờ được để password xuất hiện trong header.
        Assert.DoesNotContain("secret", parameter, StringComparison.Ordinal);
    }

    [Fact]
    public void Authorization_bo_qop_khi_camera_khong_khai_bao()
    {
        var challenge = DigestAuthHandler.ParseChallenge("realm=\"IP Camera\", nonce=\"abc123\"");

        var parameter = DigestAuthHandler.BuildAuthorizationParameter(
            challenge,
            new CameraCredential("admin", "secret"),
            "GET",
            new Uri("https://10.0.0.21/ISAPI/System/deviceInfo"));

        Assert.DoesNotContain("qop=", parameter, StringComparison.Ordinal);
        Assert.DoesNotContain("cnonce=", parameter, StringComparison.Ordinal);
    }

    [Fact]
    public void Cung_challenge_va_credential_cho_cung_response_hash()
    {
        var challenge = DigestAuthHandler.ParseChallenge("realm=\"r\", nonce=\"n\"");
        var credential = new CameraCredential("admin", "secret");
        var uri = new Uri("https://10.0.0.21/ISAPI/System/deviceInfo");

        var first = DigestAuthHandler.BuildAuthorizationParameter(challenge, credential, "GET", uri);
        var second = DigestAuthHandler.BuildAuthorizationParameter(challenge, credential, "GET", uri);

        // Không có qop nên không có cnonce ngẫu nhiên, kết quả phải xác định.
        Assert.Equal(first, second);
    }
}
