namespace Kpi.Dashboard.Web.Models;

public class Entity
{
    public int Id { get; set; }
    public string NameAr { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public List<TrackingItem> TrackingItems { get; set; } = new();
    public List<AppUser> Users { get; set; } = new();
}
