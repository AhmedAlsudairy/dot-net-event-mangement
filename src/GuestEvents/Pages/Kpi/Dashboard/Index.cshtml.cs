using System.Text.Json;
using Kpi.Dashboard.Web.Data;
using Kpi.Dashboard.Web.Models;
using Kpi.Dashboard.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kpi.Dashboard.Web.Pages.Dashboard;

[Authorize(Policy = Permissions.DashboardView)]
public class IndexModel : PageModel
{
    private readonly DashboardService _dashboard;
    private readonly AppDbContext _db;
    private readonly PermissionService _perms;

    public IndexModel(DashboardService dashboard, AppDbContext db, PermissionService perms)
    {
        _perms = perms;
        _dashboard = dashboard;
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string Plan { get; set; } = "all";

    public bool IsEntityScoped { get; private set; }
    public string? ScopedEntityName { get; private set; }
    public bool IsEmpty { get; private set; }
    public bool CanManageKpis { get; private set; }
    public bool CanExport { get; private set; }

    public PlanStats OverviewStats { get; set; } = new();
    public AlignmentOverview Alignment { get; set; } = new();

    public string StatusChartJson { get; set; } = "{}";
    public string PlanComparisonChartJson { get; set; } = "{}";
    public string DomainChartJson { get; set; } = "{}";
    public string GoalChartJson { get; set; } = "{}";
    public string ClassificationChartJson { get; set; } = "{}";
    public string CoverageChartJson { get; set; } = "{}";
    public string ScorecardChartJson { get; set; } = "{}";

    public List<AtRiskRow> AtRiskItems { get; set; } = new();
    public List<EntityScoreRow> Scorecard { get; set; } = new();
    public AssignmentCoverage Coverage { get; set; } = new();

    public void OnGet()
    {
        var perms = _perms.For(User);
        CanManageKpis = perms.Contains(Permissions.KpiManage) || perms.Contains(Permissions.KpiImport);
        CanExport = perms.Contains(Permissions.ReportExport);

        // Users who may only edit their own entity see the dashboard for that entity alone.
        var scopeEntityId = _perms.IsEntityScoped(User) ? User.GetEntityId() : null;
        IsEntityScoped = scopeEntityId.HasValue;
        ScopedEntityName = scopeEntityId.HasValue ? _db.Entities.Find(scopeEntityId.Value)?.NameAr : null;
        IsEmpty = !_db.Kpis.Any();
        if (IsEmpty) return;

        PlanType? planFilter = Plan switch
        {
            "operational" => PlanType.Operational,
            "annual" => PlanType.Annual,
            "ejada" => PlanType.Ejada,
            _ => null
        };

        OverviewStats = _dashboard.ComputePlanStats(scopeEntityId, planFilter);
        Alignment = _dashboard.ComputeAlignmentOverview(scopeEntityId);

        StatusChartJson = JsonSerializer.Serialize(new
        {
            labels = new[] { T("Done", "منفذ"), T("In progress", "قيد التنفيذ"), T("Not done", "لم ينفذ") },
            data = new[] { OverviewStats.Done, OverviewStats.Prog, OverviewStats.Low }
        });

        var opS = _dashboard.ComputePlanStats(scopeEntityId, PlanType.Operational);
        var anS = _dashboard.ComputePlanStats(scopeEntityId, PlanType.Annual);
        var ejS = _dashboard.ComputePlanStats(scopeEntityId, PlanType.Ejada);
        PlanComparisonChartJson = JsonSerializer.Serialize(new
        {
            labels = new[] { T("Operational plan", "الخطة التشغيلية"), T("Annual plan", "الخطة السنوية"), T("Ejada", "الإجادة المؤسسية") },
            names = new[] { T("Done", "منفذ"), T("In progress", "قيد التنفيذ"), T("Not done", "لم ينفذ") },
            done = new[] { opS.Done, anS.Done, ejS.Done },
            prog = new[] { opS.Prog, anS.Prog, ejS.Prog },
            low = new[] { opS.Low, anS.Low, ejS.Low }
        });

        DomainChartJson = ToChartJson(_dashboard.CountByDomain(scopeEntityId));
        GoalChartJson = ToChartJson(_dashboard.CountByGoal(scopeEntityId));
        ClassificationChartJson = ToChartJson(_dashboard.CountByClassification(scopeEntityId));

        AtRiskItems = _dashboard.GetAtRiskItems(scopeEntityId);

        Coverage = _dashboard.GetAssignmentCoverage(scopeEntityId);
        CoverageChartJson = JsonSerializer.Serialize(new
        {
            labels = new[] { T("Assigned", "مُسندة لجهة"), T("Not assigned yet", "غير مُسندة بعد") },
            data = new[] { Coverage.Assigned, Coverage.Unassigned }
        });

        // Cross-entity comparison only makes sense for the global view — an
        // EntityEditor's dashboard is already scoped to one entity.
        if (!scopeEntityId.HasValue)
        {
            Scorecard = _dashboard.GetEntityScorecard();
            ScorecardChartJson = JsonSerializer.Serialize(new
            {
                labels = Scorecard.Select(s => s.EntityName),
                data = Scorecard.Select(s => s.CompletionPct)
            });
        }
    }

    private string T(string en, string ar) => (Request.Cookies["lang"] ?? "en") == "ar" ? ar : en;

    private static string ToChartJson(Dictionary<string, int> data) =>
        JsonSerializer.Serialize(new { labels = data.Keys, data = data.Values });
}
