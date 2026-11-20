using System.Text.Json;
using Kpi.Dashboard.Web.Data;
using Kpi.Dashboard.Web.Models;
using Kpi.Dashboard.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kpi.Dashboard.Web.Pages.Tracking;

[Authorize(Policy = Permissions.TrackingView)]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly TrackingQueryService _query;
    private readonly TrackingSaveService _save;
    private readonly PermissionService _perms;

    private readonly GuestEvents.Services.AuditService _audit;
    public IndexModel(AppDbContext db, TrackingQueryService query, TrackingSaveService save, PermissionService perms, GuestEvents.Services.AuditService audit)
    {
        _audit = audit;
        _perms = perms;
        _db = db;
        _query = query;
        _save = save;
    }

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Plan { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int? EntityId { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? DateFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? DateTo { get; set; }

    public List<TrackingRow> Rows { get; set; } = new();
    public List<Entity> Entities { get; set; } = new();
    public int TotalCount { get; set; }
    public string RowsJson { get; set; } = "[]";
    public bool IsEntityScoped { get; set; }
    public bool CanAssign { get; set; }
    public bool CanEditAll { get; set; }
    public bool CanEditOwn { get; set; }
    public bool CanEditAnything => CanAssign || CanEditAll || (CanEditOwn && CurrentEntityId.HasValue);
    public int? CurrentEntityId { get; set; }

    public void OnGet()
    {
        Entities = _db.Entities.Where(e => e.IsActive).OrderBy(e => e.NameAr).ToList();
        var perms = _perms.For(User);
        CanAssign = perms.Contains(Permissions.TrackingAssign);
        CanEditAll = perms.Contains(Permissions.TrackingEditAll);
        CanEditOwn = perms.Contains(Permissions.TrackingEditOwn);
        CurrentEntityId = User.GetEntityId();

        // A department user lands on their own KPIs; picking "الكل" in the filter (which submits an
        // empty EntityId) still shows everything.
        IsEntityScoped = _perms.IsEntityScoped(User) && CurrentEntityId.HasValue;
        if (IsEntityScoped && !Request.Query.ContainsKey("EntityId")) EntityId = CurrentEntityId;

        PlanType? planFilter = Plan switch
        {
            "operational" => PlanType.Operational,
            "annual" => PlanType.Annual,
            "ejada" => PlanType.Ejada,
            _ => null
        };

        var filters = new TrackingFilters
        {
            Search = Search,
            Plan = planFilter,
            Status = string.IsNullOrEmpty(Status) || Status == "all" ? null : Status,
            EntityId = EntityId,
            DateFrom = DateFrom,
            DateTo = DateTo
        };

        Rows = _query.GetFiltered(filters);
        TotalCount = _db.Kpis.Count();

        RowsJson = JsonSerializer.Serialize(Rows.Select(r => new
        {
            kpiId = r.Kpi.Id,
            sn = r.Kpi.Sn,
            plans = r.ItemsByPlan.ToDictionary(
                kv => kv.Key.ToString(),
                kv => kv.Value.Select(it => new
                {
                    entityId = it.EntityId,
                    target = it.Target,
                    deadline = it.Deadline?.ToString("yyyy-MM-dd"),
                    status = it.Status?.ToString()
                }))
        }));
    }

    private IActionResult LogSaved(SaveTrackingRequest r)
    {
        var kpi = _db.Kpis.AsNoTracking().Where(k => k.Id == r.KpiId).Select(k => k.Sn).FirstOrDefault() ?? r.KpiId.ToString();
        var ents = _db.Entities.AsNoTracking().ToDictionary(e => e.Id, e => e.NameAr);
        var parts = r.Items.Select(i => $"{(ents.TryGetValue(i.EntityId, out var n) ? n : "#" + i.EntityId)}: {(string.IsNullOrEmpty(i.Status) ? "no status" : i.Status)}" + (string.IsNullOrWhiteSpace(i.Target) ? "" : $", target {i.Target}") + (string.IsNullOrEmpty(i.Deadline) ? "" : $", due {i.Deadline}"));
        _audit.Log(User.Identity?.Name ?? "?", "TrackingSaved", $"KPI {kpi}, {r.Plan} plan: " + (r.Items.Count == 0 ? "no departments assigned" : string.Join(" | ", parts)));
        return new JsonResult(new { ok = true });
    }

    public IActionResult OnPostSave([FromBody] SaveTrackingRequest? request)
    {
        if (request is null) return BadRequest(new { ok = false, message = "طلب غير صالح." });

        var outcome = _save.Save(request, _perms.For(User), User.GetEntityId(), User.GetUserId());
        return outcome switch
        {
            SaveOutcome.Saved => LogSaved(request),
            SaveOutcome.Forbidden => Forbid(),
            _ => BadRequest(new { ok = false, message = "طلب غير صالح." })
        };
    }
}
