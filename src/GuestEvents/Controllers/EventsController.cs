using GuestEvents.Data;
using GuestEvents.Models;
using GuestEvents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Controllers;

[Authorize(Policy = "Viewer")]
public class EventsController : AppController
{
    private readonly AppDb _db;
    private readonly WorkflowService _wf;
    private readonly QrService _qr;
    private readonly SettingsService _settings;
    private readonly ConnectionConfig _conn;
    public EventsController(AppDb db, WorkflowService wf, QrService qr, SettingsService settings, ConnectionConfig conn) { _db = db; _wf = wf; _qr = qr; _settings = settings; _conn = conn; }

    static readonly string[] ImgExt = { ".png", ".jpg", ".jpeg" };

    public async Task<IActionResult> Index()
    {
        if (User.IsInRole(Roles.Research) && !IsAdmin && !User.IsInRole(Roles.Staff) && !User.IsInRole(Roles.Hod) && !User.IsInRole(Roles.Media))
            return RedirectToAction("Index", "Research"); // the research in-charge lands on the archive
        var q = _db.Events.AsNoTracking().AsQueryable();
        if (User.IsInRole(Roles.Media) && !IsAdmin && !User.IsInRole(Roles.Hod) && !User.IsInRole(Roles.Staff))
            q = q.Where(e => e.Status >= EventStatus.Approved && e.Status != EventStatus.Rejected);
        else if (!IsAdmin && !User.IsInRole(Roles.Hod))
            q = q.Where(e => e.RequesterUsername == Username);
        if (IsAdmin) ViewBag.Setup = await SetupChecklist();
        return View(await q.OrderByDescending(e => e.Date).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var e = await Load(id);
        if (e == null || !CanView(e)) return NotFound();
        return View(e);
    }

    [Authorize(Policy = "Requester")]
    public IActionResult Create() => View("Edit", new EventRequest { Date = DateTime.Today.AddDays(7) });

    [Authorize(Policy = "Requester")]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await _db.Events.FindAsync(id);
        if (e == null || !CanEdit(e)) return NotFound();
        if (e.Status == EventStatus.Rejected)
            ViewBag.LastRejection = await _db.Approvals.Where(a => a.EventRequestId == id && !a.Approved).OrderByDescending(a => a.DecidedAt).FirstOrDefaultAsync();
        return View(e);
    }

    [Authorize(Policy = "Requester"), HttpPost]
    public async Task<IActionResult> Save(EventRequest m, IFormFile? logo, string submitAction)
    {
        EventRequest e;
        if (m.Id == 0)
        {
            e = new EventRequest { RequesterUsername = Username, RequesterName = DisplayName, RequesterDepartment = Department, RequesterEmail = UserEmail };
            _db.Events.Add(e);
        }
        else
        {
            e = (await _db.Events.FindAsync(m.Id))!;
            if (e == null || !CanEdit(e)) return NotFound();
        }
        ModelState.Remove("logo"); ModelState.Remove("submitAction");
        if (m.EndTime <= m.StartTime) ModelState.AddModelError(nameof(m.EndTime), "End time must be after start time (the times apply to every day).");
        var lastDay = (m.EndDate ?? m.Date).Date;
        if (lastDay < m.Date.Date) ModelState.AddModelError(nameof(m.EndDate), "The last day cannot be before the first day.");
        else if ((lastDay - m.Date.Date).TotalDays > 60) ModelState.AddModelError(nameof(m.EndDate), "An event can last at most 60 days.");
        if (logo is { Length: > 0 })
        {
            var ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
            if (!ImgExt.Contains(ext) || logo.Length > 2_000_000 || !LooksLikeImage(logo)) ModelState.AddModelError("logo", "Logo must be a PNG or JPG up to 2 MB.");
        }
        if (!ModelState.IsValid) { m.CompanyLogoPath = e.CompanyLogoPath; return View("Edit", m); }

        e.Name = m.Name; e.Type = m.Type; e.Presenter = m.Presenter; e.CompanyName = m.CompanyName;
        e.ContactName = m.ContactName; e.ContactEmail = m.ContactEmail; e.ContactPhone = m.ContactPhone;
        e.Date = m.Date.Date; e.EndDate = lastDay > m.Date.Date ? lastDay : null; e.StartTime = m.StartTime; e.EndTime = m.EndTime; e.Venue = m.Venue;
        e.ExpectedAttendees = m.ExpectedAttendees; e.ContentAr = m.ContentAr; e.ContentEn = m.ContentEn;
        if (logo is { Length: > 0 })
        {
            var rel = Path.Combine("logos", Guid.NewGuid().ToString("N") + Path.GetExtension(logo.FileName).ToLowerInvariant());
            Directory.CreateDirectory(_wf.AppData("logos"));
            await using var fs = System.IO.File.Create(_wf.AppData(rel));
            await logo.CopyToAsync(fs);
            e.CompanyLogoPath = rel;
        }
        await _db.SaveChangesAsync();
        if (submitAction == "submit") await _wf.Submit(e, Username);
        else await _wf.Audit(Username, "SaveDraft", $"Event {e.Id}");
        return RedirectToAction(nameof(Details), new { id = e.Id });
    }

