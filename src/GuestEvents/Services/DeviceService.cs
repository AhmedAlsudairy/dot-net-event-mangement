using System.Security.Cryptography;
using System.Text;

namespace GuestEvents.Services;

/// <summary>
/// A random per-browser id lives in a cookie. Attendance and Evaluation hash it with DIFFERENT keys,
/// so the two stored hashes cannot be matched to each other.
/// </summary>
public class DeviceService
{
    private readonly byte[] _keyAttendance, _keyEvaluation;
    public const string CookieName = "ge_dev";
    public DeviceService(IConfiguration cfg)
    {
        var master = cfg["Security:HashSecret"] ?? "change-me-in-production";
        _keyAttendance = Encoding.UTF8.GetBytes(master + "|attendance");
        _keyEvaluation = Encoding.UTF8.GetBytes(master + "|evaluation");
    }
    public string DeviceId(HttpContext ctx)
    {
        if (ctx.Request.Cookies.TryGetValue(CookieName, out var id) && id.Length >= 16) return id;
        id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        ctx.Response.Cookies.Append(CookieName, id, new CookieOptions { HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365) });
        return id;
    }
    public string AttendanceHash(string deviceId, int eventId, DateTime day) => H(_keyAttendance, deviceId + ":" + eventId + ":" + day.ToString("yyyyMMdd"));
    public string EvaluationHash(string deviceId, int eventId) => H(_keyEvaluation, deviceId + ":" + eventId);
    private static string H(byte[] k, string v) => Convert.ToHexString(HMACSHA256.HashData(k, Encoding.UTF8.GetBytes(v)));
}
