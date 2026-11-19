using GuestEvents.Data;
using GuestEvents.Models;
using GuestEvents.Services;
using Kpi.Dashboard.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KpiPerms = Kpi.Dashboard.Web.Models.Permissions;

namespace GuestEvents.Controllers;

/// <summary>A thing waiting for this person, shown at the top of the home page.</summary>
public record HomeTask(string Icon, string Tone, int Count, string Title, string Hint, string Url, string Action);
public record HomeAction(string Icon, string Label, string Url, bool Primary = false);
public record HomeItem(string Kind, string Name, DateTime Date, string Status, string StatusTone, string Url);
public record KpiSnapshot(int Kpis, int Pct, int Done, int Prog, int Low, int Overdue, int Unassigned, bool CanEdit);

public class HomeVm
{
    public string Name { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public List<HomeTask> Tasks { get; set; } = new();
    public List<HomeAction> Actions { get; set; } = new();
    public List<HomeItem> Mine { get; set; } = new();
    public List<EventRequest> Upcoming { get; set; } = new();
    public KpiSnapshot? Kpi { get; set; }
    public List<SetupItem>? Setup { get; set; }
}

/// <summary>
/// Home page after sign-in: what is waiting for this person (by role), quick actions,
/// their recent requests, upcoming events and a KPI snapshot.
/// </summary>
[Authorize(Policy = "Viewer")]
public class HomeController : AppController
{
    private readonly AppDb _db;
    private readonly ApprovalChain _chain;
    private readonly SettingsService _settings;
    private readonly ConnectionConfig _conn;
    private readonly DashboardService _kpi;
    private readonly PermissionService _perms;
    private readonly Kpi.Dashboard.Web.Data.AppDbContext _kpiDb;

    public HomeController(AppDb db, ApprovalChain chain, SettingsService settings, ConnectionConfig conn, DashboardService kpi, PermissionService perms, Kpi.Dashboard.Web.Data.AppDbContext kpiDb)
    { _db = db; _chain = chain; _settings = settings; _conn = conn; _kpi = kpi; _perms = perms; _kpiDb = kpiDb; }

    private string T(string en, string ar) => Request.Cookies["lang"] == "ar" ? ar : en;