    [Authorize(Policy = "Requester"), HttpPost]
    public async Task<IActionResult> Submit(int id)
    {
        var e = await _db.Events.FindAsync(id);
        if (e == null || !CanEdit(e)) return NotFound();
        await _wf.Submit(e, Username);
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> DownloadImage(int id)
    {
        var e = await Load(id);
        if (e?.Image == null || !CanView(e)) return NotFound();
        var path = _wf.AppData(e.Image.FilePath);
        return System.IO.File.Exists(path) ? PhysicalFile(path, "image/png", $"event-{id}.png") : NotFound();
    }

    public async Task<IActionResult> DownloadQr(int id)
    {
        var e = await Load(id);
        if (e?.QR == null || !CanView(e) || User.IsInRole(Roles.Media) && !IsAdmin && e.RequesterUsername != Username) return NotFound();
        return File(_qr.Png($"{_wf.PublicBase}/p/{e.QR.Token}", 20), "image/png", $"event-{id}-qr.png");
    }

    // Venue screen for the optional rotating QR (internal, signed in).
    [Authorize(Policy = "Requester")]
    public async Task<IActionResult> Screen(int id)
    {
        var e = await Load(id);
        if (e?.QR == null || !CanView(e)) return NotFound();
        ViewBag.Rotating = SettingsService.Bool(await _settings.AllAsync(), "RotatingQr");
        return View(e);
    }

    [Authorize(Policy = "Requester")]
    public async Task<IActionResult> ScreenQr(int id)
    {
        var e = await Load(id);
        if (e?.QR == null || !CanView(e)) return NotFound();
        var s = await _settings.AllAsync();
        var token = SettingsService.Bool(s, "RotatingQr") ? _qr.Rotating(e.QR.Token, DateTime.UtcNow) : e.QR.Token;
        Response.Headers.CacheControl = "no-store";
        return File(_qr.Png($"{_wf.PublicBase}/p/{token}", 20), "image/png");
    }

    // First-time setup steps for Admin (shown on the home page until everything is done).
    private async Task<List<SetupItem>> SetupChecklist()
    {
        var l = await _conn.LdapAsync(); var m = await _conn.SmtpAsync(); var s = await _settings.AllAsync();
        bool Changed(string key) => !string.IsNullOrWhiteSpace(s[key]) && !s[key].EndsWith("@example.com");
        string T(string en, string ar) => Request.Cookies["lang"] == "ar" ? ar : en;
        return new List<SetupItem>
        {
            new(T("Connect Active Directory", "اربط Active Directory"), T("So people can sign in with their company password.", "ليسجّل الموظفون الدخول بكلمة مرور الشركة."),
                !string.IsNullOrEmpty(l.Host) && !string.IsNullOrEmpty(l.BaseDn) && !string.IsNullOrEmpty(l.SvcDn), Url.Action("Connections", "Admin")!),
            new(T("Turn on email sending", "فعّل إرسال البريد"), T("Approval emails, food requests and reminders need an SMTP server.", "رسائل الموافقة وطلبات الطعام والتذكير تحتاج خادم SMTP."),
                m.Enabled && !string.IsNullOrEmpty(m.Host), Url.Action("Connections", "Admin")!),
            new(T("Set the admin mailbox", "حدّد بريد الإدارة"), T("It receives approval notices, food requests and evaluation results.", "يستلم إشعارات الموافقة وطلبات الطعام ونتائج التقييم."),
                Changed("AdminMailbox"), Url.Action("Settings", "Admin")!),
            new(T("Set the Media Officer email", "حدّد بريد مسؤول الإعلام"), T("It receives the approved event image.", "يستلم صورة الفعالية المعتمدة."),
                Changed("MediaOfficerEmail"), Url.Action("Settings", "Admin")!),
            new(T("Choose the HOD", "حدّد رئيس القسم"), T("Enter the HOD's username and email in Settings > Recipients (or leave the username empty to allow anyone with the HOD role).", "أدخل اسم مستخدم رئيس القسم وبريده في الإعدادات > المستلمون."),
                !string.IsNullOrWhiteSpace(s["HodEmail"]), Url.Action("Settings", "Admin")!),
        };
    }

    // Printable poster: big QR + attendee instructions in both languages.
    [Authorize(Policy = "Requester")]
    public async Task<IActionResult> Poster(int id)
    {
        var e = await Load(id);
        if (e?.QR == null || !CanView(e)) return NotFound();
        ViewBag.Rotating = SettingsService.Bool(await _settings.AllAsync(), "RotatingQr");
        return View(e);
    }

    private Task<EventRequest?> Load(int id) => _db.Events.Include(e => e.Approvals.OrderBy(a => a.DecidedAt)).Include(e => e.Image).Include(e => e.QR).Include(e => e.FoodRequests)
        .AsSplitQuery().FirstOrDefaultAsync(e => e.Id == id);

    private bool CanView(EventRequest e) => IsAdmin || User.IsInRole(Roles.Hod) || e.RequesterUsername == Username ||
        (User.IsInRole(Roles.Media) && e.Status >= EventStatus.Approved && e.Status != EventStatus.Rejected);

    private bool CanEdit(EventRequest e) => e.RequesterUsername == Username && (e.Status == EventStatus.Draft || e.Status == EventStatus.Rejected);

    private static bool LooksLikeImage(IFormFile f)
    {
        using var s = f.OpenReadStream();
        Span<byte> h = stackalloc byte[4];
        if (s.Read(h) < 4) return false;
        return (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) || (h[0] == 0xFF && h[1] == 0xD8);
    }
}

public record SetupItem(string Label, string Hint, bool Done, string Url);
