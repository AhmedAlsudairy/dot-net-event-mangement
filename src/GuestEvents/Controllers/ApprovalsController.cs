using GuestEvents.Data;
using GuestEvents.Models;
using GuestEvents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Controllers;

[Authorize(Policy = "Hod")]
public class ApprovalsController : AppController
{
    private readonly AppDb _db;
    private readonly WorkflowService _wf;
    private readonly SettingsService _settings;
    public ApprovalsController(AppDb db, WorkflowService wf, SettingsService settings) { _db = db; _wf = wf; _settings = settings; }

    public async Task<IActionResult> Index()
    {
        ViewBag.Allowed = await IsAssignedHod();
        return View(await _db.Events.AsNoTracking().Where(e => e.Status == EventStatus.Submitted).OrderBy(e => e.SubmittedAt).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Decide(int id, string decision, string? comment)
    {
        if (!await IsAssignedHod()) return Forbid();
        var e = await _db.Events.FindAsync(id);
        if (e == null || e.Status != EventStatus.Submitted) return NotFound();
        var me = new AdUserLite(Username, DisplayName);
        if (decision == "reject")
        {
            if (string.IsNullOrWhiteSpace(comment)) { TempData["Error"] = "CommentRequired"; return RedirectToAction(nameof(Index)); }
            await _wf.Reject(e, me, comment.Trim());
        }
        else await _wf.Approve(e, me, comment?.Trim());
        return RedirectToAction(nameof(Index));
    }

    // "One HOD": when Settings.HodUsername is set only that user may decide, otherwise any Events-HOD member.
    private async Task<bool> IsAssignedHod()
    {
        var hod = (await _settings.AllAsync())["HodUsername"];
        return string.IsNullOrWhiteSpace(hod) || hod.Equals(Username, StringComparison.OrdinalIgnoreCase);
    }
}
