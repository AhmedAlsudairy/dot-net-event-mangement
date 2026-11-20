using System.Diagnostics;

namespace GuestEvents.Services;

/// <summary>
/// Records what goes wrong while serving a page: unhandled exceptions and server errors (500+), blocked API calls (403) and very slow requests.
/// Normal page views are not logged here (the volume would bury the useful entries); the actions people take are logged where they happen.
/// </summary>
public class AuditMiddleware
{
    private readonly RequestDelegate _next;
    public const int SlowMs = 5000;
    public AuditMiddleware(RequestDelegate next) => _next = next;

    private static bool IsAsset(PathString p) =>
        p.StartsWithSegments("/css") || p.StartsWithSegments("/js") || p.StartsWithSegments("/lib") || p.StartsWithSegments("/kpi") || p.StartsWithSegments("/brand") || p.Value == "/favicon.ico";

    public async Task InvokeAsync(HttpContext ctx)
    {
        var sw = Stopwatch.StartNew();
        try { await _next(ctx); }
        catch (Exception ex)
        {
            await Write(ctx, "UnhandledException", $"{ex.GetType().Name}: {ex.Message}\n{string.Join("\n", (ex.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim()))}", (int)sw.ElapsedMilliseconds);
            throw;
        }
        sw.Stop();
        if (IsAsset(ctx.Request.Path)) return;
        var status = ctx.Response.StatusCode;
        if (status >= 500) await Write(ctx, "ServerError", $"HTTP {status} on {ctx.Request.Method} {ctx.Request.Path}", (int)sw.ElapsedMilliseconds);
        else if (status == 403 || status == 401) await Write(ctx, "AccessDenied", $"HTTP {status} on {ctx.Request.Method} {ctx.Request.Path}", (int)sw.ElapsedMilliseconds);
        else if (sw.ElapsedMilliseconds >= SlowMs) await Write(ctx, "SlowRequest", $"{ctx.Request.Method} {ctx.Request.Path} took {sw.ElapsedMilliseconds} ms", (int)sw.ElapsedMilliseconds);
    }

    private static async Task Write(HttpContext ctx, string action, string details, int ms)
    {
        try
        {
            // a fresh scope: the request's own database context may be in a failed state
            using var scope = ctx.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var who = ctx.User.Identity?.IsAuthenticated == true ? ctx.User.Identity.Name ?? "?" : "(anonymous)";
            await scope.ServiceProvider.GetRequiredService<AuditService>().LogAsync(who, action, details, durationMs: ms);
        }
        catch { /* never let logging break the response */ }
    }
}
