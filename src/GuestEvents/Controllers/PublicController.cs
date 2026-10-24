using GuestEvents.Data;
using GuestEvents.Models;
using GuestEvents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Controllers;

public class PublicVm
{
    public EventRequest Event { get; set; } = null!;
    public QRCodeInfo Qr { get; set; } = null!;
    public string UrlToken { get; set; } = "";
    public string State { get; set; } = "form";   // form | notopen | closed | already | checkedin | done | evalclosed | invalid
    public bool RequireEmail, RequirePhone;
    public List<(string en, string ar)> Questions { get; set; } = new();
    public string? Error { get; set; }
    public DateTime? OpensAt { get; set; }
    public bool HasAnyCheckIn { get; set; }
}

/// <summary>
/// Anonymous pages reachable from the internet. They expose only the event title / venue / time and never touch AD.
/// Attendance (named) and Evaluation (anonymous) are stored separately and never linked.
/// </summary>
[AllowAnonymous, Route("p")]
public class PublicController : Controller
{
    private readonly AppDb _db;
    private readonly SettingsService _settings;
    private readonly DeviceService _device;
    private readonly QrService _qr;
    public PublicController(AppDb db, SettingsService settings, DeviceService device, QrService qr) { _db = db; _settings = settings; _device = device; _qr = qr; }

    [HttpGet("{token}"), EnableRateLimiting("public")]
    public async Task<IActionResult> Index(string token)
    {
        var (vm, s) = await Resolve(token);
        if (vm == null) return Invalid();
        var dev = _device.DeviceId(HttpContext);
        vm.HasAnyCheckIn = await HasCheckedAny(vm.Event, dev);
        if (await HasChecked(vm.Event.Id, dev, DateTime.Today)) vm.State = "already";
        else (vm.State, vm.OpensAt) = CheckInWindow(vm.Event, s);
        if (vm.State == "form" && !PassesChecks(s, vm.Qr, token)) vm.State = "invalid";
        if (vm.State == "form" && !AllowedNetwork(s)) { vm.State = "closed"; vm.Error = "OutsideVenue"; }
        return View("Index", vm);
    }

    [HttpPost("{token}/checkin"), EnableRateLimiting("publicPost")]
    public async Task<IActionResult> CheckIn(string token, string? fullName, string? organization, string? email, string? phone, string? website)
    {
        var (vm, s) = await Resolve(token);
        if (vm == null) return Invalid();
        // Honeypot: real users never see or fill "website". Pretend success so bots learn nothing.
        if (!string.IsNullOrEmpty(website)) { vm.State = "checkedin"; return View("Index", vm); }
        if (!PassesChecks(s, vm.Qr, token)) { vm.State = "invalid"; return View("Index", vm); }

        var dev = _device.DeviceId(HttpContext);
        vm.HasAnyCheckIn = await HasCheckedAny(vm.Event, dev);
        if (await HasChecked(vm.Event.Id, dev, DateTime.Today)) { vm.State = "already"; return View("Index", vm); }
        (vm.State, vm.OpensAt) = CheckInWindow(vm.Event, s);
        if (vm.State != "form") return View("Index", vm);
        if (!AllowedNetwork(s)) { vm.State = "closed"; vm.Error = "OutsideVenue"; return View("Index", vm); }

        fullName = fullName?.Trim(); organization = organization?.Trim();
        var bad = string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(organization) || fullName.Length > 200 || organization.Length > 200
            || (SettingsService.Bool(s, "RequireEmail") && string.IsNullOrWhiteSpace(email))
            || (SettingsService.Bool(s, "RequirePhone") && string.IsNullOrWhiteSpace(phone))
            || (email?.Length ?? 0) > 200 || (phone?.Length ?? 0) > 50;
        if (bad) { vm.State = "form"; vm.Error = "Required"; return View("Index", vm); }

        _db.Attendances.Add(new Attendance
        {
            EventRequestId = vm.Event.Id, FullName = fullName!, Organization = organization!,
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(), Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            Day = DateTime.Today, DeviceHash = _device.AttendanceHash(dev, vm.Event.Id, DateTime.Today),
        });
        await _db.SaveChangesAsync();
        vm.State = "checkedin";
        return View("Index", vm);
    }

    [HttpGet("{token}/evaluate"), EnableRateLimiting("public")]
    public async Task<IActionResult> Evaluate(string token)
    {
        var (vm, s) = await Resolve(token, evaluating: true);
        if (vm == null) return Invalid();
        vm.State = await EvalState(vm);
        return View("Evaluate", vm);
    }

