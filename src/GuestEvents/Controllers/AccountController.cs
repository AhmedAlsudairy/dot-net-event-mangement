using System.Security.Claims;
using GuestEvents.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuestEvents.Controllers;

public class AppController : Controller
{
    protected string Username => User.Identity?.Name ?? "";
    protected string DisplayName => User.FindFirstValue("display") ?? Username;
    protected string? UserEmail => User.FindFirstValue(ClaimTypes.Email);
    protected string? Department => User.FindFirstValue("department");
    protected bool IsAdmin => User.IsInRole(Models.Roles.Admin);
}

public class AccountController : AppController
{
    private readonly IAuthService _auth;
    private readonly WorkflowService _wf;
    private readonly GuestEvents.Data.AppDb _db;
    private readonly IConfiguration _cfg;
    public AccountController(IAuthService auth, WorkflowService wf, GuestEvents.Data.AppDb db, IConfiguration cfg) { _auth = auth; _wf = wf; _db = db; _cfg = cfg; }

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl) { ViewBag.ReturnUrl = returnUrl; ViewBag.Sso = _cfg.GetValue("Auth:Sso", false); return View(); }

    // After 5 wrong passwords for one name the app stops asking AD for 10 minutes (protects the AD account from being locked out by guessing).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int n, DateTime since)> Fails = new();
    private static string Key(string u) => (u ?? "").Trim().ToLowerInvariant();
    private static bool Blocked(string u) => Fails.TryGetValue(Key(u), out var f) && f.n >= 5 && DateTime.UtcNow - f.since < TimeSpan.FromMinutes(10);
    private static void CountFail(string u)
    {
        if (Fails.Count > 10000) Fails.Clear();
        Fails.AddOrUpdate(Key(u), (1, DateTime.UtcNow), (_, f) => DateTime.UtcNow - f.since > TimeSpan.FromMinutes(10) ? (1, DateTime.UtcNow) : (f.n + 1, f.since));
    }

    private static string ReasonKey(AuthFail f) => f switch
    {
        AuthFail.Disabled => "LoginDisabled", AuthFail.Locked => "LoginLocked", AuthFail.PasswordExpired => "LoginPwExpired",
        AuthFail.AccountExpired => "LoginAcctExpired", AuthFail.Unavailable => "LoginUnavailable", AuthFail.TooMany => "LoginTooMany",
        AuthFail.NoRole => "LoginNoRole", _ => "BadLogin",
    };

    [AllowAnonymous, HttpPost, Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl)
    {
        username = (username ?? "").Trim();
        if (Blocked(username)) return await Failed(username, AuthFail.TooMany, returnUrl, null);
        var res = await _auth.AuthenticateAsync(username, password ?? "");
        if (res.User == null && res.Fail == AuthFail.Invalid) CountFail(username);
        return await Complete(res, username, returnUrl);
    }

    /// <summary>Windows single sign-on: the browser proves the Windows identity (Kerberos/NTLM), the app only looks the person up in AD.</summary>
    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Sso(string? returnUrl)
    {
        if (!_cfg.GetValue("Auth:Sso", false)) return NotFound();
        var r = await HttpContext.AuthenticateAsync("Negotiate");
        if (!r.Succeeded || string.IsNullOrEmpty(r.Principal?.Identity?.Name)) return Challenge("Negotiate");
        var win = r.Principal!.Identity!.Name!;
        return await Complete(await _auth.LookupAsync(win), win, returnUrl);
    }

    private async Task<IActionResult> Complete(AuthResult res, string who, string? returnUrl)
    {
        var u = res.User;
        if (u != null)
        {
            // Roles = AD groups + roles assigned in the app (Admin > Roles) + bootstrap admins from configuration.
            var lower = u.Username.ToLower();
            var extra = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                _db.RoleAssignments.Where(r => r.Username.ToLower() == lower).Select(r => r.Role));
            var boot = (_cfg["App:BootstrapAdmins"] ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (boot.Contains(u.Username, StringComparer.OrdinalIgnoreCase)) extra.Add(GuestEvents.Models.Roles.Admin);
            u = u with { Roles = u.Roles.Concat(extra).Distinct().ToList() };
            if (u.Roles.Count == 0) return await Failed(who, AuthFail.NoRole, returnUrl, u.Username);
        }
        else return await Failed(who, res.Fail, returnUrl, null);

        Fails.TryRemove(Key(who), out _);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, u.Username), new("display", u.DisplayName),
            new(ClaimTypes.Email, u.Email ?? ""), new("department", u.Department ?? ""),
        };
        claims.AddRange(u.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        await _wf.Audit(u.Username, "Login");
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }

    private async Task<IActionResult> Failed(string who, AuthFail fail, string? returnUrl, string? known)
    {
        await _wf.Audit(known ?? (who.Length > 100 ? who[..100] : who), "LoginFailed", fail.ToString());
        ViewBag.ErrorKey = ReasonKey(fail); ViewBag.ReturnUrl = returnUrl; ViewBag.Sso = _cfg.GetValue("Auth:Sso", false);
        return View("Login");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult Denied() => View();

    // Language toggle, remembered in a cookie (also used by the public pages).
    [AllowAnonymous, HttpPost("/lang")]
    public IActionResult Lang(string lang, string? returnUrl)
    {
        Response.Cookies.Append("lang", lang == "ar" ? "ar" : "en", new CookieOptions { MaxAge = TimeSpan.FromDays(365), HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax });
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
