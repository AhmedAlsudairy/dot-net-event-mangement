using GuestEvents.Data;
using GuestEvents.Models;
using GuestEvents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Controllers;

[Authorize(Policy = "EventsUser")]
public class EventsController : AppController
{
    private readonly AppDb _db;
    private readonly WorkflowService _wf;
    private readonly QrService _qr;
    private readonly SettingsService _settings;
    private readonly ConnectionConfig _conn;
    public EventsController(AppDb db, WorkflowService wf, QrService qr, SettingsService settings, ConnectionConfig conn, KpiPicker kpi, ApprovalChain chain) { _chain = chain; _db = db; _wf = wf; _qr = qr; _settings = settings; _conn = conn; _kpi = kpi; }

    static readonly string[] ImgExt = { ".png", ".jpg", ".jpeg" };

    public async Task<IActionResult> Index()
    {
        if (User.IsInRole(Roles.Research) && !IsAdmin && !User.IsInRole(Roles.Staff) && !User.IsInRole(Roles.Hod) && !User.IsInRole(Roles.Dean) && !User.IsInRole(Roles.Media))
            return RedirectToAction("Index", "Research"); // the research in-charge lands on the archive
        var q = _db.Events.AsNoTracking().AsQueryable();
        if (User.IsInRole(Roles.Media) && !IsAdmin && !User.IsInRole(Roles.Hod) && !User.IsInRole(Roles.Dean) && !User.IsInRole(Roles.Staff))
            q = q.Where(e => EventStatuses.Live.Contains(e.Status));
        else if (!IsAdmin && !User.IsInRole(Roles.Hod) && !User.IsInRole(Roles.Dean))
        {
            if (_isApprover) q = q.Where(e => e.RequesterUsername == Username || e.Status == EventStatus.Submitted || e.Approvals.Any(a => a.ApproverUsername == Username));
            else q = q.Where(e => e.RequesterUsername == Username);
        }
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
        e.KpiId = m.KpiId; e.KpiLabel = await _kpi.LabelAsync(m.KpiId);
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

    // Printable poster: big QR + attendee instructions in both languages.
    [Authorize(Policy = "Requester")]
    public async Task<IActionResult> Poster(int id)
    {
        var e = await Load(id);
        if (e?.QR == null || !CanView(e)) return NotFound();
        ViewBag.Rotating = SettingsService.Bool(await _settings.AllAsync(), "RotatingQr");
        return View(e);
    }

    private readonly KpiPicker _kpi;
    private readonly ApprovalChain _chain;
    private bool _isApprover;   // holds a place in the approval chain (a named approver may have no HOD/Dean role)
    public override async Task OnActionExecutionAsync(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext ctx, Microsoft.AspNetCore.Mvc.Filters.ActionExecutionDelegate next)
    {
        if (User.Identity?.IsAuthenticated == true) _isApprover = await _chain.IsApproverAsync(User);
        await base.OnActionExecutionAsync(ctx, next);
    }
    private Task<EventRequest?> Load(int id) => _db.Events.Include(e => e.Approvals.OrderBy(a => a.DecidedAt)).Include(e => e.Image).Include(e => e.QR).Include(e => e.FoodRequests)
        .AsSplitQuery().FirstOrDefaultAsync(e => e.Id == id);

    private bool CanView(EventRequest e) => IsAdmin || User.IsInRole(Roles.Hod) || User.IsInRole(Roles.Dean) || e.RequesterUsername == Username ||
        (_isApprover && (e.Status == EventStatus.Submitted || e.Approvals.Any(a => a.ApproverUsername == Username))) ||
        (User.IsInRole(Roles.Media) && e.Status.IsLive());

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
