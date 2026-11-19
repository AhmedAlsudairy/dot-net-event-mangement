namespace Kpi.Dashboard.Web.Models;

public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";

    /// <summary>Only used while LDAP is disabled. Null once an account is LDAP-only.</summary>
    public string? PasswordHash { get; set; }

    public int RoleId { get; set; }
    public Role? RoleRef { get; set; }

    /// <summary>Required for roles that hold tracking.edit.own without tracking.edit.all; the one entity this user may edit.</summary>
    public int? EntityId { get; set; }
    public Entity? EntityRef { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
