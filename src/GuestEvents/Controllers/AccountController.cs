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
    private readonly SettingsService _settings;
    private readonly MailService _mail;
    public AccountController(IAuthService auth, WorkflowService wf, GuestEvents.Data.AppDb db, IConfiguration cfg, SettingsService settings, MailService mail)
    { _auth = auth; _wf = wf; _db = db; _cfg = cfg; _settings = settings; _mail = mail; }

    // ---- Forgot password. Passwords live in Active Directory, so the app does not reset them: it points to the company reset page and lets people ask IT.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> HelpAsked = new();

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Forgot()
    {
        ViewBag.ResetUrl = (await _settings.AllAsync())["PasswordResetUrl"];
        return View();
    }

    [AllowAnonymous, HttpPost, Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")]
    public async Task<IActionResult> Forgot(string? email, string? note)
    {
        var s = await _settings.AllAsync();
        ViewBag.ResetUrl = s["PasswordResetUrl"];
        email = (email ?? "").Trim();
        if (email.Length > 200 || !System.Net.Mail.MailAddress.TryCreate(email, out _)) { ViewBag.Error = true; return View(); }
        note = (note ?? "").Trim(); if (note.Length > 500) note = note[..500];
        // one request per address every 10 minutes; the answer is always the same so nobody can learn which addresses exist
        var key = email.ToLowerInvariant();
        if (HelpAsked.Count > 5000) HelpAsked.Clear();
        var fresh = !HelpAsked.TryGetValue(key, out var last) || DateTime.UtcNow - last > TimeSpan.FromMinutes(10);
        if (fresh)
        {
            HelpAsked[key] = DateTime.UtcNow;
            var to = MailService.Split(s["PasswordHelpEmail"]);
            if (to.Count == 0) to = new List<string> { s["AdminMailbox"] };
            await _mail.SendAsync(null, to, null, $"Password help requested: {email}",
                $"Someone asked for help with their password.\nEmail: {email}\nTime: {DateTime.Now:yyyy-MM-dd HH:mm}\nNote: {note}\n\nPlease reset it in Active Directory and tell the person. Nothing was changed by the app.");
            await _wf.Audit(email.Length > 100 ? email[..100] : email, "PasswordHelpRequested");
        }
        ViewBag.Sent = true;
        return View();
    }

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
            // A role can be given to the username or to the email address (the email is what people sign in with).
            var lower = u.Username.ToLower();
            var mail = (u.Email ?? "").Trim().ToLower();
            var extra = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                _db.RoleAssignments.Where(r => r.Username.ToLower() == lower || (mail != "" && r.Username.ToLower() == mail)).Select(r => r.Role));
            var boot = (_cfg["App:BootstrapAdmins"] ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (boot.Contains(u.Username, StringComparer.OrdinalIgnoreCase) || (mail != "" && boot.Contains(mail, StringComparer.OrdinalIgnoreCase))) extra.Add(GuestEvents.Models.Roles.Admin);
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
