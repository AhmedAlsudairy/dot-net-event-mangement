using GuestEvents.Data;
using GuestEvents.Models;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Services;

public class WorkflowService
{
    private readonly AppDb _db;
    private readonly SettingsService _settings;
    private readonly MailService _mail;
    private readonly QrService _qr;
    private readonly ImageService _img;
    private readonly IConfiguration _cfg;
    private readonly IWebHostEnvironment _env;

    public WorkflowService(AppDb db, SettingsService settings, MailService mail, QrService qr, ImageService img, IConfiguration cfg, IWebHostEnvironment env)
    { _db = db; _settings = settings; _mail = mail; _qr = qr; _img = img; _cfg = cfg; _env = env; }

    public string PublicBase => (_cfg["App:PublicBaseUrl"] ?? "https://localhost:5001").TrimEnd('/');
    public string InternalBase => (_cfg["App:InternalBaseUrl"] ?? "https://localhost:5001").TrimEnd('/');
    public string AppData(params string[] parts) => Path.Combine(new[] { _env.ContentRootPath, "App_Data" }.Concat(parts).ToArray());

    public async Task Audit(string user, string action, string? details = null)
    {
        _db.AuditLogs.Add(new AuditLog { User = user, Action = action, Details = details });
        await _db.SaveChangesAsync();
    }

    public async Task Submit(EventRequest e, string user)
    {
        e.Status = EventStatus.Submitted;
        e.SubmittedAt = DateTime.UtcNow;
        e.ReminderSentAt = null;
        await _db.SaveChangesAsync();
        await Audit(user, "Submit", $"Event {e.Id}");
    }

    public async Task Reject(EventRequest e, AdUserLite approver, string comment)
    {
        _db.Approvals.Add(new Approval { EventRequestId = e.Id, ApproverUsername = approver.Username, ApproverName = approver.Name, Approved = false, Comment = comment });
        e.Status = EventStatus.Rejected;
        await _db.SaveChangesAsync();
        await Audit(approver.Username, "Reject", $"Event {e.Id}: {comment}");
    }

