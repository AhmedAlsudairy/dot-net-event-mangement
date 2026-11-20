using System.Text.RegularExpressions;
using GuestEvents.Data;
using GuestEvents.Models;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Services;

/// <summary>
/// Writes the audit / activity log. Every entry is stamped with the category and severity of its action, the person's roles,
/// the IP address, browser, page and a request id, so one request can be followed from start to finish.
/// Logging never breaks the page that called it: a failure to log is swallowed.
/// </summary>
public class AuditService
{
    private readonly AppDb _db;
    private readonly IHttpContextAccessor _h;
    private readonly ILogger<AuditService> _log;
    private static readonly Regex TargetRx = new(@"\b(Event|Visit|Research|Food|KPI)\s+#?(\d+)", RegexOptions.Compiled);

    public AuditService(AppDb db, IHttpContextAccessor h, ILogger<AuditService> log) { _db = db; _h = h; _log = log; }

    /// <summary>Columns added to AuditLogs by the advanced log system (existing databases get them at start-up).</summary>
    public static (string col, string type)[] SchemaColumns(bool sqlServer) => new[]
    {
        ("Category", sqlServer ? "NVARCHAR(30)" : "TEXT"), ("Severity", sqlServer ? "NVARCHAR(10)" : "TEXT"), ("Outcome", sqlServer ? "NVARCHAR(10)" : "TEXT"),
        ("Ip", sqlServer ? "NVARCHAR(60)" : "TEXT"), ("UserAgent", sqlServer ? "NVARCHAR(250)" : "TEXT"), ("Method", sqlServer ? "NVARCHAR(10)" : "TEXT"),
        ("Path", sqlServer ? "NVARCHAR(300)" : "TEXT"), ("Target", sqlServer ? "NVARCHAR(80)" : "TEXT"), ("Roles", sqlServer ? "NVARCHAR(200)" : "TEXT"),
        ("CorrelationId", sqlServer ? "NVARCHAR(40)" : "TEXT"), ("DurationMs", sqlServer ? "INT" : "INTEGER"),
    };

    static string? Cut(string? s, int n) => string.IsNullOrEmpty(s) ? s : (s.Length > n ? s[..n] : s);

    public async Task LogAsync(string user, string action, string? details = null, string? category = null, string? severity = null,
        string? outcome = null, string? target = null, int? durationMs = null)
    {
        try
        {
            var info = AuditCatalog.Describe(action);
            var ctx = _h.HttpContext;
            string? roles = null;
            if (ctx?.User.Identity?.IsAuthenticated == true && string.Equals(ctx.User.Identity.Name, user, StringComparison.OrdinalIgnoreCase))
                roles = string.Join(",", ctx.User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).Distinct());
            if (target == null && details != null && TargetRx.Match(details) is { Success: true } m) target = $"{m.Groups[1].Value}:{m.Groups[2].Value}";
            var failed = outcome == "Failure" || (outcome == null && (action == "LoginFailed" || action == "AccessDenied" || action == "ServerError" || action == "UnhandledException"
                         || (details != null && details.Equals("failed", StringComparison.OrdinalIgnoreCase))));
            _db.AuditLogs.Add(new AuditLog
            {
                User = Cut(user, 200) ?? "", Action = Cut(action, 100) ?? "", Details = Cut(details, 2000),
                Category = category ?? info.Category, Severity = severity ?? info.Severity, Outcome = outcome ?? (failed ? "Failure" : "Success"),
                Ip = Cut(ctx?.Connection.RemoteIpAddress?.ToString(), 60), UserAgent = Cut(ctx?.Request.Headers.UserAgent.ToString(), 250),
                Method = ctx?.Request.Method, Path = Cut(ctx?.Request.Path.Value, 300), Target = Cut(target, 80), Roles = Cut(roles, 200),
                CorrelationId = Cut(ctx?.TraceIdentifier, 40), DurationMs = durationMs,
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex) { _log.LogWarning(ex, "Could not write audit entry {Action}", action); }
    }

    /// <summary>For code that cannot await (Razor Pages handlers).</summary>
    public void Log(string user, string action, string? details = null, string? category = null, string? severity = null)
        => LogAsync(user, action, details, category, severity).GetAwaiter().GetResult();

    /// <summary>Deletes entries older than the retention period. Returns how many were removed.</summary>
    public async Task<int> PurgeAsync(int days, string by = "system")
    {
        if (days <= 0) return 0;
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var n = await _db.AuditLogs.Where(a => a.At < cutoff).ExecuteDeleteAsync();
        if (n > 0) await LogAsync(by, "AuditPurged", $"{n} entries older than {days} days removed");
        return n;
    }
}
