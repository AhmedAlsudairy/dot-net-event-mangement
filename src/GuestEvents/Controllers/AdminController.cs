using GuestEvents.Data;
using GuestEvents.Models;
using GuestEvents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Controllers;

[Authorize(Policy = "Admin")]
public class AdminController : AppController
{
    private readonly AppDb _db;
    private readonly SettingsService _settings;
    private readonly WorkflowService _wf;
    private readonly ConnectionConfig _conn;
    public AdminController(AppDb db, SettingsService settings, WorkflowService wf, ConnectionConfig conn) { _db = db; _settings = settings; _wf = wf; _conn = conn; }

    public async Task<IActionResult> Settings() => View(await _settings.AllAsync());

    [HttpPost]
    public async Task<IActionResult> Settings(Dictionary<string, string> values)
    {
        var current = await _settings.AllAsync();
        var errors = new List<string>();
        foreach (var kv in values)
        {
            if (!SettingsService.Defaults.ContainsKey(kv.Key)) continue; // ignore unknown keys
            var v = (kv.Value ?? "").Trim();
            if (kv.Key is "AdminMailbox" or "MediaOfficerEmail" or "AdminExtraTo" or "AdminCc" or "HodEmail" or "VisitApproverEmail" or "VisitBusEmail" or "VisitBusCc" or "ResearchEmail" or "ResearchCc" or "PasswordHelpEmail")
            {
                var list = MailService.Split(v);
                if (list.Any(x => !System.Net.Mail.MailAddress.TryCreate(x, out _)) || (kv.Key is "AdminMailbox" or "MediaOfficerEmail" && list.Count != 1))
                { errors.Add(kv.Key); continue; }
            }
            if (kv.Key is "ReminderWorkingDays" or "CheckInOpenMinutesBefore" or "CheckInCloseMinutesAfterEnd" or "EvaluationOpenDays" or "VisitMinDaysAhead" or "ResearchMaxMb"
                && !(int.TryParse(v, out var n) && n >= 0 && (kv.Key != "ResearchMaxMb" || n is >= 1 and <= 25)))
            { errors.Add(kv.Key); continue; }
            if (kv.Key == "PasswordResetUrl" && v.Length > 0 && !(Uri.TryCreate(v, UriKind.Absolute, out var ru) && (ru.Scheme == "https" || ru.Scheme == "http"))) { errors.Add(kv.Key); continue; }
            if (kv.Key is "ImgPrimary" or "ImgAccent" && !SkiaSharp.SKColor.TryParse(v, out _)) { errors.Add(kv.Key); continue; }
            if (current[kv.Key] == v) continue;
            await _settings.SetAsync(kv.Key, v);
            await _wf.Audit(Username, "SettingChanged", kv.Key.StartsWith("Tpl") ? kv.Key : $"{kv.Key}: {current[kv.Key]} -> {v}");
        }
        var s = await _settings.AllAsync();
        Loc.UseArabicDigits = SettingsService.Bool(s, "UseArabicDigits");
        Loc.DateFmt = s["DateFormat"];
        TempData[errors.Count == 0 ? "Saved" : "Error"] = errors.Count == 0 ? "1" : "Invalid: " + string.Join(", ", errors);
        return RedirectToAction(nameof(Settings));
    }

    // ---- LDAPS / SMTP connections (saved in the database, passwords encrypted) ----
    static readonly string[] LdapKeys = { "Host", "Port", "UseSsl", "StartTls", "SkipCertValidation", "CertThumbprint", "Nested", "BaseDn", "ServiceAccountDn", "ServiceAccountPassword", "GroupStaff", "GroupHod", "GroupMedia", "GroupAdmin", "GroupResearch" };
    static readonly string[] SmtpKeys = { "Enabled", "Host", "Port", "UseStartTls", "User", "Password", "From" };