    [HttpPost("{token}/evaluate"), EnableRateLimiting("publicPost")]
    public async Task<IActionResult> EvaluatePost(string token, int overall, int presenter, int content, int venueOrg, int? food, string? comments, string? website)
    {
        var (vm, s) = await Resolve(token, evaluating: true);
        if (vm == null) return Invalid();
        if (!string.IsNullOrEmpty(website)) { vm.State = "done"; return View("Evaluate", vm); }
        vm.State = await EvalState(vm);
        if (vm.State != "form") return View("Evaluate", vm);
        if (!R(overall) || !R(presenter) || !R(content) || !R(venueOrg) || (food != null && !R(food.Value)) || (comments?.Length ?? 0) > 2000)
        { vm.Error = "Required"; return View("Evaluate", vm); }

        var dev = _device.DeviceId(HttpContext);
        // Stored with a DIFFERENT hash key than Attendance, and no attendance id: the two cannot be matched.
        _db.Evaluations.Add(new Evaluation
        {
            EventRequestId = vm.Event.Id, Overall = overall, Presenter = presenter, Content = content, VenueOrg = venueOrg, Food = food,
            Comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim(), DeviceHash = _device.EvaluationHash(dev, vm.Event.Id),
        });
        await _db.SaveChangesAsync();
        vm.State = "done";
        return View("Evaluate", vm);
    }

    // ---------------------------------------------------------------- helpers
    private static bool R(int v) => v is >= 1 and <= 5;
    private IActionResult Invalid() { Response.StatusCode = 404; return View("Invalid"); }

    private async Task<(PublicVm?, Dictionary<string, string>)> Resolve(string urlToken, bool evaluating = false)
    {
        var s = await _settings.AllAsync();
        var baseToken = urlToken.Split('.')[0];
        var qr = await _db.QRCodes.Include(q => q.EventRequest).AsNoTracking().FirstOrDefaultAsync(q => q.Token == baseToken);
        if (qr?.EventRequest == null || qr.EventRequest.Status == EventStatus.Rejected || qr.EventRequest.Status == EventStatus.Draft) return (null, s);
        if (DateTime.Now > qr.EvaluationClose) return (null, s);                           // QR stops working after evaluation close
        var qs = s["Questions"].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('|')).Select(p => (p[0], p.Length > 1 ? p[1] : p[0])).ToList();
        return (new PublicVm
        {
            Event = qr.EventRequest, Qr = qr, UrlToken = urlToken, Questions = qs,
            RequireEmail = SettingsService.Bool(s, "RequireEmail"), RequirePhone = SettingsService.Bool(s, "RequirePhone"),
        }, s);
    }

    /// <summary>Rotating mode: the check-in link must carry a fresh signature. Static mode: the bare token is enough.</summary>
    private bool PassesChecks(Dictionary<string, string> s, QRCodeInfo qr, string urlToken)
    {
        if (!SettingsService.Bool(s, "RotatingQr")) return true;
        var parts = urlToken.Split('.');
        return parts.Length == 2 && _qr.ValidRotating(qr.Token, parts[1], DateTime.UtcNow);
    }

    /// <summary>Check-in window of TODAY. The daily times apply to every day between the first and last date.</summary>
    private static (string state, DateTime? opensAt) CheckInWindow(EventRequest e, Dictionary<string, string> s)
    {
        var now = DateTime.Now; var today = now.Date;
        var before = TimeSpan.FromMinutes(SettingsService.Int(s, "CheckInOpenMinutesBefore"));
        var after = TimeSpan.FromMinutes(SettingsService.Int(s, "CheckInCloseMinutesAfterEnd"));
        if (today < e.Date.Date) return ("notopen", e.Date.Date + e.StartTime - before);
        if (today > e.LastDate) return ("closed", null);
        var open = today + e.StartTime - before; var close = today + e.EndTime + after;
        if (now < open) return ("notopen", open);
        if (now > close) return today < e.LastDate ? ("notopen", today.AddDays(1) + e.StartTime - before) : ("closed", null);
        return ("form", null);
    }

    private bool AllowedNetwork(Dictionary<string, string> s)
    {
        if (SettingsService.Bool(s, "RotatingQr") || !SettingsService.Bool(s, "BlockOutsideVenue")) return true;
        var ip = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "";
        var prefixes = MailService.Split(s["VenueNetworkPrefixes"]);
        return prefixes.Count == 0 || prefixes.Any(p => ip.StartsWith(p));
    }

    private Task<bool> HasChecked(int eventId, string dev, DateTime day)
    {
        var h = _device.AttendanceHash(dev, eventId, day);
        return _db.Attendances.AnyAsync(a => a.EventRequestId == eventId && a.DeviceHash == h);
    }

    /// <summary>Has this device checked in on ANY day of the event? (needed to open the evaluation)</summary>
    private Task<bool> HasCheckedAny(EventRequest e, string dev)
    {
        var hashes = Enumerable.Range(0, e.DayCount).Select(i => _device.AttendanceHash(dev, e.Id, e.Date.Date.AddDays(i))).ToList();
        return _db.Attendances.AnyAsync(a => a.EventRequestId == e.Id && hashes.Contains(a.DeviceHash));
    }

    private async Task<string> EvalState(PublicVm vm)
    {
        var dev = _device.DeviceId(HttpContext);
        if (DateTime.Now > vm.Qr.EvaluationClose) return "evalclosed";
        if (!await HasCheckedAny(vm.Event, dev)) return "mustcheckin";
        if (await _db.Evaluations.AnyAsync(e => e.EventRequestId == vm.Event.Id && e.DeviceHash == _device.EvaluationHash(dev, vm.Event.Id))) return "done";
        return "form";
    }
}
