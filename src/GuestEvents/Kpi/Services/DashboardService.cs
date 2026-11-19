using Kpi.Dashboard.Web.Data;
using Kpi.Dashboard.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Kpi.Dashboard.Web.Services;

public class PlanStats
{
    public int Total { get; set; }
    public int Done { get; set; }
    public int Prog { get; set; }
    public int Low { get; set; }
    public int NoStatus { get; set; }
    public int OverallPct { get; set; }
}

public class AlignmentOverview
{
    public int Total { get; set; }
    public int EjadaCount { get; set; }
    public int AnnualCount { get; set; }
    public int AccreditationCount { get; set; }
}

public class AtRiskRow
{
    public string Sn { get; set; } = "";
    public string KpiText { get; set; } = "";
    public string EntityName { get; set; } = "";
    public PlanType Plan { get; set; }
    public DateOnly Deadline { get; set; }
    public int DaysOverdue { get; set; }
    public ExecutionStatus? Status { get; set; }
}

public class EntityScoreRow
{
    public string EntityName { get; set; } = "";
    public int AssignedCount { get; set; }
    public int CompletionPct { get; set; }
}

public class AssignmentCoverage
{
    public int Assigned { get; set; }
    public int Unassigned { get; set; }
}

/// <summary>
/// Ports computePlanStats / countBy / renderAlignmentOverview from the original
/// dashboard's JS into LINQ-over-EF. "Every assigned entity counts as one
/// tracking item; a KPI/plan with plan text but no entity yet counts once as
/// unassigned" — same rule as the source app.
/// </summary>
public class DashboardService
{
    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db)
    {
        _db = db;
    }

    private static readonly PlanType[] AllPlans = Enum.GetValues<PlanType>();

    public PlanStats ComputePlanStats(int? scopeEntityId, PlanType? planFilter)
    {
        var plans = planFilter.HasValue ? new[] { planFilter.Value } : AllPlans;
        var kpis = _db.Kpis.AsNoTracking().ToList();

        var itemsQuery = _db.TrackingItems.AsNoTracking().Where(t => plans.Contains(t.Plan));
        if (scopeEntityId.HasValue) itemsQuery = itemsQuery.Where(t => t.EntityId == scopeEntityId.Value);
        var itemsByKpiPlan = itemsQuery.ToList()
            .GroupBy(t => (t.KpiId, t.Plan))
            .ToDictionary(g => g.Key, g => g.ToList());

        var stats = new PlanStats();
        int sumPct = 0, cntPct = 0;

        foreach (var kpi in kpis)
        {
            foreach (var plan in plans)
            {
                if (string.IsNullOrWhiteSpace(kpi.PlanText(plan))) continue;

                var items = itemsByKpiPlan.TryGetValue((kpi.Id, plan), out var list) ? list : new List<TrackingItem>();

                // An entity-scoped dashboard only counts KPIs actually assigned to that entity.
                if (scopeEntityId.HasValue && items.Count == 0) continue;

                if (items.Count == 0) { stats.Total++; continue; }

                foreach (var it in items)
                {
                    stats.Total++;
                    switch (it.Status)
                    {
                        case ExecutionStatus.Done: stats.Done++; sumPct += 100; cntPct++; break;
                        case ExecutionStatus.InProgress: stats.Prog++; sumPct += 50; cntPct++; break;
                        case ExecutionStatus.NotStarted: stats.Low++; sumPct += 0; cntPct++; break;
                        default: stats.NoStatus++; break;
                    }
                }
            }
        }

        stats.OverallPct = cntPct > 0 ? (int)Math.Round(sumPct / (double)cntPct) : 0;
        return stats;
    }

    public AlignmentOverview ComputeAlignmentOverview(int? scopeEntityId)
    {
        var kpis = ScopedKpis(scopeEntityId);
        return new AlignmentOverview
        {
            Total = kpis.Count,
            EjadaCount = kpis.Count(k => !string.IsNullOrWhiteSpace(k.EjadaPlanText)),
            AnnualCount = kpis.Count(k => !string.IsNullOrWhiteSpace(k.AnnualPlanText)),
            AccreditationCount = kpis.Count(k => !string.IsNullOrWhiteSpace(k.AccreditationCriteria))
        };
    }

    public Dictionary<string, int> CountByDomain(int? scopeEntityId) =>
        CountBy(ScopedKpis(scopeEntityId), k => Shorten(k.Domain, 35));

    public Dictionary<string, int> CountByGoal(int? scopeEntityId) =>
        CountBy(ScopedKpis(scopeEntityId), k => Shorten(k.Goal, 45));

    public Dictionary<string, int> CountByClassification(int? scopeEntityId) =>
        CountBy(ScopedKpis(scopeEntityId), k => string.IsNullOrWhiteSpace(k.Classification) ? "Not specified" : k.Classification);

    /// <summary>KPIs visible to this scope: everything for the global dashboard,
    /// or only KPIs that actually have a tracking item for this entity.</summary>
    private List<Kpi.Dashboard.Web.Models.Kpi> ScopedKpis(int? scopeEntityId)
    {
        if (!scopeEntityId.HasValue) return _db.Kpis.AsNoTracking().ToList();
        var kpiIds = _db.TrackingItems.Where(t => t.EntityId == scopeEntityId.Value).Select(t => t.KpiId).Distinct().ToList();
        return _db.Kpis.AsNoTracking().Where(k => kpiIds.Contains(k.Id)).ToList();
    }

    private static Dictionary<string, int> CountBy(List<Kpi.Dashboard.Web.Models.Kpi> kpis, Func<Kpi.Dashboard.Web.Models.Kpi, string> key)
    {
        var result = new Dictionary<string, int>();
        foreach (var k in kpis)
        {
            var v = key(k);
            if (string.IsNullOrWhiteSpace(v)) v = "Not specified";
            result[v] = result.GetValueOrDefault(v) + 1;
        }
        return result;
    }

    private static string Shorten(string v, int max) =>
        string.IsNullOrWhiteSpace(v) ? "Not specified" : (v.Length > max ? v[..max] + "..." : v);

    private static readonly Dictionary<PlanType, string> PlanLabelAr = new()
    {
        [PlanType.Operational] = "الخطة التشغيلية",
        [PlanType.Annual] = "الخطة السنوية",
        [PlanType.Ejada] = "الإجادة المؤسسية"
    };

    /// <summary>Overdue = deadline has passed and the item isn't Done yet
    /// (includes NotStarted, InProgress, and never-set status). Ordered by
    /// how overdue it is, worst first.</summary>
    public List<AtRiskRow> GetAtRiskItems(int? scopeEntityId, int take = 50)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _db.TrackingItems.AsNoTracking()
            .Include(t => t.KpiRef)
            .Include(t => t.EntityRef)
            .Where(t => t.Deadline.HasValue && t.Deadline.Value < today && t.Status != ExecutionStatus.Done);
        if (scopeEntityId.HasValue) query = query.Where(t => t.EntityId == scopeEntityId.Value);

        return query.ToList()
            .Select(t => new AtRiskRow
            {
                Sn = t.KpiRef?.Sn ?? "",
                KpiText = t.KpiRef?.KpiText ?? "",
                EntityName = t.EntityRef?.NameAr ?? "",
                Plan = t.Plan,
                Deadline = t.Deadline!.Value,
                DaysOverdue = today.DayNumber - t.Deadline!.Value.DayNumber,
                Status = t.Status
            })
            .OrderByDescending(r => r.DaysOverdue)
            .Take(take)
            .ToList();
    }

    public string PlanLabel(PlanType plan) => PlanLabelAr[plan];

    /// <summary>Completion % per entity (Done=100/InProgress=50/NotStarted=0,
    /// items with no status yet excluded from the average), for entities that
    /// have at least one assigned tracking item. Ranked worst-to-best so the
    /// dashboard can highlight who needs attention.</summary>
    public List<EntityScoreRow> GetEntityScorecard()
    {
        var items = _db.TrackingItems.AsNoTracking().Include(t => t.EntityRef).ToList();
        return items
            .GroupBy(t => t.EntityRef?.NameAr ?? "")
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .Select(g =>
            {
                var scored = g.Where(t => t.Status.HasValue).ToList();
                var pct = scored.Count > 0
                    ? (int)Math.Round(scored.Average(t => t.Status switch { ExecutionStatus.Done => 100, ExecutionStatus.InProgress => 50, _ => 0 }))
                    : 0;
                return new EntityScoreRow { EntityName = g.Key, AssignedCount = g.Count(), CompletionPct = pct };
            })
            .OrderBy(r => r.CompletionPct)
            .ToList();
    }

    /// <summary>How many KPI/plan slots (that have plan text) have at least
    /// one entity assigned, vs none yet.</summary>
    public AssignmentCoverage GetAssignmentCoverage(int? scopeEntityId)
    {
        var kpis = _db.Kpis.AsNoTracking().ToList();
        var allItems = _db.TrackingItems.AsNoTracking().ToList();
        var countByKpiPlan = allItems.GroupBy(t => (t.KpiId, t.Plan)).ToDictionary(g => g.Key, g => g.Count());
        var mineByKpiPlan = scopeEntityId.HasValue
            ? allItems.Where(t => t.EntityId == scopeEntityId.Value).Select(t => (t.KpiId, t.Plan)).ToHashSet()
            : null;

        int assigned = 0, unassigned = 0;
        foreach (var kpi in kpis)
        {
            foreach (var plan in AllPlans)
            {
                if (string.IsNullOrWhiteSpace(kpi.PlanText(plan))) continue;
                if (mineByKpiPlan is not null)
                {
                    if (mineByKpiPlan.Contains((kpi.Id, plan))) assigned++;
                    continue;
                }
                var count = countByKpiPlan.TryGetValue((kpi.Id, plan), out var c) ? c : 0;
                if (count > 0) assigned++; else unassigned++;
            }
        }
        return new AssignmentCoverage { Assigned = assigned, Unassigned = unassigned };
    }
}