    public async Task<IActionResult> Connections()
    {
        ViewBag.Ldap = await _conn.LdapAsync();
        ViewBag.Smtp = await _conn.SmtpAsync();
        ViewBag.Mode = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Auth:Mode"] ?? "Ldap";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Connections(Dictionary<string, string> ldap, Dictionary<string, string> smtp)
    {
        var errors = new List<string>();
        ldap = NormalizeLdap(ldap);
        if (!int.TryParse(ldap.GetValueOrDefault("Port"), out var lp) || lp is < 1 or > 65535) errors.Add("LDAP port");
        if (ldap.GetValueOrDefault("CertMode") == "pin")
        {
            var th = ldap.GetValueOrDefault("CertThumbprint") ?? "";
            if (!(th.Length is 40 or 64) || !th.All(Uri.IsHexDigit)) errors.Add("certificate thumbprint (40 hex characters)");
        }
        if (!int.TryParse(smtp.GetValueOrDefault("Port"), out var sp) || sp is < 1 or > 65535) errors.Add("SMTP port");
        if (!System.Net.Mail.MailAddress.TryCreate(smtp.GetValueOrDefault("From") ?? "", out _)) errors.Add("SMTP from address");
        if (errors.Count > 0) { TempData["Error"] = "Invalid: " + string.Join(", ", errors); return RedirectToAction(nameof(Connections)); }

        foreach (var k in LdapKeys) await _conn.SetAsync("Ldap:" + k, (ldap.GetValueOrDefault(k) ?? "").Trim());
        foreach (var k in SmtpKeys) await _conn.SetAsync("Smtp:" + k, (smtp.GetValueOrDefault(k) ?? "").Trim());
        // Never log secrets, only that they were touched.
        var changedSecrets = new[] { ldap.GetValueOrDefault("ServiceAccountPassword"), smtp.GetValueOrDefault("Password") }.Count(x => !string.IsNullOrEmpty(x));
        await _wf.Audit(Username, "ConnectionsChanged", $"LDAP host={ldap.GetValueOrDefault("Host")}:{lp}, SMTP host={smtp.GetValueOrDefault("Host")}:{sp}, enabled={smtp.GetValueOrDefault("Enabled")}, passwords updated={changedSecrets}");
        TempData["Saved"] = "1";
        return RedirectToAction(nameof(Connections));
    }

    [HttpPost]
    public async Task<IActionResult> TestLdap(Dictionary<string, string> ldap, string? testUser, string? testPassword)
    {
        var o = await MergeLdap(NormalizeLdap(ldap));
        var auth = HttpContext.RequestServices.GetRequiredService<LdapAuthService>();
        var (ok, msg) = string.IsNullOrWhiteSpace(testUser) ? await auth.TestConnectionAsync(o) : await auth.TestLoginAsync(o, testUser, testPassword ?? "");
        await _wf.Audit(Username, "TestLdap", ok ? "ok" : "failed");
        return Json(new { ok, message = msg });
    }

    // Who is in one group (with the groups inside it), using the unsaved form values.
    [HttpPost]
    public async Task<IActionResult> GroupTree(Dictionary<string, string> ldap, string group)
    {
        var o = await MergeLdap(NormalizeLdap(ldap));
        var (ok, msg, tree) = await HttpContext.RequestServices.GetRequiredService<LdapAuthService>().GroupTreeAsync(o, (group ?? "").Trim());
        return Json(new { ok, message = msg, tree });
    }

    private sealed record PersonRow(string Name, string? Email, string Sam, List<string> Via);

    // "Who gets each role": every person coming from the role's groups (nested too) plus the people assigned directly.
    [HttpPost]
    public async Task<IActionResult> RolePreview(Dictionary<string, string> ldap)
    {
        var o = await MergeLdap(NormalizeLdap(ldap));
        var auth = HttpContext.RequestServices.GetRequiredService<LdapAuthService>();
        var defs = new[] { (Roles.Staff, o.GroupStaff), (Roles.Hod, o.GroupHod), (Roles.Media, o.GroupMedia), (Roles.Research, o.GroupResearch), (Roles.Admin, o.GroupAdmin) };
        var direct = await _db.RoleAssignments.AsNoTracking().OrderBy(r => r.Username).ToListAsync();
        var roles = new List<object>(); var calls = 0; string? problem = null;
        foreach (var (role, groups) in defs)
        {
            var people = new Dictionary<string, PersonRow>(StringComparer.OrdinalIgnoreCase);
            var glist = new List<object>();
            foreach (var g in MailService.Split(groups).Distinct(StringComparer.OrdinalIgnoreCase).Take(10))
            {
                if (++calls > 30) break;
                var (ok, msg, tree) = await auth.GroupTreeAsync(o, g);
                if (!ok || tree == null) { glist.Add(new { name = g, found = false, people = 0, message = msg }); if (!msg.Contains("was not found")) problem ??= msg; continue; }
                var inGroup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                void Walk(GroupNode nd, string path)
                {
                    var p = path == "" ? nd.Name : path + " > " + nd.Name;
                    foreach (var u in nd.Users)
                    {
                        var key = string.IsNullOrEmpty(u.Sam) ? (u.Email ?? u.Name) : u.Sam;
                        if (!people.TryGetValue(key, out var row)) people[key] = row = new PersonRow(u.Name, u.Email, u.Sam, new List<string>());
                        if (!row.Via.Contains(p)) row.Via.Add(p);
                        inGroup.Add(key);
                    }
                    foreach (var c in nd.Children) if (!c.Cycle) Walk(c, p);
                }
                Walk(tree, "");
                glist.Add(new { name = g, found = true, people = inGroup.Count, truncated = tree.Truncated, message = "" });
            }
            roles.Add(new
            {
                role,
                groups = glist,
                people = people.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                direct = direct.Where(d => d.Role == role).Select(d => new { d.Id, d.Username }).ToList(),
            });
        }
        return Json(new { ok = problem == null, message = problem ?? "", nested = o.Nested, roles });
    }

    // Give a role straight to one person (email address or username), without any group. Same store as the Roles page.
    [HttpPost]
    public async Task<IActionResult> AssignPerson(string who, string role)
    {
        who = (who ?? "").Trim();
        if (who.Contains('\\')) who = who.Split('\\')[1];
        if (who.Length == 0 || who.Length > 100 || !AssignableRoles.Contains(role)) return Json(new { ok = false, message = "Enter an email address or username and choose a role." });
        var lower = who.ToLower();
        if (!await _db.RoleAssignments.AnyAsync(r => r.Username.ToLower() == lower && r.Role == role))
        {
            _db.RoleAssignments.Add(new RoleAssignment { Username = who, Role = role, AssignedBy = Username });
            await _db.SaveChangesAsync();
            await _wf.Audit(Username, "RoleAssigned", $"{who} -> {role}");
        }
        return Json(new { ok = true, message = "" });
    }

    [HttpPost]
    public async Task<IActionResult> UnassignPerson(int id)
    {
        var r = await _db.RoleAssignments.FindAsync(id);
        if (r != null)
        {
            _db.RoleAssignments.Remove(r);
            await _db.SaveChangesAsync();
            await _wf.Audit(Username, "RoleRemoved", $"{r.Username} -> {r.Role}");
        }
        return Json(new { ok = true, message = "" });
    }

    // Lists the groups in the directory (using the unsaved form values) so each role can be pointed at a real group.
    [HttpPost]
    public async Task<IActionResult> FetchGroups(Dictionary<string, string> ldap)
    {
        var o = await MergeLdap(NormalizeLdap(ldap));
        var auth = HttpContext.RequestServices.GetRequiredService<LdapAuthService>();
        var (ok, msg, groups) = await auth.ListGroupsAsync(o);
        await _wf.Audit(Username, "FetchLdapGroups", ok ? $"{groups.Count} groups" : "failed");
        return Json(new { ok, message = msg, groups });
    }

    [HttpPost]
    public async Task<IActionResult> TestSmtp(Dictionary<string, string> smtp, string to)
    {
        if (!System.Net.Mail.MailAddress.TryCreate(to ?? "", out _)) return Json(new { ok = false, message = "Enter a valid test recipient." });
        var cur = await _conn.SmtpAsync();
        var o = new SmtpOpts(true, Pick(smtp, "Host", cur.Host), int.TryParse(smtp.GetValueOrDefault("Port"), out var p) ? p : cur.Port,
            smtp.GetValueOrDefault("UseStartTls") is { } st ? st == "true" : cur.StartTls, (smtp.TryGetValue("User", out var su) ? (su ?? "").Trim() : cur.User),
            string.IsNullOrEmpty(smtp.GetValueOrDefault("Password")) ? cur.Password : smtp["Password"], Pick(smtp, "From", cur.From));
        var (ok, msg) = await HttpContext.RequestServices.GetRequiredService<MailService>().TestAsync(o, to);
        await _wf.Audit(Username, "TestSmtp", ok ? "ok" : "failed");
        return Json(new { ok, message = msg });
    }

    // Test with what is on the form (unsaved); blank password falls back to the saved one.
    private async Task<LdapOpts> MergeLdap(Dictionary<string, string> f)
    {
        var c = await _conn.LdapAsync();
        return new LdapOpts(Pick(f, "Host", c.Host), int.TryParse(f.GetValueOrDefault("Port"), out var p) ? p : c.Port,
            f.GetValueOrDefault("UseSsl") is { } s ? s == "true" : c.UseSsl, f.GetValueOrDefault("SkipCertValidation") is { } k ? k == "true" : c.SkipCert,
            Pick(f, "BaseDn", c.BaseDn), Pick(f, "ServiceAccountDn", c.SvcDn),
            string.IsNullOrEmpty(f.GetValueOrDefault("ServiceAccountPassword")) ? c.SvcPassword : f["ServiceAccountPassword"],
            Pick(f, "GroupStaff", c.GroupStaff), Pick(f, "GroupHod", c.GroupHod), Pick(f, "GroupMedia", c.GroupMedia), Pick(f, "GroupAdmin", c.GroupAdmin),
            f.GetValueOrDefault("StartTls") is { } st ? st == "true" : c.StartTls,
            f.ContainsKey("CertThumbprint") ? (f["CertThumbprint"] ?? "").Trim() : c.CertThumbprint,
            f.GetValueOrDefault("Nested") is { } n ? n == "true" : c.Nested, Pick(f, "GroupResearch", c.GroupResearch));
    }

    // The page sends friendly choices (Encryption: ldaps/starttls/none, CertMode: verify/pin/skip); store them as the plain flags the code uses.
    private static Dictionary<string, string> NormalizeLdap(Dictionary<string, string> f)
    {
        var d = new Dictionary<string, string>(f);
        if (d.TryGetValue("Encryption", out var enc)) { d["UseSsl"] = enc == "ldaps" ? "true" : "false"; d["StartTls"] = enc == "starttls" ? "true" : "false"; }
        if (d.TryGetValue("CertMode", out var cm))
        {
            d["SkipCertValidation"] = cm == "skip" ? "true" : "false";
            d["CertThumbprint"] = cm == "pin" ? (d.GetValueOrDefault("CertThumbprint") ?? "").Replace(":", "").Replace(" ", "").Trim().ToUpperInvariant() : "";
        }
        return d;
    }
    private static string Pick(Dictionary<string, string> f, string k, string fallback) => string.IsNullOrWhiteSpace(f.GetValueOrDefault(k)) ? fallback : f[k].Trim();

    // ---- roles assigned inside the app (for AD users who are not in the Events-* groups) ----
    static readonly string[] AssignableRoles = { Roles.Staff, Roles.Hod, Roles.Media, Roles.Admin, Roles.Research };

    public async Task<IActionResult> RolesPage() => View("Roles", await _db.RoleAssignments.AsNoTracking().OrderBy(r => r.Username).ThenBy(r => r.Role).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> AddRole(string username, string role)
    {
        username = (username ?? "").Trim();
        if (username.Contains('\\')) username = username.Split('\\')[1];
        // an email address is kept whole: people sign in with their email and a role can be given to it directly
        if (username.Length == 0 || username.Length > 100 || !AssignableRoles.Contains(role)) { TempData["Error"] = "Enter an email address (or username) and choose a role."; return RedirectToAction(nameof(RolesPage)); }
        var lower = username.ToLower();
        if (!await _db.RoleAssignments.AnyAsync(r => r.Username.ToLower() == lower && r.Role == role))
        {
            _db.RoleAssignments.Add(new RoleAssignment { Username = username, Role = role, AssignedBy = Username });
            await _db.SaveChangesAsync();
            await _wf.Audit(Username, "RoleAssigned", $"{username} -> {role}");
        }
        return RedirectToAction(nameof(RolesPage));
    }

    [HttpPost]
    public async Task<IActionResult> RemoveRole(int id)
    {
        var r = await _db.RoleAssignments.FindAsync(id);
        if (r == null) return NotFound();
        _db.RoleAssignments.Remove(r);
        await _db.SaveChangesAsync();
        await _wf.Audit(Username, "RoleRemoved", $"{r.Username} -> {r.Role}");
        return RedirectToAction(nameof(RolesPage));
    }

    public async Task<IActionResult> Audit() => View(await _db.AuditLogs.AsNoTracking().OrderByDescending(a => a.At).Take(500).ToListAsync());

    public async Task<IActionResult> Notifications() => View(await _db.Notifications.AsNoTracking().OrderByDescending(a => a.SentAt).Take(500).ToListAsync());
}
