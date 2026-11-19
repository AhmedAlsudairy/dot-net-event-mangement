using System.Security.Claims;
using GuestEvents.Data;
using GuestEvents.Models;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Services;

/// <summary>
/// The configurable event approval chain: which steps exist, in which order, who may decide at each step and how many
/// different approvals each step needs. An Admin edits it at Admin > Approval flow.
/// </summary>
public class ApprovalChain
{
    private readonly AppDb _db;
    public ApprovalChain(AppDb db) { _db = db; }

    public async Task<List<ApprovalStep>> StepsAsync(bool enabledOnly = true)
    {
        var q = _db.ApprovalSteps.AsNoTracking().AsQueryable();
        if (enabledOnly) q = q.Where(s => s.Enabled);
        return await q.OrderBy(s => s.Order).ThenBy(s => s.Id).ToListAsync();
    }

    /// <summary>Creates the default two-step chain (HOD, then Dean) the first time, taking over the old HOD/Dean settings.</summary>
    public static async Task EnsureDefaultsAsync(AppDb db, SettingsService settings)
    {
        if (await db.ApprovalSteps.AnyAsync()) return;
        var s = await settings.AllAsync();
        db.ApprovalSteps.Add(new ApprovalStep { Order = 1, Name = "HOD", Role = Roles.Hod, Approvers = Blank(s.GetValueOrDefault("HodUsername")), NotifyEmail = Blank(s.GetValueOrDefault("HodEmail")), RequiredApprovals = 1 });
        db.ApprovalSteps.Add(new ApprovalStep { Order = 2, Name = "Dean", Role = Roles.Dean, Approvers = Blank(s.GetValueOrDefault("DeanUsername")), NotifyEmail = Blank(s.GetValueOrDefault("DeanEmail")), RequiredApprovals = 1 });
        await db.SaveChangesAsync();
    }
    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    /// <summary>May this person decide at this step? Named people win; with none named, the role decides.</summary>
    public static bool IsEligible(ApprovalStep step, ClaimsPrincipal u)
    {
        if (!string.IsNullOrWhiteSpace(step.Approvers)) return SettingsService.InUserList(step.Approvers, u);
        return !string.IsNullOrWhiteSpace(step.Role) && u.IsInRole(step.Role);
    }

    /// <summary>May this person decide on this request right now? Eligible for the step, not the requester (nobody approves their own request), and not already voted at this step.</summary>
    public static bool CanDecide(EventRequest e, ApprovalStep step, ClaimsPrincipal u)
    {
        var me = u.Identity?.Name ?? "";
        return IsEligible(step, u)
            && !e.RequesterUsername.Equals(me, StringComparison.OrdinalIgnoreCase)
            && !Given(e, step).Any(a => a.ApproverUsername.Equals(me, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The step a submitted request is waiting at (the first enabled step at or after its stored step; the first step if none stored).</summary>
    public static ApprovalStep? Current(EventRequest e, List<ApprovalStep> steps)
    {
        if (steps.Count == 0) return null;
        if (e.CurrentStepId is int id)
        {
            var at = steps.FirstOrDefault(s => s.Id == id);
            if (at != null) return at;
        }
        return steps[0];
    }

    public static ApprovalStep? Next(ApprovalStep cur, List<ApprovalStep> steps) =>
        steps.Where(s => s.Order > cur.Order || (s.Order == cur.Order && s.Id > cur.Id)).OrderBy(s => s.Order).ThenBy(s => s.Id).FirstOrDefault();

    /// <summary>Approvals already given at this step in the current submission round.</summary>
    public static List<Approval> Given(EventRequest e, ApprovalStep step) =>
        e.Approvals.Where(a => a.Approved && a.StepId == step.Id && a.DecidedAt >= (e.SubmittedAt ?? DateTime.MinValue)).ToList();

    /// <summary>Submitted requests that wait for a decision from this person (not those they already approved at the current step).</summary>
    public async Task<List<EventRequest>> PendingForAsync(ClaimsPrincipal u)
    {
        var steps = await StepsAsync();
        if (steps.Count == 0) return new();
        var open = await _db.Events.AsNoTracking().Include(e => e.Approvals).Where(e => e.Status == EventStatus.Submitted).OrderBy(e => e.SubmittedAt).ToListAsync();
        return open.Where(e => Current(e, steps) is { } cur && CanDecide(e, cur, u)).ToList();
    }

    public async Task<bool> IsApproverAsync(ClaimsPrincipal u) => (await StepsAsync()).Any(s => IsEligible(s, u));
}
