using GuestEvents.Data;
using GuestEvents.Models;
using GuestEvents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Controllers;

/// <summary>
/// Event approvals follow the chain the Admin configured (Admin > Approval flow): steps in order, each with its own approvers and
/// a number of approvals it needs. A person sees and may decide only the requests waiting at a step they are eligible for.
/// </summary>
[Authorize(Policy = "EventsUser")]
public class ApprovalsController : AppController
{
    private readonly AppDb _db;
    private readonly WorkflowService _wf;
    private readonly ApprovalChain _chain;
    public ApprovalsController(AppDb db, WorkflowService wf, ApprovalChain chain) { _db = db; _wf = wf; _chain = chain; }

    public async Task<IActionResult> Index()
    {
        ViewBag.Steps = await _chain.StepsAsync();
        if (!await _chain.IsApproverAsync(User)) return Forbid();
        return View(await _chain.PendingForAsync(User));
    }

    [HttpPost]
    public async Task<IActionResult> Decide(int id, string decision, string? comment)
    {
        var steps = await _chain.StepsAsync();
        var e = await _db.Events.Include(x => x.Approvals).FirstOrDefaultAsync(x => x.Id == id);
        if (e == null || e.Status != EventStatus.Submitted) return NotFound();
        var step = ApprovalChain.Current(e, steps);
        if (step == null || !ApprovalChain.IsEligible(step, User) || e.RequesterUsername.Equals(Username, StringComparison.OrdinalIgnoreCase)) return Forbid();   // not your step, or your own request
        if (!ApprovalChain.CanDecide(e, step, User)) { TempData["Error"] = "AlreadyApproved"; return RedirectToAction(nameof(Index)); }                   // one vote per person per step

        var me = new AdUserLite(Username, DisplayName);
        if (decision == "reject")
        {
            if (string.IsNullOrWhiteSpace(comment)) { TempData["Error"] = "CommentRequired"; return RedirectToAction(nameof(Index)); }
            await _wf.Reject(e, me, comment.Trim());
        }
        else await _wf.ApproveStep(e, step, steps, me, comment?.Trim());
        return RedirectToAction(nameof(Index));
    }
}
