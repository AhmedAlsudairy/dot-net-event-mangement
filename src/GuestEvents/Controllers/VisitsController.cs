using GuestEvents.Data;
using GuestEvents.Models;
using GuestEvents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Controllers;

/// <summary>
/// External visits: staff request a bus to take students off campus.
/// Who approves, who confirms the bus and who gets the emails is configured in Settings > External visits.
/// </summary>
[Authorize(Policy = "Viewer")]
public class VisitsController : AppController
{
    private readonly AppDb _db;
    private readonly MailService _mail;
    private readonly SettingsService _settings;
    private readonly WorkflowService _wf;
    public VisitsController(AppDb db, MailService mail, SettingsService settings, WorkflowService wf) { _db = db; _mail = mail; _settings = settings; _wf = wf; }

    public async Task<IActionResult> Index()
    {
        var s = await _settings.AllAsync();
        var q = _db.Visits.AsNoTracking().AsQueryable();
        if (!SeesAll(s)) q = q.Where(v => v.RequesterUsername == Username);
        else q = q.Where(v => v.Status != VisitStatus.Draft || v.RequesterUsername == Username);
        return View(await q.OrderByDescending(v => v.Date).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var s = await _settings.AllAsync();
        var v = await _db.Visits.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (v == null || !(v.RequesterUsername == Username || (SeesAll(s) && v.Status != VisitStatus.Draft) || IsAdmin)) return NotFound();
        ViewBag.CanDecide = v.Status == VisitStatus.Submitted && SettingsService.IsVisitApprover(s, User);
        ViewBag.CanConfirm = v.Status == VisitStatus.Approved && SettingsService.IsVisitBusUser(s, User);
        return View(v);
    }

    [Authorize(Policy = "Requester")]
    public IActionResult Create() => View("Edit", new ExternalVisit { Date = DateTime.Today.AddDays(7) });

    [Authorize(Policy = "Requester")]
    public async Task<IActionResult> Edit(int id)
    {
        var v = await _db.Visits.FindAsync(id);
        if (v == null || !CanEdit(v)) return NotFound();
        return View(v);
    }

    [Authorize(Policy = "Requester"), HttpPost]
    public async Task<IActionResult> Save(ExternalVisit m, string submitAction)
    {
        ExternalVisit v;
        if (m.Id == 0)
        {
            v = new ExternalVisit { RequesterUsername = Username, RequesterName = DisplayName, RequesterDepartment = Department, RequesterEmail = UserEmail };
            _db.Visits.Add(v);
        }
        else
        {
            v = (await _db.Visits.FindAsync(m.Id))!;
            if (v == null || !CanEdit(v)) return NotFound();
        }
        ModelState.Remove("submitAction");
        if (m.ReturnTime <= m.DepartureTime) ModelState.AddModelError(nameof(m.ReturnTime), "Return time must be after departure time.");
        if (submitAction == "submit") await CheckNotice(m);
        if (!ModelState.IsValid) { if (m.Id == 0) _db.Entry(v).State = EntityState.Detached; return View("Edit", m); }

        v.Name = m.Name; v.Destination = m.Destination; v.DestinationAddress = m.DestinationAddress; v.Purpose = m.Purpose;
        v.Date = m.Date.Date; v.DepartureTime = m.DepartureTime; v.ReturnTime = m.ReturnTime; v.PickupLocation = m.PickupLocation;
        v.Grade = m.Grade; v.StudentCount = m.StudentCount; v.StaffCount = m.StaffCount;
        v.SupervisorName = m.SupervisorName; v.SupervisorPhone = m.SupervisorPhone; v.Notes = m.Notes;
        await _db.SaveChangesAsync();
        if (submitAction == "submit") await DoSubmit(v);
        else await _wf.Audit(Username, "SaveVisitDraft", $"Visit {v.Id}");
        return RedirectToAction(nameof(Details), new { id = v.Id });
    }

    [Authorize(Policy = "Requester"), HttpPost]
    public async Task<IActionResult> Submit(int id)
    {
        var v = await _db.Visits.FindAsync(id);
        if (v == null || !CanEdit(v)) return NotFound();
        await CheckNotice(v);
        if (!ModelState.IsValid) { TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(e => e.ErrorMessage)); return RedirectToAction(nameof(Details), new { id }); }
        await DoSubmit(v);
        return RedirectToAction(nameof(Details), new { id });
    }