    public async Task Approve(EventRequest e, AdUserLite approver, string? comment)
    {
        var s = await _settings.AllAsync();
        _db.Approvals.Add(new Approval { EventRequestId = e.Id, ApproverUsername = approver.Username, ApproverName = approver.Name, Approved = true, Comment = comment });
        e.Status = EventStatus.Approved;

        // QR (one per event) with its windows
        var qr = await _db.QRCodes.FirstOrDefaultAsync(q => q.EventRequestId == e.Id);
        if (qr == null)
        {
            qr = new QRCodeInfo { EventRequestId = e.Id, Token = QrService.NewToken() };
            _db.QRCodes.Add(qr);
        }
        SetWindows(qr, e, s);
        await _db.SaveChangesAsync();

        // Image
        var rotating = SettingsService.Bool(s, "RotatingQr");
        var qrPng = rotating ? null : _qr.Png($"{PublicBase}/p/{qr.Token}");
        var png = _img.Render(e, qrPng, s);
        Directory.CreateDirectory(AppData("images"));
        var rel = Path.Combine("images", $"event-{e.Id}.png");
        await File.WriteAllBytesAsync(AppData(rel), png);
        var img = await _db.Images.FirstOrDefaultAsync(i => i.EventRequestId == e.Id);
        if (img == null) _db.Images.Add(new EventImage { EventRequestId = e.Id, FilePath = rel });
        else { img.FilePath = rel; img.GeneratedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync();
        await Audit(approver.Username, "Approve", $"Event {e.Id}");

        // Automatic messages
        var imgPath = AppData(rel);
        var v = Vars(e, s); v["approver"] = approver.Name; v["link"] = $"{InternalBase}/Events/Details/{e.Id}";
        await _mail.SendAsync(e.Id, new[] { s["MediaOfficerEmail"] }, null, MailService.Fill(s["TplMediaSubject"], v), MailService.Fill(s["TplMediaBody"], v), imgPath);
        await _mail.SendAsync(e.Id, new[] { e.ContactEmail }, null, MailService.Fill(s["TplContactSubject"], v), MailService.Fill(s["TplContactBody"], v), imgPath);
        var adminTo = new[] { s["AdminMailbox"] }.Concat(MailService.Split(s["AdminExtraTo"]));
        await _mail.SendAsync(e.Id, adminTo, MailService.Split(s["AdminCc"]), MailService.Fill(s["TplAdminSubject"], v), MailService.Fill(s["TplAdminBody"], v));
        if (!string.IsNullOrEmpty(e.RequesterEmail))
            await _mail.SendAsync(e.Id, new[] { e.RequesterEmail }, null, MailService.Fill(s["TplRequesterSubject"], v), MailService.Fill(s["TplRequesterBody"], v));
    }

    public static void SetWindows(QRCodeInfo qr, EventRequest e, Dictionary<string, string> s)
    {
        qr.CheckInOpen = e.Start.AddMinutes(-SettingsService.Int(s, "CheckInOpenMinutesBefore"));
        qr.CheckInClose = e.End.AddMinutes(SettingsService.Int(s, "CheckInCloseMinutesAfterEnd"));
        qr.EvaluationClose = e.End.AddDays(SettingsService.Int(s, "EvaluationOpenDays"));
    }

    public static Dictionary<string, string> Vars(EventRequest e, Dictionary<string, string> s) => new()
    {
        ["event_name"] = e.Name, ["presenter"] = e.Presenter, ["company"] = e.CompanyName, ["contact_name"] = e.ContactName,
        ["date"] = e.Date.ToString(s.GetValueOrDefault("DateFormat", "yyyy-MM-dd")) + (e.IsMultiDay ? " - " + e.LastDate.ToString(s.GetValueOrDefault("DateFormat", "yyyy-MM-dd")) : ""),
        ["end_date"] = e.LastDate.ToString(s.GetValueOrDefault("DateFormat", "yyyy-MM-dd")), ["days"] = e.DayCount.ToString(), ["start"] = e.StartTime.ToString(@"hh\:mm"), ["end"] = e.EndTime.ToString(@"hh\:mm"),
        ["venue"] = e.Venue, ["requester"] = e.RequesterName,
    };

    /// <summary>Called periodically: reminders to the HOD (working days Sun-Thu) and status roll-over.</summary>
    public async Task Tick()
    {
        var s = await _settings.AllAsync();
        var days = SettingsService.Int(s, "ReminderWorkingDays");
        var now = DateTime.Now;
        var pending = await _db.Events.Where(e => e.Status == EventStatus.Submitted && e.ReminderSentAt == null && e.SubmittedAt != null).ToListAsync();
        foreach (var e in pending)
        {
            if (WorkingDays.Between(e.SubmittedAt!.Value.ToLocalTime(), now) < days) continue;
            var hod = await HodEmail(s);
            e.ReminderSentAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _mail.SendAsync(e.Id, new[] { hod }, null, $"Reminder: pending approval - {e.Name}",
                $"The event request \"{e.Name}\" from {e.RequesterName} has been waiting for {days} working day(s).\n{InternalBase}/Approvals");
        }

        var utc = DateTime.UtcNow;
        var approved = await _db.Events.Include(e => e.QR).Where(e => e.Status == EventStatus.Approved || e.Status == EventStatus.EventDone).ToListAsync();
        foreach (var e in approved)
        {
            if (e.QR != null && e.QR.EvaluationClose <= now) e.Status = EventStatus.EvaluationClosed;
            else if (e.End <= now) e.Status = EventStatus.EventDone;
        }
        await _db.SaveChangesAsync();
    }

    private async Task<string> HodEmail(Dictionary<string, string> s)
    {
        if (!string.IsNullOrWhiteSpace(s["HodEmail"])) return s["HodEmail"];
        var configured = _cfg["App:HodEmail"];
        return string.IsNullOrWhiteSpace(configured) ? s["AdminMailbox"] : configured;
    }
}

public record AdUserLite(string Username, string Name);
