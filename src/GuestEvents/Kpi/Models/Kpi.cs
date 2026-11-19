namespace Kpi.Dashboard.Web.Models;

public class Kpi
{
    public int Id { get; set; }
    public string Sn { get; set; } = "";
    public string Classification { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Goal { get; set; } = "";
    public string Objective { get; set; } = "";
    public string KpiText { get; set; } = "";
    public string OperationalPlanText { get; set; } = "";
    public string AnnualPlanText { get; set; } = "";
    public string EjadaPlanText { get; set; } = "";
    public string AccreditationCriteria { get; set; } = "";
    public KpiGroup Group { get; set; } = KpiGroup.BranchPlan;
    public string BranchTarget2026 { get; set; } = "";
    public string UpdateNotes { get; set; } = "";
    /// <summary>Serial number in the previous (121-KPI) list, kept for traceability.</summary>
    public string PrevSn { get; set; } = "";

    public List<TrackingItem> TrackingItems { get; set; } = new();

    public string PlanText(PlanType plan) => plan switch
    {
        PlanType.Operational => OperationalPlanText,
        PlanType.Annual => AnnualPlanText,
        PlanType.Ejada => EjadaPlanText,
        _ => ""
    };
}