    // Approver decision (people chosen in Settings)
    [HttpPost]
    public async Task<IActionResult> Decide(int id, string decision, string? comment)
    {
        var s = await _settings.AllAsync();
        if (!SettingsService.IsVisitApprover(s, User)) return Forbid();
        var v = await _db.Visits.FindAsync(id);
        if (v == null || v.Status != VisitStatus.Submitted) return NotFound();
        var reject = decision == "reject";
        if (reject && string.IsNullOrWhiteSpace(comment)) { TempData["Error"] = "CommentRequired"; return RedirectToAction(nameof(Details), new { id }); }
        v.Status = reject ? VisitStatus.Rejected : VisitStatus.Approved;
        v.DecisionBy = DisplayName; v.DecisionComment = comment?.Trim(); v.DecidedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _wf.Audit(Username, reject ? "RejectVisit" : "ApproveVisit", $"Visit {id}" + (reject ? $": {comment}" : ""));

        var link = $"{_wf.InternalBase}/Visits/Details/{v.Id}";
        if (SettingsService.Bool(s, "VisitNotifyRequester") && !string.IsNullOrEmpty(v.RequesterEmail))
            await _mail.SendAsync(null, new[] { v.RequesterEmail }, null, $"External visit {(reject ? "rejected" : "approved")}: {v.Name}",
                reject ? $"Your external visit request \"{v.Name}\" was rejected by {DisplayName}.\nReason: {comment}\n{link}"
                       : $"Your external visit request \"{v.Name}\" was approved by {DisplayName}. The bus will now be confirmed.\n{link}");
        if (!reject) await SendBusRequest(v, s, $"Approved by: {DisplayName}");
        return RedirectToAction(nameof(Details), new { id });
    }

    // Bus coordinator confirms the bus
    [HttpPost]
    public async Task<IActionResult> ConfirmBus(int id, string? busDetails)
    {
        var s = await _settings.AllAsync();
        if (!SettingsService.IsVisitBusUser(s, User)) return Forbid();
        var v = await _db.Visits.FindAsync(id);
        if (v == null || v.Status != VisitStatus.Approved) return NotFound();
        v.Status = VisitStatus.BusConfirmed; v.BusDetails = busDetails?.Trim();
        await _db.SaveChangesAsync();
        await _wf.Audit(Username, "ConfirmBus", $"Visit {id}");
        if (SettingsService.Bool(s, "VisitNotifyRequester") && !string.IsNullOrEmpty(v.RequesterEmail))
            await _mail.SendAsync(null, new[] { v.RequesterEmail }, null, $"Bus confirmed: {v.Name}",
                $"The bus for \"{v.Name}\" on {v.Date:yyyy-MM-dd} at {v.DepartureTime:hh\\:mm} is confirmed.\nBus details: {v.BusDetails}\n{_wf.InternalBase}/Visits/Details/{v.Id}");
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task DoSubmit(ExternalVisit v)
    {
        var s = await _settings.AllAsync();
        v.Status = VisitStatus.Submitted; v.SubmittedAt = DateTime.UtcNow;
        v.DecisionBy = null; v.DecisionComment = null; v.DecidedAt = null;
        var auto = !SettingsService.Bool(s, "VisitApprovalRequired");
        if (auto) { v.Status = VisitStatus.Approved; v.DecisionBy = "(no approval required)"; v.DecidedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync();
        await _wf.Audit(Username, "SubmitVisit", $"Visit {v.Id}");
        if (auto) { await SendBusRequest(v, s, "Approval is not required."); return; }

        var to = MailService.Split(s["VisitApproverEmail"]);
        if (to.Count == 0) to = new List<string> { !string.IsNullOrWhiteSpace(s["HodEmail"]) ? s["HodEmail"] : s["AdminMailbox"] };
        await _mail.SendAsync(null, to, null, $"External visit awaiting approval: {v.Name}", VisitSummary(v) + $"\n{_wf.InternalBase}/Visits/Details/{v.Id}");
    }

    private Task SendBusRequest(ExternalVisit v, Dictionary<string, string> s, string note)
    {
        var to = MailService.Split(s["VisitBusEmail"]);
        if (to.Count == 0) to = new[] { s["AdminMailbox"] }.Concat(MailService.Split(s["AdminExtraTo"])).ToList();
        return _mail.SendAsync(null, to, MailService.Split(s["VisitBusCc"]), $"Bus request: {v.Name} ({v.Date:yyyy-MM-dd})",
            VisitSummary(v) + $"\n{note}\nConfirm the bus: {_wf.InternalBase}/Visits/Details/{v.Id}");
    }

    private async Task CheckNotice(ExternalVisit v)
    {
        var min = SettingsService.Int(await _settings.AllAsync(), "VisitMinDaysAhead");
        if (v.Date.Date < DateTime.Today) ModelState.AddModelError("Date", "The visit date cannot be in the past.");
        else if (min > 0 && (v.Date.Date - DateTime.Today).TotalDays < min) ModelState.AddModelError("Date", $"A visit must be submitted at least {min} day(s) before the trip.");
    }

    private string VisitSummary(ExternalVisit v) =>
        $"External visit \"{v.Name}\" to {v.Destination}\nDate: {v.Date:yyyy-MM-dd}, depart {v.DepartureTime:hh\\:mm} from {v.PickupLocation}, return {v.ReturnTime:hh\\:mm}\n" +
        $"Students: {v.StudentCount}{(string.IsNullOrWhiteSpace(v.Grade) ? "" : " (" + v.Grade + ")")}, accompanying staff: {v.StaffCount}\n" +
        $"Supervisor: {v.SupervisorName} {v.SupervisorPhone}\nRequested by: {v.RequesterName}";

    private bool SeesAll(Dictionary<string, string> s) => IsAdmin || SettingsService.IsVisitApprover(s, User) || SettingsService.IsVisitBusUser(s, User);
    private bool CanEdit(ExternalVisit v) => v.RequesterUsername == Username && (v.Status == VisitStatus.Draft || v.Status == VisitStatus.Rejected);
}
