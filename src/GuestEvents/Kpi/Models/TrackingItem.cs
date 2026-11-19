namespace Kpi.Dashboard.Web.Models;

/// <summary>
/// One entity's progress on one KPI's one plan. Replaces TRACKING[key][plan].items[]
/// from the original localStorage model with a shared, multi-user row.
/// </summary>
public class TrackingItem
{
    public int Id { get; set; }

    public int KpiId { get; set; }
    public Kpi? KpiRef { get; set; }

    public PlanType Plan { get; set; }

    public int EntityId { get; set; }
    public Entity? EntityRef { get; set; }

    public string Target { get; set; } = "";
    public DateOnly? Deadline { get; set; }
    public ExecutionStatus? Status { get; set; }

    public int? UpdatedByUserId { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
