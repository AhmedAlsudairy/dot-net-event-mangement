using ClosedXML.Excel;
using GuestEvents.Data;
using GuestEvents.Models;
using GuestEvents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Controllers;

public record EventStat(EventRequest Event, int Attendees, int Evaluations, double? Overall, double? Presenter, double? Content, double? Venue, double? Food)
{
    public double Rate => Attendees == 0 ? 0 : Math.Round(100.0 * Evaluations / Attendees, 1);
}
public record PresenterStat(string Presenter, int Events, int Evaluations, double? Overall);

[Authorize(Policy = "Admin")]
public class ReportsController : AppController
{
    private readonly AppDb _db;
    private readonly WorkflowService _wf;
    public ReportsController(AppDb db, WorkflowService wf) { _db = db; _wf = wf; }

    public async Task<IActionResult> Index()
    {
        var events = await _db.Events.AsNoTracking().Where(e => EventStatuses.Live.Contains(e.Status)).OrderByDescending(e => e.Date).ToListAsync();
        var stats = new List<EventStat>();
        foreach (var e in events) stats.Add(await Stat(e));
        ViewBag.Presenters = stats.GroupBy(x => x.Event.Presenter).Select(g => new PresenterStat(g.Key, g.Count(), g.Sum(x => x.Evaluations),
            g.Sum(x => x.Evaluations) == 0 ? null : Math.Round(g.Where(x => x.Overall != null).Sum(x => x.Overall!.Value * x.Evaluations) / g.Sum(x => x.Evaluations), 2))).OrderByDescending(p => p.Overall).ToList();
        return View(stats);
    }

    public async Task<IActionResult> Event(int id)
    {
        var e = await _db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        ViewBag.Stat = await Stat(e);
        ViewBag.PerDay = (await _db.Attendances.AsNoTracking().Where(a => a.EventRequestId == id).GroupBy(a => a.Day).Select(g => new { Day = g.Key, N = g.Count() }).ToListAsync())
            .ToDictionary(x => x.Day.Date, x => x.N);
        ViewBag.Attendance = await _db.Attendances.AsNoTracking().Where(a => a.EventRequestId == id).OrderBy(a => a.CheckedInAt).ToListAsync();
        return View(e);
    }

    // Live count for polling on the event page.
    public async Task<IActionResult> Live(int id) => Json(new
    {
        attendees = await _db.Attendances.CountAsync(a => a.EventRequestId == id),
        evaluations = await _db.Evaluations.CountAsync(a => a.EventRequestId == id),
    });

    public async Task<IActionResult> ExportAttendance(int id)
    {
        var e = await _db.Events.FindAsync(id);
        if (e == null) return NotFound();
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Attendance");
        string[] h = { "Name", "Organization", "Email", "Phone", "Checked in", "Event day" };
        for (int i = 0; i < h.Length; i++) ws.Cell(1, i + 1).SetValue(h[i]).Style.Font.Bold = true;
        int r = 2;
        foreach (var a in await _db.Attendances.AsNoTracking().Where(a => a.EventRequestId == id).OrderBy(a => a.CheckedInAt).ToListAsync())
        {
            ws.Cell(r, 1).SetValue(Safe(a.FullName)); ws.Cell(r, 2).SetValue(Safe(a.Organization));
            ws.Cell(r, 3).SetValue(Safe(a.Email)); ws.Cell(r, 4).SetValue(Safe(a.Phone));
            ws.Cell(r, 5).SetValue(a.CheckedInAt.ToLocalTime()); ws.Cell(r, 6).SetValue(a.Day.ToString("yyyy-MM-dd")); r++;
        }
        ws.Columns().AdjustToContents();
        await _wf.Audit(Username, "Export", $"Attendance event {id}");
        return Xlsx(wb, $"attendance-{id}.xlsx");
    }