    public async Task<IActionResult> Index()
    {
        var kp = _perms.For(User);
        bool eventsUser = new[] { Roles.Staff, Roles.Hod, Roles.Dean, Roles.Media, Roles.Research, Roles.Admin }.Any(User.IsInRole);
        // Someone who only works in the KPI pages has no events home: send them straight to the KPI dashboard.
        if (!eventsUser) return kp.Contains(KpiPerms.DashboardView) ? Redirect("/Kpi/Dashboard") : View("NoAccess");
        var vm = new HomeVm { Name = DisplayName };
        vm.Roles = new[] { Roles.Staff, Roles.Hod, Roles.Dean, Roles.Media, Roles.Research, Roles.Kpi, Roles.KpiManager, Roles.KpiEditor, Roles.KpiOfficer, Roles.KpiViewer, Roles.Admin }.Where(User.IsInRole).ToList();
        var s = await _settings.AllAsync();
        bool requester = User.IsInRole(Roles.Staff) || IsAdmin;

        // ---- Things waiting for me ----
        if (await _chain.IsApproverAsync(User))
        {
            var n = (await _chain.PendingForAsync(User)).Count;
            if (n > 0) vm.Tasks.Add(new("check2-square", "warn", n, T("Event requests waiting for your approval", "طلبات فعاليات بانتظار موافقتك"),
                T("Approve or return them with a comment.", "اعتمدها أو أعدها مع ملاحظة."), Url.Action("Index", "Approvals")!, T("Review", "مراجعة")));
        }
        if (SettingsService.IsVisitApprover(s, User))
        {
            var n = await _db.Visits.CountAsync(v => v.Status == VisitStatus.Submitted);
            if (n > 0) vm.Tasks.Add(new("bus-front", "warn", n, T("External visits waiting for your decision", "زيارات خارجية بانتظار قرارك"),
                T("Approve them so the bus can be booked.", "اعتمدها ليتم حجز الحافلة."), Url.Action("Index", "Visits")!, T("Review", "مراجعة")));
        }
        if (SettingsService.IsVisitBusUser(s, User))
        {
            var n = await _db.Visits.CountAsync(v => v.Status == VisitStatus.Approved);
            if (n > 0) vm.Tasks.Add(new("truck", "brand", n, T("Approved visits that need a bus", "زيارات معتمدة تحتاج حافلة"),
                T("Confirm the bus details.", "أكّد تفاصيل الحافلة."), Url.Action("Index", "Visits")!, T("Confirm", "تأكيد")));
        }
        if (requester)
        {
            var rejected = await _db.Events.CountAsync(e => e.RequesterUsername == Username && e.Status == EventStatus.Rejected)
                         + await _db.Visits.CountAsync(v => v.RequesterUsername == Username && v.Status == VisitStatus.Rejected);
            if (rejected > 0) vm.Tasks.Add(new("arrow-counterclockwise", "bad", rejected, T("Requests returned to you", "طلبات أُعيدت إليك"),
                T("Read the comment, fix and resubmit.", "اقرأ الملاحظة وعدّل ثم أعد الإرسال."), Url.Action("Index", "Events")!, T("Fix", "تعديل")));
            var drafts = await _db.Events.CountAsync(e => e.RequesterUsername == Username && e.Status == EventStatus.Draft)
                       + await _db.Visits.CountAsync(v => v.RequesterUsername == Username && v.Status == VisitStatus.Draft);
            if (drafts > 0) vm.Tasks.Add(new("pencil-square", "brand", drafts, T("Drafts not submitted yet", "مسودات لم تُرسل بعد"),
                T("Finish and submit them for approval.", "أكملها وأرسلها للموافقة."), Url.Action("Index", "Events")!, T("Open", "فتح")));
        }
        if (User.IsInRole(Roles.Media))
        {
            var week = DateTime.Today.AddDays(7);
            var n = await _db.Events.CountAsync(e => e.Status == EventStatus.Approved && e.Date >= DateTime.Today && e.Date <= week);
            if (n > 0) vm.Tasks.Add(new("megaphone", "brand", n, T("Approved events this week to announce", "فعاليات معتمدة هذا الأسبوع للإعلان"),
                T("Download the event image and share it.", "نزّل صورة الفعالية وانشرها."), Url.Action("Index", "Events")!, T("Open", "فتح")));
        }
        if (User.IsInRole(Roles.Research) || IsAdmin)
        {
            var since = DateTime.UtcNow.AddDays(-7);
            var n = await _db.ResearchDocs.CountAsync(r => r.UploadedAt >= since);
            if (n > 0) vm.Tasks.Add(new("journal-text", "brand", n, T("New research uploads this week", "أبحاث جديدة هذا الأسبوع"),
                T("Open the archive to read them.", "افتح الأرشيف للاطلاع عليها."), Url.Action("Index", "Research")!, T("Open", "فتح")));
        }

        // ---- KPI snapshot ----
        if (kp.Contains(KpiPerms.DashboardView) && await _kpiDb.Kpis.AnyAsync())
        {
            var st = _kpi.ComputePlanStats(null, null);
            var overdue = _kpi.GetAtRiskItems(null, 500).Count;
            var cov = _kpi.GetAssignmentCoverage(null);
            bool canEdit = kp.Contains(KpiPerms.TrackingEditAll) || kp.Contains(KpiPerms.TrackingEditOwn) || kp.Contains(KpiPerms.TrackingAssign);
            vm.Kpi = new KpiSnapshot(await _kpiDb.Kpis.CountAsync(), st.OverallPct, st.Done, st.Prog, st.Low, overdue, cov.Unassigned, canEdit);
            if (canEdit && overdue > 0) vm.Tasks.Add(new("exclamation-triangle", "bad", overdue, T("KPI targets past their deadline", "مستهدفات مؤشرات تجاوزت موعدها"),
                T("Update their status on the tracking page.", "حدّث مواقفها في جدول التتبع."), "/Kpi/Tracking", T("Update", "تحديث")));
        }

        // ---- Quick actions ----
        bool approver = await _chain.IsApproverAsync(User);
        if (approver) vm.Actions.Add(new("check2-square", T("Approvals", "الموافقات"), Url.Action("Index", "Approvals")!, !requester));
        if (requester)
        {
            vm.Actions.Add(new("calendar2-plus", T("New event request", "طلب فعالية جديد"), Url.Action("Create", "Events")!, true));
            vm.Actions.Add(new("bus-front", T("Request a bus (visit)", "طلب حافلة (زيارة)"), Url.Action("Create", "Visits")!));
            vm.Actions.Add(new("upload", T("Upload research", "رفع بحث"), Url.Action("Upload", "Research")!));
        }
        if (kp.Contains(KpiPerms.TrackingView)) vm.Actions.Add(new("table", T("KPI tracking", "جدول تتبع المؤشرات"), "/Kpi/Tracking"));
        if (User.IsInRole(Roles.Research) && !requester) vm.Actions.Add(new("journal-text", T("Research archive", "أرشيف الأبحاث"), Url.Action("Index", "Research")!, true));
        if (IsAdmin)
        {
            vm.Actions.Add(new("bar-chart-line", T("Reports & Excel", "التقارير وExcel"), Url.Action("Index", "Reports")!));
            vm.Actions.Add(new("diagram-3", T("Approval flow", "مسار الموافقات"), Url.Action("ApprovalFlow", "Admin")!));
            vm.Actions.Add(new("people", T("Access & roles", "الصلاحيات والأدوار"), Url.Action("RolesPage", "Admin")!));
        }

        if (User.IsInRole(Roles.Media) && !requester) vm.Actions.Add(new("calendar3", T("Approved events", "الفعاليات المعتمدة"), Url.Action("Index", "Events")!, !approver));
        vm.Actions = vm.Actions.OrderByDescending(a => a.Primary).ToList();

        // ---- My recent requests ----
        if (requester)
        {
            var ev = await _db.Events.AsNoTracking().Where(e => e.RequesterUsername == Username).OrderByDescending(e => e.CreatedAt).Take(6).ToListAsync();
            var vi = await _db.Visits.AsNoTracking().Where(v => v.RequesterUsername == Username).OrderByDescending(v => v.CreatedAt).Take(6).ToListAsync();
            vm.Mine = ev.Select(e => new HomeItem("event", e.Name, e.Date, EventLabel(e.Status), EventTone(e.Status), Url.Action("Details", "Events", new { id = e.Id })!))
                .Concat(vi.Select(v => new HomeItem("visit", v.Name, v.Date, VisitLabel(v.Status), VisitTone(v.Status), Url.Action("Details", "Visits", new { id = v.Id })!)))
                .OrderByDescending(x => x.Date).Take(6).ToList();
        }

        // ---- Upcoming approved events (next 14 days) ----
        var until = DateTime.Today.AddDays(14);
        // SQLite cannot sort by TimeSpan, so the time-of-day ordering happens in memory.
        vm.Upcoming = (await _db.Events.AsNoTracking().Where(e => e.Status == EventStatus.Approved && e.Date >= DateTime.Today && e.Date <= until).ToListAsync())
            .OrderBy(e => e.Date).ThenBy(e => e.StartTime).Take(5).ToList();

        if (IsAdmin) vm.Setup = await SetupChecklist(s);
        return View(vm);
    }

