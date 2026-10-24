using System.Security.Cryptography;
using System.Text;
using QRCoder;

namespace GuestEvents.Services;

public class QrService
{
    private readonly byte[] _key;
    public QrService(IConfiguration cfg) => _key = Encoding.UTF8.GetBytes((cfg["Security:HashSecret"] ?? "change-me-in-production") + "|rot");

    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public byte[] Png(string url, int pixelsPerModule = 12)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
    }

    // Rotating mode: URL token = base token + "." + 60s-window MAC. Current and previous window are accepted.
    public string Rotating(string baseToken, DateTime utcNow) => baseToken + "." + Mac(baseToken, Window(utcNow));
    public bool ValidRotating(string baseToken, string mac, DateTime utcNow) =>
        mac == Mac(baseToken, Window(utcNow)) || mac == Mac(baseToken, Window(utcNow) - 1);
    private static long Window(DateTime t) => new DateTimeOffset(DateTime.SpecifyKind(t, DateTimeKind.Utc)).ToUnixTimeSeconds() / 60;
    private string Mac(string t, long w) => Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(t + w)))[..12];
}
