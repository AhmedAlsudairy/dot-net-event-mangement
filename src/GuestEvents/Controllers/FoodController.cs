using GuestEvents.Data;
using GuestEvents.Models;
using GuestEvents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Controllers;

[Authorize(Policy = "Viewer")]
public class FoodController : AppController
{
    private readonly AppDb _db;
    private readonly MailService _mail;
    private readonly SettingsService _settings;
    private readonly WorkflowService _wf;
    public FoodController(AppDb db, MailService mail, SettingsService settings, WorkflowService wf) { _db = db; _mail = mail; _settings = settings; _wf = wf; }

    [Authorize(Policy = "Requester")]
    public async Task<IActionResult> Create(int eventId)
    {
        var e = await Owned(eventId);
        if (e == null) return NotFound();
        return View(new FoodRequest { EventRequestId = e.Id, People = e.ExpectedAttendees, Location = e.Venue, DeliveryTime = e.StartTime.Add(TimeSpan.FromMinutes(-30)) });
    }

    [Authorize(Policy = "Requester"), HttpPost]
    public async Task<IActionResult> Create(FoodRequest m)
    {
        var e = await Owned(m.EventRequestId);
        if (e == null) return NotFound();
        ModelState.Remove("EventRequest");
        if (!ModelState.IsValid) return View(m);
        m.Status = FoodStatus.Submitted; m.RequestedBy = DisplayName; m.CreatedAt = DateTime.UtcNow; m.Id = 0;
        _db.FoodRequests.Add(m);
        await _db.SaveChangesAsync();

        var s = await _settings.AllAsync();
        var to = new[] { s["AdminMailbox"] }.Concat(MailService.Split(s["AdminExtraTo"]));
        var body = $"Food request for \"{e.Name}\" ({e.Date:yyyy-MM-dd}{(e.IsMultiDay ? " - " + e.LastDate.ToString("yyyy-MM-dd") : "")}, {e.Venue})\nPeople: {m.People}\nMeal: {m.Meal}\nDelivery: {m.DeliveryTime:hh\\:mm} at {m.Location}\nNotes: {m.Notes}\nRequested by: {DisplayName}\n{_wf.InternalBase}/Food";
        await _mail.SendAsync(e.Id, to, MailService.Split(s["AdminCc"]), $"Food request: {e.Name}", body);
        await _wf.Audit(Username, "FoodRequest", $"Event {e.Id}");
        return RedirectToAction("Details", "Events", new { id = e.Id });
    }

    // Admin: list and confirm / reject
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Index() =>
        View(await _db.FoodRequests.Include(f => f.EventRequest).AsNoTracking().OrderByDescending(f => f.CreatedAt).ToListAsync());

    [Authorize(Policy = "Admin"), HttpPost]
    public async Task<IActionResult> SetStatus(int id, FoodStatus status)
    {
        var f = await _db.FoodRequests.Include(x => x.EventRequest).FirstOrDefaultAsync(x => x.Id == id);
        if (f == null) return NotFound();
        f.Status = status;
        await _db.SaveChangesAsync();
        await _wf.Audit(Username, "FoodStatus", $"Food {id} -> {status}");
        if (f.EventRequest?.RequesterEmail is { Length: > 0 } email)
            await _mail.SendAsync(f.EventRequestId, new[] { email }, null, $"Food request {status}: {f.EventRequest.Name}", $"Your food request for \"{f.EventRequest.Name}\" is now: {status}.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<EventRequest?> Owned(int eventId)
    {
        var e = await _db.Events.FindAsync(eventId);
        if (e == null || e.Status < EventStatus.Approved || e.Status == EventStatus.Rejected) return null; // only approved events
        return e.RequesterUsername == Username || IsAdmin ? e : null;
    }
}