    public async Task<IActionResult> ExportEvaluations(int? id)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Evaluations");
        string[] h = { "Event", "Presenter", "Overall", "Presenter quality", "Content", "Venue", "Food", "Comments", "Submitted" };
        for (int i = 0; i < h.Length; i++) ws.Cell(1, i + 1).SetValue(h[i]).Style.Font.Bold = true;
        var q = _db.Evaluations.AsNoTracking().Where(x => id == null || x.EventRequestId == id)
            .Join(_db.Events, ev => ev.EventRequestId, e => e.Id, (ev, e) => new { ev, e }).OrderBy(x => x.e.Id).ThenBy(x => x.ev.SubmittedAt);
        int r = 2;
        foreach (var x in await q.ToListAsync())
        {
            ws.Cell(r, 1).SetValue(Safe(x.e.Name)); ws.Cell(r, 2).SetValue(Safe(x.e.Presenter));
            ws.Cell(r, 3).SetValue(x.ev.Overall); ws.Cell(r, 4).SetValue(x.ev.Presenter); ws.Cell(r, 5).SetValue(x.ev.Content); ws.Cell(r, 6).SetValue(x.ev.VenueOrg);
            if (x.ev.Food != null) ws.Cell(r, 7).SetValue(x.ev.Food.Value);
            ws.Cell(r, 8).SetValue(Safe(x.ev.Comments)); ws.Cell(r, 9).SetValue(x.ev.SubmittedAt.ToLocalTime()); r++;
        }
        ws.Columns().AdjustToContents();
        await _wf.Audit(Username, "Export", $"Evaluations {(id?.ToString() ?? "all")}");
        return Xlsx(wb, "evaluations.xlsx");
    }

    public async Task<IActionResult> ExportEvents()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Events");
        string[] h = { "Id", "Name", "Type", "Status", "Date", "Start", "End", "Venue", "Presenter", "Company", "Expected attendees", "Requester", "Department", "KPI", "Submitted", "Approvals (step - person - decision - date)", "Attendees", "Evaluations" };
        for (int i = 0; i < h.Length; i++) ws.Cell(1, i + 1).SetValue(h[i]).Style.Font.Bold = true;
        var events = await _db.Events.AsNoTracking().OrderByDescending(e => e.Date).ToListAsync();
        var approvals = (await _db.Approvals.AsNoTracking().OrderBy(a => a.DecidedAt).ToListAsync()).GroupBy(a => a.EventRequestId).ToDictionary(g => g.Key, g => g.ToList());
        var att = await _db.Attendances.AsNoTracking().GroupBy(a => a.EventRequestId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var evs = await _db.Evaluations.AsNoTracking().GroupBy(a => a.EventRequestId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        int r = 2;
        foreach (var e in events)
        {
            var hist = approvals.TryGetValue(e.Id, out var l) ? string.Join("\n", l.Select(a => $"{a.StepName ?? "-"} - {a.ApproverName} - {(a.Approved ? "approved" : "rejected")} - {a.DecidedAt.ToLocalTime():yyyy-MM-dd HH:mm}")) : "";
            object[] v = { e.Id, Safe(e.Name), Safe(e.Type.ToString()), e.Status.ToString(), e.Date.ToString("yyyy-MM-dd"), e.StartTime.ToString(@"hh\:mm"), e.EndTime.ToString(@"hh\:mm"), Safe(e.Venue), Safe(e.Presenter), Safe(e.CompanyName), e.ExpectedAttendees,
                Safe(e.RequesterName), Safe(e.RequesterDepartment), Safe(e.KpiLabel), e.SubmittedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "", Safe(hist), att.GetValueOrDefault(e.Id), evs.GetValueOrDefault(e.Id) };
            for (int c = 0; c < v.Length; c++) ws.Cell(r, c + 1).SetValue(XLCellValue.FromObject(v[c]));
            r++;
        }
        ws.Columns().AdjustToContents(); ws.Column(16).Width = 60; ws.Column(16).Style.Alignment.WrapText = true;
        await _wf.Audit(Username, "Export", "Events");
        return Xlsx(wb, "events.xlsx");
    }

    public async Task<IActionResult> ExportVisits()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Visits");
        string[] h = { "Id", "Name", "Destination", "Status", "Date", "Departure", "Return", "Pick-up", "Grade", "Students", "Staff", "Supervisor", "Supervisor phone", "Requester", "Department", "KPI", "Decision by", "Decision comment", "Bus details" };
        for (int i = 0; i < h.Length; i++) ws.Cell(1, i + 1).SetValue(h[i]).Style.Font.Bold = true;
        int r = 2;
        foreach (var x in await _db.Visits.AsNoTracking().OrderByDescending(v => v.Date).ToListAsync())
        {
            object[] v = { x.Id, Safe(x.Name), Safe(x.Destination), x.Status.ToString(), x.Date.ToString("yyyy-MM-dd"), x.DepartureTime.ToString(@"hh\:mm"), x.ReturnTime.ToString(@"hh\:mm"), Safe(x.PickupLocation), Safe(x.Grade), x.StudentCount, x.StaffCount,
                Safe(x.SupervisorName), Safe(x.SupervisorPhone), Safe(x.RequesterName), Safe(x.RequesterDepartment), Safe(x.KpiLabel), Safe(x.DecisionBy), Safe(x.DecisionComment), Safe(x.BusDetails) };
            for (int c = 0; c < v.Length; c++) ws.Cell(r, c + 1).SetValue(XLCellValue.FromObject(v[c]));
            r++;
        }
        ws.Columns().AdjustToContents();
        await _wf.Audit(Username, "Export", "Visits");
        return Xlsx(wb, "visits.xlsx");
    }

    private async Task<EventStat> Stat(EventRequest e)
    {
        var att = await _db.Attendances.CountAsync(a => a.EventRequestId == e.Id);
        var ev = await _db.Evaluations.AsNoTracking().Where(a => a.EventRequestId == e.Id).ToListAsync();
        double? Avg(IEnumerable<int> v) { var l = v.ToList(); return l.Count == 0 ? null : Math.Round(l.Average(), 2); }
        return new EventStat(e, att, ev.Count, Avg(ev.Select(x => x.Overall)), Avg(ev.Select(x => x.Presenter)), Avg(ev.Select(x => x.Content)),
            Avg(ev.Select(x => x.VenueOrg)), Avg(ev.Where(x => x.Food != null).Select(x => x.Food!.Value)));
    }

    // Guards against spreadsheet formula injection from public input.
    private static string Safe(string? s) => string.IsNullOrEmpty(s) ? "" : "=+-@".Contains(s[0]) ? "'" + s : s;

    private FileContentResult Xlsx(XLWorkbook wb, string name)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }
}
