namespace Kpi.Dashboard.Web.Models;

public class AuditLog
{
    public int Id { get; set; }
    public int? TrackingItemId { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = "";
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
