using Kpi.Dashboard.Web.Models;

namespace Kpi.Dashboard.Web.Services;

public class TrackingItemDto
{
    public int EntityId { get; set; }
    public string Target { get; set; } = "";
    public string? Deadline { get; set; } // yyyy-MM-dd
    public string? Status { get; set; } // "Done" / "InProgress" / "NotStarted" / null
}

public class SaveTrackingRequest
{
    public int KpiId { get; set; }
    public PlanType Plan { get; set; }
    public List<TrackingItemDto> Items { get; set; } = new();
}
