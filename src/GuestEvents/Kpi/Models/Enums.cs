namespace Kpi.Dashboard.Web.Models;

public enum PlanType
{
    Operational,
    Annual,
    Ejada
}

public enum ExecutionStatus
{
    NotStarted,
    InProgress,
    Done
}

/// <summary>
/// Which block of the alignment workbook a KPI row came from. Only OutsideBranchPlan
/// rows are hidden from the dashboard/tracking/export (they are kept for reference).
/// </summary>
public enum KpiGroup
{
    /// <summary>One of the branch's operational-plan KPIs.</summary>
    BranchPlan,
    /// <summary>Annual-plan project with no operational KPI counterpart.</summary>
    AnnualOnly,
    /// <summary>Ejada indicator with no operational KPI counterpart.</summary>
    EjadaOnly,
    /// <summary>From the previous 121-KPI list but not in the branch plan any more.</summary>
    OutsideBranchPlan
}