    /// <summary>Friendly page for unexpected errors (configured in Program.cs).</summary>
    [AllowAnonymous, IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() { Response.StatusCode = 500; return View(); }

    private string EventLabel(EventStatus s) => s switch
    {
        EventStatus.Draft => T("Draft", "مسودة"), EventStatus.Submitted => T("In approval", "قيد الموافقة"), EventStatus.Approved => T("Approved", "معتمد"),
        EventStatus.Rejected => T("Returned", "معاد"), _ => T("Done", "منتهية"),
    };
    private static string EventTone(EventStatus s) => s switch
    { EventStatus.Draft => "secondary", EventStatus.Submitted => "warning", EventStatus.Approved => "success", EventStatus.Rejected => "danger", _ => "info" };
    private string VisitLabel(VisitStatus s) => s switch
    {
        VisitStatus.Draft => T("Draft", "مسودة"), VisitStatus.Submitted => T("In approval", "قيد الموافقة"), VisitStatus.Approved => T("Approved", "معتمد"),
        VisitStatus.Rejected => T("Returned", "معاد"), VisitStatus.BusConfirmed => T("Bus confirmed", "الحافلة مؤكدة"), _ => s.ToString(),
    };
    private static string VisitTone(VisitStatus s) => s switch
    { VisitStatus.Draft => "secondary", VisitStatus.Submitted => "warning", VisitStatus.Approved => "success", VisitStatus.Rejected => "danger", _ => "info" };

    // First-time setup steps for Admin (shown on the home page until everything is done).
    private async Task<List<SetupItem>> SetupChecklist(Dictionary<string, string> s)
    {
        var l = await _conn.LdapAsync(); var m = await _conn.SmtpAsync();
        bool Changed(string key) => !string.IsNullOrWhiteSpace(s[key]) && !s[key].EndsWith("@example.com");
        var steps = await _chain.StepsAsync(true);
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
            new(T("Check the approval flow", "راجع مسار الموافقات"), T("Choose who approves each step (by role or by name) and where the emails go.", "حدّد من يعتمد كل خطوة (بالدور أو بالاسم) وأين تذهب الرسائل."),
                steps.Any(x => !string.IsNullOrWhiteSpace(x.Approvers) || !string.IsNullOrWhiteSpace(x.NotifyEmail)), Url.Action("ApprovalFlow", "Admin")!),
        };
    }
}
