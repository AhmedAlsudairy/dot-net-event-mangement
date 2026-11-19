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
    private readonly ApprovalChain _chain;

    public WorkflowService(AppDb db, SettingsService settings, MailService mail, QrService qr, ImageService img, IConfiguration cfg, IWebHostEnvironment env, ApprovalChain chain)
    { _chain = chain; _db = db; _settings = settings; _mail = mail; _qr = qr; _img = img; _cfg = cfg; _env = env; }

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
        var first = (await _chain.StepsAsync()).FirstOrDefault();
        e.CurrentStepId = first?.Id;
        e.StepStartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await Audit(user, "Submit", $"Event {e.Id}");
        if (first != null) await NotifyStep(e, first, null);
    }

    /// <summary>Tell the people of a step that a request is waiting for them (named emails, the step's extra address, or the admin mailbox).</summary>
    private async Task NotifyStep(EventRequest e, ApprovalStep step, string? previousApprover)
    {
        var s = await _settings.AllAsync();
        var to = MailService.Split(step.Approvers).Where(x => x.Contains('@')).Concat(MailService.Split(step.NotifyEmail)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (to.Count == 0) to.Add(s["AdminMailbox"]);
        var v = Vars(e, s);
        var from = previousApprover == null ? "" : $" It was approved by {previousApprover} at the previous step.";
        await _mail.SendAsync(e.Id, to, null, $"Waiting for your approval ({step.Name}) - {e.Name}",
            $"The event request \"{e.Name}\" from {e.RequesterName} needs your approval at the step \"{step.Name}\".{from}\nDate: {v["date"]}, venue: {e.Venue}.\n{InternalBase}/Approvals");
    }

    public async Task Reject(EventRequest e, AdUserLite approver, string comment)
    {
        var step = ApprovalChain.Current(e, await _chain.StepsAsync(false));
        _db.Approvals.Add(new Approval { EventRequestId = e.Id, ApproverUsername = approver.Username, ApproverName = approver.Name, Approved = false, StepId = step?.Id, StepName = step?.Name, Comment = comment });
        e.Status = EventStatus.Rejected;
        e.CurrentStepId = null;
        await _db.SaveChangesAsync();
        await Audit(approver.Username, "Reject", $"Event {e.Id} at {step?.Name}: {comment}");
    }

    /// <summary>
    /// One approval at the current step. When the step has enough approvals the request moves to the next step (its people are emailed);
    /// after the last step the event is fully approved (image, QR and the final emails).
    /// </summary>
    public async Task ApproveStep(EventRequest e, ApprovalStep step, List<ApprovalStep> steps, AdUserLite approver, string? comment)
    {
        _db.Approvals.Add(new Approval { EventRequestId = e.Id, ApproverUsername = approver.Username, ApproverName = approver.Name, Approved = true, StepId = step.Id, StepName = step.Name, Comment = comment });
        await _db.SaveChangesAsync();
        await Audit(approver.Username, "ApproveStep", $"Event {e.Id} at {step.Name}");

        e = (await _db.Events.Include(x => x.Approvals).FirstAsync(x => x.Id == e.Id));
        if (ApprovalChain.Given(e, step).Select(a => a.ApproverUsername.ToLowerInvariant()).Distinct().Count() < step.RequiredApprovals) return; // still collecting approvals at this step

        var next = ApprovalChain.Next(step, steps);
        if (next == null) { await Approve(e, approver, comment); return; }

        e.CurrentStepId = next.Id; e.StepStartedAt = DateTime.UtcNow; e.ReminderSentAt = null;
        await _db.SaveChangesAsync();
        await NotifyStep(e, next, approver.Name);
        if (!string.IsNullOrEmpty(e.RequesterEmail))
            await _mail.SendAsync(e.Id, new[] { e.RequesterEmail }, null, $"Your event request moved to the next approval step - {e.Name}",
                $"\"{step.Name}\" approved your event request \"{e.Name}\". It now waits at the step \"{next.Name}\". You will get an email when it is decided.\n{InternalBase}/Events/Details/{e.Id}");
    }

    public async Task Approve(EventRequest e, AdUserLite approver, string? comment)
    {
        var s = await _settings.AllAsync();
        e.Status = EventStatus.Approved;
        e.CurrentStepId = null;

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
        // The poster is a nice-to-have: if image drawing fails on this machine, still approve the event and send the emails without it.
        string? rel = null;
        try
        {
            var png = _img.Render(e, qrPng, s);
            Directory.CreateDirectory(AppData("images"));
            rel = Path.Combine("images", $"event-{e.Id}.png");
            await File.WriteAllBytesAsync(AppData(rel), png);
            var img = await _db.Images.FirstOrDefaultAsync(i => i.EventRequestId == e.Id);
            if (img == null) _db.Images.Add(new EventImage { EventRequestId = e.Id, FilePath = rel });
            else { img.FilePath = rel; img.GeneratedAt = DateTime.UtcNow; }
        }
        catch (Exception ex)
        {
            rel = null;
            await Audit(approver.Username, "ImageFailed", $"Event {e.Id}: {ex.GetType().Name} {ex.Message}".Length > 300 ? $"Event {e.Id}: {ex.GetType().Name}" : $"Event {e.Id}: {ex.GetType().Name} {ex.Message}");
        }
        await _db.SaveChangesAsync();
        await Audit(approver.Username, "Approve", $"Event {e.Id}");

        // Automatic messages
        var imgPath = rel == null ? null : AppData(rel);
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
        // reminders: a request that has waited too long at its current approval step reminds that step's people
        var steps = await _chain.StepsAsync();
        var waiting = await _db.Events.Where(e => e.Status == EventStatus.Submitted && e.ReminderSentAt == null).ToListAsync();
        foreach (var e in waiting)
        {
            var step = ApprovalChain.Current(e, steps);
            var since = e.StepStartedAt ?? e.SubmittedAt;
            if (step == null || since == null || WorkingDays.Between(since.Value.ToLocalTime(), now) < days) continue;
            e.ReminderSentAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            var to = MailService.Split(step.Approvers).Where(x => x.Contains('@')).Concat(MailService.Split(step.NotifyEmail)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (to.Count == 0) to.Add(s["AdminMailbox"]);
            await _mail.SendAsync(e.Id, to, null, $"Reminder: pending approval ({step.Name}) - {e.Name}",
                $"The event request \"{e.Name}\" from {e.RequesterName} has been waiting at the step \"{step.Name}\" for {days} working day(s).\n{InternalBase}/Approvals");
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
}

public record AdUserLite(string Username, string Name);
