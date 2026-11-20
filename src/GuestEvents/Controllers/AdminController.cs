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

    /// <summary>Upload the app logo (shown in the top bar, login and public pages). Resized to fit 256 px.</summary>
    [HttpPost, RequestSizeLimit(4_000_000)]
    public async Task<IActionResult> UploadLogo(IFormFile? logo)
    {
        if (logo == null) { TempData["Error"] = "Choose an image file first."; return RedirectToAction(nameof(Settings)); }
        string? err;
        await using (var s = logo.OpenReadStream()) err = Brand.Save(s, logo.Length);
        if (err != null) { TempData["Error"] = err; return RedirectToAction(nameof(Settings)); }
        await _wf.Audit(Username, "LogoChanged", logo.FileName.Length > 100 ? logo.FileName[..100] : logo.FileName);
        TempData["Saved"] = "1";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    public async Task<IActionResult> RemoveLogo()
    {
        Brand.Remove();
        await _wf.Audit(Username, "LogoRemoved");
        TempData["Saved"] = "1";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    public async Task<IActionResult> Settings(Dictionary<string, string> values)
    {
        var current = await _settings.AllAsync();
        var errors = new List<string>();
        foreach (var kv in values)
        {
            if (!SettingsService.Defaults.ContainsKey(kv.Key)) continue; // ignore unknown keys
            var v = (kv.Value ?? "").Trim();
            if (kv.Key is "AdminMailbox" or "MediaOfficerEmail" or "AdminExtraTo" or "AdminCc" or "HodEmail" or "DeanEmail" or "VisitApproverEmail" or "VisitBusEmail" or "VisitBusCc" or "ResearchEmail" or "ResearchCc" or "PasswordHelpEmail")
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
        Theme.Apply(s);
        Fonts.Apply(s);
        if (!string.IsNullOrWhiteSpace(s["AppNameEn"])) Loc.BrandEn = s["AppNameEn"].Trim();
        if (!string.IsNullOrWhiteSpace(s["AppNameAr"])) Loc.BrandAr = s["AppNameAr"].Trim();
        TempData[errors.Count == 0 ? "Saved" : "Error"] = errors.Count == 0 ? "1" : "Invalid: " + string.Join(", ", errors);
        return RedirectToAction(nameof(Settings));
    }

    // ---- LDAPS / SMTP connections (saved in the database, passwords encrypted) ----
    static readonly string[] LdapKeys = { "Host", "Port", "UseSsl", "StartTls", "SkipCertValidation", "CertThumbprint", "Nested", "BaseDn", "ServiceAccountDn", "ServiceAccountPassword", "GroupStaff", "GroupHod", "GroupDean", "GroupMedia", "GroupAdmin", "GroupResearch" };
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
        var defs = new[] { (Roles.Staff, o.GroupStaff), (Roles.Hod, o.GroupHod), (Roles.Dean, o.GroupDean), (Roles.Media, o.GroupMedia), (Roles.Research, o.GroupResearch), (Roles.Admin, o.GroupAdmin) };
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
            f.GetValueOrDefault("Nested") is { } n ? n == "true" : c.Nested, Pick(f, "GroupResearch", c.GroupResearch), Pick(f, "GroupDean", c.GroupDean));
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
    static readonly string[] AssignableRoles = { Roles.Staff, Roles.Hod, Roles.Dean, Roles.Media, Roles.Admin, Roles.Research, Roles.Kpi, Roles.KpiManager, Roles.KpiEditor, Roles.KpiOfficer, Roles.KpiViewer };

    /// <summary>Everything about access in one place: each role, who has it, its AD group, what it can do (events side and KPI pages).</summary>
    public async Task<IActionResult> RolesPage()
    {
        var vm = new RolesVm
        {
            Assignments = await _db.RoleAssignments.AsNoTracking().OrderBy(r => r.Username).ThenBy(r => r.Role).ToListAsync(),
            Ldap = await _conn.LdapAsync(),
            Steps = await _db.ApprovalSteps.AsNoTracking().Where(s => s.Enabled).OrderBy(s => s.Order).ToListAsync(),
            Dev = (HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Auth:Mode"] ?? "Ldap") == "Dev",
        };
        return View("Roles", vm);
    }

    [HttpPost]
    public async Task<IActionResult> AddRole(string username, string role, int? entityId)
    {
        username = (username ?? "").Trim();
        if (username.Contains('\\')) username = username.Split('\\')[1];
        // an email address is kept whole: people sign in with their email and a role can be given to it directly
        if (username.Length == 0 || username.Length > 100 || !AssignableRoles.Contains(role)) { TempData["Error"] = "Enter an email address (or username) and choose a role."; return Redirect(Url.Action(nameof(RolesPage)) + "#role-" + role); }
        var lower = username.ToLower();
        // A department KPI officer works for one department: it must be chosen.
        if (role == Roles.KpiOfficer)
        {
            var kdb = HttpContext.RequestServices.GetRequiredService<Kpi.Dashboard.Web.Data.AppDbContext>();
            if (entityId == null || !await kdb.Entities.AnyAsync(e => e.Id == entityId && e.IsActive)) { TempData["Error"] = "Choose the department this KPI officer works for."; return Redirect(Url.Action(nameof(RolesPage)) + "#role-" + role); }
        }
        else entityId = null;
        if (!await _db.RoleAssignments.AnyAsync(r => r.Username.ToLower() == lower && r.Role == role))
        {
            _db.RoleAssignments.Add(new RoleAssignment { Username = username, Role = role, EntityId = entityId, AssignedBy = Username });
            await _db.SaveChangesAsync();
            await _wf.Audit(Username, "RoleAssigned", $"{username} -> {role}");
        }
        TempData["Done"] = $"{username} -> {role}";
        return Redirect(Url.Action(nameof(RolesPage)) + "#role-" + role);
    }

    [HttpPost]
    public async Task<IActionResult> RemoveRole(int id)
    {
        var r = await _db.RoleAssignments.FindAsync(id);
        if (r == null) return NotFound();
        _db.RoleAssignments.Remove(r);
        await _db.SaveChangesAsync();
        await _wf.Audit(Username, "RoleRemoved", $"{r.Username} -> {r.Role}");
        return Redirect(Url.Action(nameof(RolesPage)) + "#role-" + r.Role);
    }

    // ---- approval flow: which steps, in which order, who decides, how many approvals ----
    static readonly string[] StepRoles = { Roles.Hod, Roles.Dean, Roles.Staff, Roles.Media, Roles.Research, Roles.Admin };

    public async Task<IActionResult> ApprovalFlow()
    {
        ViewBag.Waiting = await _db.Events.AsNoTracking().Where(e => e.Status == EventStatus.Submitted && e.CurrentStepId != null).GroupBy(e => e.CurrentStepId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        return View(await _db.ApprovalSteps.AsNoTracking().OrderBy(s => s.Order).ThenBy(s => s.Id).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> SaveStep(int id, string name, string? role, string? approvers, int required, string? notifyEmail)
    {
        var enabled = Request.Form["enabled"].Contains("true");   // checkbox "true" (+ a hidden "false" so an unticked box still arrives)
        name = (name ?? "").Trim(); approvers = (approvers ?? "").Trim(); notifyEmail = (notifyEmail ?? "").Trim();
        var list = MailService.Split(approvers);
        string? error = null;
        if (name.Length == 0 || name.Length > 100) error = "Give the step a name.";
        else if (!string.IsNullOrEmpty(role) && !StepRoles.Contains(role)) error = "Unknown role.";
        else if (list.Count == 0 && string.IsNullOrEmpty(role)) error = "Choose a role or list at least one person: otherwise nobody could approve this step.";
        else if (required < 1 || required > 20) error = "Approvals needed must be between 1 and 20.";
        else if (list.Count > 0 && required > list.Count) error = $"{required} approvals are needed but only {list.Count} people are listed. Add people or lower the number.";
        if (error != null) { TempData["Error"] = error; return RedirectToAction(nameof(ApprovalFlow)); }

        ApprovalStep? step;
        if (id == 0)
        {
            var max = await _db.ApprovalSteps.AnyAsync() ? await _db.ApprovalSteps.MaxAsync(s => s.Order) : 0;
            step = new ApprovalStep { Order = max + 1 };
            _db.ApprovalSteps.Add(step);
        }
        else step = await _db.ApprovalSteps.FindAsync(id);
        if (step == null) return NotFound();
        if (!enabled && step.Enabled && !await CanLeave(step)) return RedirectToAction(nameof(ApprovalFlow));
        step.Name = name; step.Role = string.IsNullOrEmpty(role) ? null : role; step.Approvers = approvers.Length == 0 ? null : approvers;
        step.RequiredApprovals = required; step.NotifyEmail = notifyEmail.Length == 0 ? null : notifyEmail; step.Enabled = enabled || id == 0;
        await _db.SaveChangesAsync();
        await _wf.Audit(Username, id == 0 ? "ApprovalStepAdded" : "ApprovalStepChanged", $"{step.Name}: role {step.Role}, people {step.Approvers}, needs {step.RequiredApprovals}, enabled {step.Enabled}");
        return RedirectToAction(nameof(ApprovalFlow));
    }

    [HttpPost]
    public async Task<IActionResult> MoveStep(int id, int dir)
    {
        var all = await _db.ApprovalSteps.OrderBy(s => s.Order).ThenBy(s => s.Id).ToListAsync();
        var i = all.FindIndex(s => s.Id == id); var j = i + (dir < 0 ? -1 : 1);
        if (i < 0 || j < 0 || j >= all.Count) return RedirectToAction(nameof(ApprovalFlow));
        (all[i], all[j]) = (all[j], all[i]);
        for (int k = 0; k < all.Count; k++) all[k].Order = k + 1;   // renumber so the order is always clean
        await _db.SaveChangesAsync();
        await _wf.Audit(Username, "ApprovalStepMoved", string.Join(" > ", all.Select(s => s.Name)));
        return RedirectToAction(nameof(ApprovalFlow));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteStep(int id)
    {
        var step = await _db.ApprovalSteps.FindAsync(id);
        if (step == null) return NotFound();
        if (!await CanLeave(step)) return RedirectToAction(nameof(ApprovalFlow));
        _db.ApprovalSteps.Remove(step);
        await _db.SaveChangesAsync();
        await _wf.Audit(Username, "ApprovalStepDeleted", step.Name);
        return RedirectToAction(nameof(ApprovalFlow));
    }

    /// <summary>A step can only be switched off or deleted if no request waits at it and it is not the last active step.</summary>
    private async Task<bool> CanLeave(ApprovalStep step)
    {
        var waiting = await _db.Events.CountAsync(e => e.Status == EventStatus.Submitted && e.CurrentStepId == step.Id);
        if (waiting > 0) { TempData["Error"] = $"{waiting} request(s) are waiting at \"{step.Name}\". Wait until they are decided (or ask the approvers to decide), then change this step."; return false; }
        if (step.Enabled && !await _db.ApprovalSteps.AnyAsync(s => s.Enabled && s.Id != step.Id)) { TempData["Error"] = "At least one approval step must stay active."; return false; }
        return true;
    }

    // ---------------- Appearance: colours, light/dark mode, corner style ----------------
    public async Task<IActionResult> Appearance() => View(await _settings.AllAsync());

    /// <summary>The CSS variables for a draft theme, scoped to the preview box (one source of truth: the same code builds the real theme).</summary>
    [HttpGet]
    public IActionResult AppearanceCss(string primary, string secondary, string accent, string bg, string radius = "soft")
    {
        if (!Theme.IsHex(primary) || !Theme.IsHex(secondary) || !Theme.IsHex(accent) || !Theme.IsHex(bg) || !Theme.Radii.Contains(radius)) return BadRequest();
        return Content(Theme.Build("preview", primary.ToUpperInvariant(), secondary.ToUpperInvariant(), accent.ToUpperInvariant(), bg.ToUpperInvariant(), radius, "#themePreview", "#themePreview[data-bs-theme=dark]", scoped: true), "text/css");
    }

    [HttpPost]
    public async Task<IActionResult> Appearance(string primary, string secondary, string accent, string bg, string mode, string radius, string? preset, bool lockTheme, string? fontEn, string? fontAr, int? fontScale)
    {
        primary = (primary ?? "").Trim().ToUpperInvariant(); secondary = (secondary ?? "").Trim().ToUpperInvariant(); accent = (accent ?? "").Trim().ToUpperInvariant(); bg = (bg ?? "").Trim().ToUpperInvariant();
        var err = Theme.Validate(primary, secondary, accent, bg, mode ?? "", radius ?? "");
        if (err != null) { TempData["Error"] = err; return RedirectToAction(nameof(Appearance)); }
        var cur0 = await _settings.AllAsync();
        fontEn = string.IsNullOrEmpty(fontEn) ? cur0["FontEn"] : fontEn; fontAr = string.IsNullOrEmpty(fontAr) ? cur0["FontAr"] : fontAr;
        var scale = fontScale ?? int.Parse(cur0["FontScale"]);
        if (!Fonts.IsValidKey(fontEn) || !Fonts.IsValidKey(fontAr)) { TempData["Error"] = "Choose one of the listed fonts."; return RedirectToAction(nameof(Appearance)); }
        if (!Fonts.Scales.Contains(scale)) { TempData["Error"] = "Choose one of the text sizes."; return RedirectToAction(nameof(Appearance)); }
        var key = Theme.Presets.FirstOrDefault(p => p.Primary == primary && p.Secondary == secondary && p.Accent == accent && p.Bg == bg)?.Key ?? "custom";
        var current = await _settings.AllAsync();
        var values = new Dictionary<string, string> { ["ThemePreset"] = key, ["ThemePrimary"] = primary, ["ThemeSecondary"] = secondary, ["ThemeAccent"] = accent, ["ThemeBg"] = bg, ["ThemeMode"] = mode!, ["ThemeRadius"] = radius!, ["ThemeLock"] = lockTheme ? "true" : "false", ["FontEn"] = fontEn, ["FontAr"] = fontAr, ["FontScale"] = scale.ToString() };
        foreach (var kv in values) if (current[kv.Key] != kv.Value) await _settings.SetAsync(kv.Key, kv.Value);
        Theme.Apply(await _settings.AllAsync()); Fonts.Apply(await _settings.AllAsync());
        await _wf.Audit(Username, "ThemeChanged", $"{key}: primary {primary}, secondary {secondary}, accent {accent}, background {bg}, mode {mode}, corners {radius}, fonts {fontEn} / {fontAr}, text size {scale}%{(lockTheme ? ", locked for everyone" : "")}");
        TempData["Saved"] = "1";
        return RedirectToAction(nameof(Appearance));
    }

    [HttpPost]
    public async Task<IActionResult> AppearanceReset()
    {
        foreach (var k in new[] { "ThemePreset", "ThemePrimary", "ThemeSecondary", "ThemeAccent", "ThemeBg", "ThemeMode", "ThemeRadius", "ThemeLock", "FontEn", "FontAr", "FontScale" }) await _settings.SetAsync(k, SettingsService.Defaults[k]);
        Theme.Apply(await _settings.AllAsync()); Fonts.Apply(await _settings.AllAsync());
        await _wf.Audit(Username, "ThemeChanged", "reset to the default theme");
        TempData["Saved"] = "1";
        return RedirectToAction(nameof(Appearance));
    }

    /// <summary>Import a font file (WOFF2, WOFF, TTF or OTF). It is checked by content, kept in App_Data/fonts and served to every page.</summary>
    [HttpPost, RequestSizeLimit(4_000_000)]
    public async Task<IActionResult> UploadFont(IFormFile? font, string? name)
    {
        if (font == null) return Json(new { ok = false, message = "Choose a font file first." });
        var s = await _settings.AllAsync(); var list = Fonts.ParseList(s["CustomFonts"]);
        string? err; Fonts.Custom? added;
        await using (var st = font.OpenReadStream()) err = Fonts.TryAdd(st, font.Length, font.FileName, name, list.Count, out added);
        if (err != null) return Json(new { ok = false, message = err });
        list.Add(added!);
        await _settings.SetAsync("CustomFonts", Fonts.ToJson(list));
        Fonts.Apply(await _settings.AllAsync());
        await _wf.Audit(Username, "FontImported", $"{added!.Name} ({added.Ext}, {added.Size / 1024} KB)");
        return Json(new { ok = true, id = added.Id, key = "custom:" + added.Id, name = added.Name, face = Fonts.FaceName(added), ext = added.Ext, size = added.Size });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteFont(string id)
    {
        var s = await _settings.AllAsync(); var list = Fonts.ParseList(s["CustomFonts"]);
        var f = list.FirstOrDefault(c => c.Id == id);
        if (f == null) return Json(new { ok = false, message = "That font is not in the list." });
        list.Remove(f); Fonts.DeleteFile(f);
        await _settings.SetAsync("CustomFonts", Fonts.ToJson(list));
        foreach (var k in new[] { "FontEn", "FontAr" }) if (s[k] == "custom:" + id) await _settings.SetAsync(k, "system");   // a deleted font falls back to the default
        Fonts.Apply(await _settings.AllAsync());
        await _wf.Audit(Username, "FontDeleted", f.Name);
        return Json(new { ok = true });
    }

    // ---------------- Audit / activity log ----------------
    const int AuditPageSize = 50;

    private static IQueryable<AuditLog> ApplyFilter(IQueryable<AuditLog> q, AuditFilter f)
    {
        var now = DateTime.UtcNow;
        switch (f.Range)
        {
            case "1h": q = q.Where(a => a.At >= now.AddHours(-1)); break;
            case "24h": q = q.Where(a => a.At >= now.AddHours(-24)); break;
            case "7d": q = q.Where(a => a.At >= now.AddDays(-7)); break;
            case "30d": q = q.Where(a => a.At >= now.AddDays(-30)); break;
            case "custom":
                if (f.From.HasValue) { var from = f.From.Value.Date.ToUniversalTime(); q = q.Where(a => a.At >= from); }
                if (f.To.HasValue) { var to = f.To.Value.Date.AddDays(1).ToUniversalTime(); q = q.Where(a => a.At < to); }
                break;
        }
        if (!string.IsNullOrWhiteSpace(f.Category)) q = q.Where(a => a.Category == f.Category);
        if (!string.IsNullOrWhiteSpace(f.Severity)) q = q.Where(a => a.Severity == f.Severity);
        if (!string.IsNullOrWhiteSpace(f.Outcome)) q = q.Where(a => a.Outcome == f.Outcome);
        if (!string.IsNullOrWhiteSpace(f.User)) { var u = f.User.Trim(); q = q.Where(a => a.User == u); }
        if (!string.IsNullOrWhiteSpace(f.What)) q = q.Where(a => a.Action == f.What);
        if (!string.IsNullOrWhiteSpace(f.Request)) { var r = f.Request.Trim(); q = q.Where(a => a.CorrelationId == r); }
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var t = f.Q.Trim();
            q = q.Where(a => (a.Details != null && a.Details.Contains(t)) || a.User.Contains(t) || a.Action.Contains(t) || (a.Ip != null && a.Ip.Contains(t)) || (a.Path != null && a.Path.Contains(t)) || (a.Target != null && a.Target.Contains(t)));
        }
        return q;
    }

    public async Task<IActionResult> Audit(AuditFilter f)
    {
        f.Range = string.IsNullOrWhiteSpace(f.Range) ? "7d" : f.Range;
        if (f.Page < 1) f.Page = 1;
        var all = _db.AuditLogs.AsNoTracking();
        var q = ApplyFilter(all, f);
        var total = await q.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)AuditPageSize));
        if (f.Page > pages) f.Page = pages;
        var rows = await q.OrderByDescending(a => a.At).ThenByDescending(a => a.Id).Skip((f.Page - 1) * AuditPageSize).Take(AuditPageSize).ToListAsync();

        var since = DateTime.UtcNow.AddHours(-24);
        var last24 = all.Where(a => a.At >= since);
        var vm = new AuditVm
        {
            F = f, Rows = rows, Total = total, Pages = pages,
            Today = await last24.CountAsync(),
            Logins = await last24.CountAsync(a => a.Action == "Login"),
            FailedLogins = await last24.CountAsync(a => a.Action == "LoginFailed"),
            Errors = await last24.CountAsync(a => a.Severity == "Error"),
            Security = await last24.CountAsync(a => a.Severity == "Security"),
            AllEntries = await all.CountAsync(),
            Oldest = await all.OrderBy(a => a.At).Select(a => (DateTime?)a.At).FirstOrDefaultAsync(),
            Retention = SettingsService.Int(await _settings.AllAsync(), "AuditRetentionDays"),
            Users = await all.Select(a => a.User).Distinct().OrderBy(u => u).Take(300).ToListAsync(),
        };
        // 14 days of activity for the little chart (problems = warnings, errors and security entries)
        var d0 = DateTime.UtcNow.Date.AddDays(-13);
        var recent = await all.Where(a => a.At >= d0).Select(a => new { a.At, a.Severity }).Take(100000).ToListAsync();
        vm.Days = Enumerable.Range(0, 14).Select(i => d0.AddDays(i)).Select(d =>
        {
            var day = recent.Where(r => r.At.Date == d.Date).ToList();
            return (d.ToLocalTime().Date, day.Count, day.Count(r => r.Severity != "Info"));
        }).ToList();
        // possible guessing: many failed sign-ins in the last hour from one address or against one account
        var hour = DateTime.UtcNow.AddHours(-1);
        var fails = await all.Where(a => a.Action == "LoginFailed" && a.At >= hour).Select(a => new { a.Ip, a.User }).ToListAsync();
        vm.Suspicious = fails.Where(x => x.Ip != null).GroupBy(x => x.Ip!).Where(g => g.Count() >= 5).Select(g => ($"IP {g.Key}", g.Count()))
            .Concat(fails.GroupBy(x => x.User).Where(g => g.Count() >= 5).Select(g => ($"account {g.Key}", g.Count()))).ToList();
        return View(vm);
    }

    public async Task<IActionResult> AuditExport(AuditFilter f, string format = "csv")
    {
        f.Range = string.IsNullOrWhiteSpace(f.Range) ? "7d" : f.Range;
        var list = await ApplyFilter(_db.AuditLogs.AsNoTracking(), f).OrderByDescending(a => a.At).ThenByDescending(a => a.Id).Take(50000).ToListAsync();
        await _wf.Audit(Username, "AuditExport", $"{list.Count} entries as {format}");
        string[] h = { "Time", "Severity", "Category", "Outcome", "User", "Roles", "Action", "Target", "Details", "IP", "Method", "Path", "Request id", "Browser", "Duration ms" };
        static string Safe(string? s) => string.IsNullOrEmpty(s) ? "" : "=+-@".Contains(s[0]) ? "'" + s : s;
        if (format == "xlsx")
        {
            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.AddWorksheet("Audit log");
            for (int i = 0; i < h.Length; i++) ws.Cell(1, i + 1).SetValue(h[i]).Style.Font.Bold = true;
            int r = 2;
            foreach (var a in list)
            {
                ws.Cell(r, 1).SetValue(a.At.ToLocalTime()); ws.Cell(r, 2).SetValue(a.Severity); ws.Cell(r, 3).SetValue(a.Category); ws.Cell(r, 4).SetValue(a.Outcome);
                ws.Cell(r, 5).SetValue(Safe(a.User)); ws.Cell(r, 6).SetValue(Safe(a.Roles)); ws.Cell(r, 7).SetValue(a.Action); ws.Cell(r, 8).SetValue(Safe(a.Target)); ws.Cell(r, 9).SetValue(Safe(a.Details));
                ws.Cell(r, 10).SetValue(Safe(a.Ip)); ws.Cell(r, 11).SetValue(a.Method ?? ""); ws.Cell(r, 12).SetValue(Safe(a.Path)); ws.Cell(r, 13).SetValue(a.CorrelationId ?? ""); ws.Cell(r, 14).SetValue(Safe(a.UserAgent));
                if (a.DurationMs.HasValue) ws.Cell(r, 15).SetValue(a.DurationMs.Value); r++;
            }
            ws.Columns().AdjustToContents(60); ws.SheetView.FreezeRows(1);
            using var ms = new MemoryStream(); wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"audit-log-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
        }
        static string Csv(string? s) => "\"" + Safe(s).Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        var sb = new System.Text.StringBuilder("\uFEFF" + string.Join(",", h) + "\r\n");
        foreach (var a in list)
            sb.Append(string.Join(",", new[] { a.At.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), a.Severity, a.Category, a.Outcome, a.User, a.Roles, a.Action, a.Target, a.Details, a.Ip, a.Method, a.Path, a.CorrelationId, a.UserAgent, a.DurationMs?.ToString() }.Select(Csv))).Append("\r\n");
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"audit-log-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    [HttpPost]
    public async Task<IActionResult> AuditPurge(int days)
    {
        if (days < 1 || days > 3650) { TempData["Error"] = "Enter a number of days between 1 and 3650."; return RedirectToAction(nameof(Audit)); }
        var n = await HttpContext.RequestServices.GetRequiredService<AuditService>().PurgeAsync(days, Username);
        TempData["Saved"] = $"{n} old entries deleted.";
        return RedirectToAction(nameof(Audit));
    }

    public async Task<IActionResult> Notifications() => View(await _db.Notifications.AsNoTracking().OrderByDescending(a => a.SentAt).Take(500).ToListAsync());
}

public class RolesVm
{
    public List<RoleAssignment> Assignments { get; set; } = new();
    public LdapOpts Ldap { get; set; } = default!;
    public List<ApprovalStep> Steps { get; set; } = new();
    public bool Dev { get; set; }
}

public class AuditFilter
{
    public string? Range { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Category { get; set; }
    public string? Severity { get; set; }
    public string? Outcome { get; set; }
    public string? User { get; set; }
    /// <summary>The action to show (named "What" because MVC would bind a property called Action from the route).</summary>
    public string? What { get; set; }
    public string? Request { get; set; }
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    /// <summary>Query string for links that keep the current filter.</summary>
    public Dictionary<string, string?> ToRoute(int? page = null) => new()
    {
        ["Range"] = Range, ["From"] = From?.ToString("yyyy-MM-dd"), ["To"] = To?.ToString("yyyy-MM-dd"), ["Category"] = Category, ["Severity"] = Severity,
        ["Outcome"] = Outcome, ["User"] = User, ["What"] = What, ["Request"] = Request, ["Q"] = Q, ["Page"] = (page ?? Page).ToString(),
    };
}

public class AuditVm
{
    public AuditFilter F { get; set; } = new();
    public List<AuditLog> Rows { get; set; } = new();
    public int Total { get; set; }
    public int Pages { get; set; }
    public int Today { get; set; }
    public int Logins { get; set; }
    public int FailedLogins { get; set; }
    public int Errors { get; set; }
    public int Security { get; set; }
    public int AllEntries { get; set; }
    public DateTime? Oldest { get; set; }
    public int Retention { get; set; }
    public List<string> Users { get; set; } = new();
    public List<(DateTime day, int total, int problems)> Days { get; set; } = new();
    public List<(string who, int n)> Suspicious { get; set; } = new();
}
