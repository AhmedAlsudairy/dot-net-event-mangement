using GuestEvents.Data;
using GuestEvents.Models;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Services;

public class SettingsService
{
    private readonly AppDb _db;
    public SettingsService(AppDb db) => _db = db;

    public static readonly Dictionary<string, string> Defaults = new()
    {
        ["AdminMailbox"] = "admin@example.com",
        ["AdminExtraTo"] = "",
        ["AdminCc"] = "",
        ["MediaOfficerEmail"] = "media@example.com",
        ["HodEmail"] = "",               // gets the reminder mail; empty = App:HodEmail, then admin mailbox
        ["HodUsername"] = "",            // single AD user; empty = any member of Events-HOD
        // External visits (bus requests) - approval is separate from event approval
        ["VisitApprovalRequired"] = "true", // false = a submitted visit goes straight to the bus step
        ["VisitApprovers"] = "",         // usernames (comma separated) who approve visits; empty = anyone with the HOD role. Admin can always approve.
        ["VisitApproverEmail"] = "",     // gets "awaiting approval" mails; empty = HOD email, then admin mailbox
        ["VisitBusUsers"] = "",          // usernames who confirm the bus; empty = Admin only
        ["VisitBusEmail"] = "",          // gets approved bus requests; empty = admin mailbox + extra admin recipients
        ["VisitBusCc"] = "",
        ["VisitNotifyRequester"] = "true",
        ["VisitMinDaysAhead"] = "0",     // a visit must be submitted at least this many days before the trip
        // Research uploads: emailed to the research in-charge and kept in the archive (no approval)
        ["ResearchEmail"] = "",          // research in-charge; empty = admin mailbox
        ["ResearchCc"] = "",
        ["ResearchNotifySubmitter"] = "true",
        ["ResearchMaxMb"] = "20",
        // Sign-in help: the app never stores passwords, so "Forgot password" sends people to the company reset page and/or to IT
        ["PasswordResetUrl"] = "",       // e.g. https://aka.ms/sspr or the company self-service portal; empty = only the "ask IT" form
        ["PasswordHelpEmail"] = "",      // IT helpdesk; empty = admin mailbox
        ["ReminderWorkingDays"] = "2",
        ["CheckInOpenMinutesBefore"] = "30",
        ["CheckInCloseMinutesAfterEnd"] = "0",
        ["EvaluationOpenDays"] = "7",
        ["RequireEmail"] = "false",
        ["RequirePhone"] = "false",
        ["RotatingQr"] = "false",
        ["VenueNetworkPrefixes"] = "",   // e.g. "10.1.5.,192.168.20." ; used when RotatingQr=false and BlockOutsideVenue=true
        ["BlockOutsideVenue"] = "false",
        ["UseArabicDigits"] = "false",
        ["DateFormat"] = "yyyy-MM-dd",
        ["TplMediaSubject"] = "Approved event: {event_name}",
        ["TplMediaBody"] = "Event: {event_name}\nPresenter: {presenter}\nCompany: {company}\nDate: {date} {start}-{end}\nVenue: {venue}\n\nThe event image is attached.",
        ["TplContactSubject"] = "Event approved / تمت الموافقة على الفعالية: {event_name}",
        ["TplContactBody"] = "Dear {contact_name},\n\nWe are pleased to confirm the event \"{event_name}\" with {presenter} on {date} at {venue}.\nThe event image is attached.\n\nعزيزي {contact_name}،\n\nيسعدنا تأكيد الفعالية \"{event_name}\" مع {presenter} بتاريخ {date} في {venue}.\nمرفق صورة الفعالية.",
        ["TplAdminSubject"] = "Event approved: {event_name}",
        ["TplAdminBody"] = "Event \"{event_name}\" ({date}, {venue}) was approved by {approver}.",
        ["TplRequesterSubject"] = "Your event was approved / تمت الموافقة: {event_name}",
        ["TplRequesterBody"] = "Your event \"{event_name}\" was approved.\nتمت الموافقة على فعاليتك.\n{link}",
        ["Questions"] = "Presenter quality|جودة المقدم;Content relevance|ملاءمة المحتوى;Venue and organization|المكان والتنظيم;Food quality|جودة الطعام",
        ["ImgPrimary"] = "#0B3D5C",
        ["ImgAccent"] = "#E0A526",
        ["ImgFont"] = "Tahoma",
        ["ImgTitleEn"] = "Guest Workshop",
        ["ImgTitleAr"] = "ورشة ضيف",
    };

    public async Task<Dictionary<string, string>> AllAsync()
    {
        var d = new Dictionary<string, string>(Defaults);
        foreach (var s in await _db.Settings.AsNoTracking().ToListAsync()) d[s.Key] = s.Value;
        return d;
    }

    public async Task SetAsync(string key, string value)
    {
        var s = await _db.Settings.FindAsync(key);
        if (s == null) _db.Settings.Add(new AppSetting { Key = key, Value = value });
        else s.Value = value;
        await _db.SaveChangesAsync();
    }

    public static int Int(Dictionary<string, string> s, string k) => int.TryParse(s.GetValueOrDefault(k), out var v) ? v : int.Parse(Defaults[k]);
    public static bool Bool(Dictionary<string, string> s, string k) => s.GetValueOrDefault(k) == "true";

    static bool InList(string? list, string? user) =>
        !string.IsNullOrEmpty(user) && MailService.Split(list).Any(x => x.Equals(user, StringComparison.OrdinalIgnoreCase));

    /// <summary>True when the person is in a comma separated list of usernames or email addresses.</summary>
    public static bool InUserList(string? list, System.Security.Claims.ClaimsPrincipal u) =>
        InList(list, u.Identity?.Name) || InList(list, u.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value);

    /// <summary>Who may approve external visits: the listed users (username or email); with an empty list anyone with the HOD role. Admin always.</summary>
    public static bool IsVisitApprover(Dictionary<string, string> s, System.Security.Claims.ClaimsPrincipal u) =>
        u.IsInRole(Roles.Admin) || (string.IsNullOrWhiteSpace(s.GetValueOrDefault("VisitApprovers")) ? u.IsInRole(Roles.Hod) : InUserList(s["VisitApprovers"], u));

    /// <summary>Who may confirm the bus: the listed users (username or email), and Admin.</summary>
    public static bool IsVisitBusUser(Dictionary<string, string> s, System.Security.Claims.ClaimsPrincipal u) =>
        u.IsInRole(Roles.Admin) || InUserList(s.GetValueOrDefault("VisitBusUsers"), u);
}

public static class WorkingDays
{
    // Working week: Sunday to Thursday.
    public static bool IsWorkingDay(DateTime d) => d.DayOfWeek != DayOfWeek.Friday && d.DayOfWeek != DayOfWeek.Saturday;

    public static int Between(DateTime from, DateTime to)
    {
        int n = 0;
        for (var d = from.Date.AddDays(1); d <= to.Date; d = d.AddDays(1))
            if (IsWorkingDay(d)) n++;
        return n;
    }
}
