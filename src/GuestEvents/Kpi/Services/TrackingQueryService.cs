using Kpi.Dashboard.Web.Data;
using Kpi.Dashboard.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Kpi.Dashboard.Web.Services;

public class TrackingFilters
{
    public string? Search { get; set; }
    public PlanType? Plan { get; set; }
    /// <summary>"Done" / "InProgress" / "NotStarted" / "__none__" (unassigned) / null (any).</summary>
    public string? Status { get; set; }
    public int? EntityId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}

public class TrackingRow
{
    public Models.Kpi Kpi { get; set; } = null!;
    public Dictionary<PlanType, List<TrackingItem>> ItemsByPlan { get; set; } = new();
}

/// <summary>Port of recordMatchesFilters / renderTable's data prep from the original dashboard JS.</summary>
public class TrackingQueryService
{
    private static readonly PlanType[] AllPlans = Enum.GetValues<PlanType>();
    private readonly AppDbContext _db;

    public TrackingQueryService(AppDbContext db)
    {
        _db = db;
    }

    public List<TrackingRow> GetFiltered(TrackingFilters f)
    {
        var kpis = _db.Kpis.AsNoTracking().ToList()
            .OrderBy(k => int.TryParse(k.Sn, out var n) ? n : int.MaxValue).ThenBy(k => k.Sn).ToList();
        var allItems = _db.TrackingItems.AsNoTracking().ToList();
        var itemsByKpiPlan = allItems.GroupBy(t => (t.KpiId, t.Plan)).ToDictionary(g => g.Key, g => g.ToList());

        List<TrackingItem> ItemsFor(int kpiId, PlanType plan) =>
            itemsByKpiPlan.TryGetValue((kpiId, plan), out var list) ? list : new List<TrackingItem>();

        bool Matches(Models.Kpi k)
        {
            if (!string.IsNullOrWhiteSpace(f.Search))
            {
                var hay = string.Join(' ', k.KpiText, k.Domain, k.Goal, k.Objective, k.Classification).ToLowerInvariant();
                if (!hay.Contains(f.Search.Trim().ToLowerInvariant())) return false;
            }

            var plansToCheck = f.Plan.HasValue ? new[] { f.Plan.Value } : AllPlans;

            if (!string.IsNullOrEmpty(f.Status))
            {
                bool anyPlanMatches = plansToCheck.Any(p =>
                {
                    var hasText = !string.IsNullOrWhiteSpace(k.PlanText(p));
                    var items = ItemsFor(k.Id, p);
                    if (f.Status == "__none__") return hasText && items.Count == 0;
                    return items.Any(it => it.Status.HasValue && it.Status.Value.ToString() == f.Status);
                });
                if (!anyPlanMatches) return false;
            }

            if (f.EntityId.HasValue)
            {
                var anyPlanMatches = plansToCheck.Any(p => ItemsFor(k.Id, p).Any(it => it.EntityId == f.EntityId.Value));
                if (!anyPlanMatches) return false;
            }

            if (f.DateFrom.HasValue || f.DateTo.HasValue)
            {
                var anyPlanMatches = plansToCheck.Any(p => ItemsFor(k.Id, p).Any(it =>
                {
                    if (!it.Deadline.HasValue) return false;
                    if (f.DateFrom.HasValue && it.Deadline.Value < f.DateFrom.Value) return false;
                    if (f.DateTo.HasValue && it.Deadline.Value > f.DateTo.Value) return false;
                    return true;
                }));
                if (!anyPlanMatches) return false;
            }

            return true;
        }

        return kpis.Where(Matches).Select(k => new TrackingRow
        {
            Kpi = k,
            ItemsByPlan = AllPlans.ToDictionary(p => p, p => ItemsFor(k.Id, p))
        }).ToList();
    }
}
