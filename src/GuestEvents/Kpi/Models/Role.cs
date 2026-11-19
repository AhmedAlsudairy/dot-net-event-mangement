namespace Kpi.Dashboard.Web.Models;

/// <summary>
/// A named bundle of permissions. Replaces the old fixed AppRole enum so an
/// administrator can create roles at runtime and decide exactly what each can do.
/// </summary>
public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>Seeded roles cannot be deleted; the Admin role's permissions are also locked.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Set for a department role: the one entity every user in this role belongs to.</summary>
    public int? EntityId { get; set; }
    public Entity? EntityRef { get; set; }

    public List<RolePermission> Permissions { get; set; } = new();
    public List<AppUser> Users { get; set; } = new();
}

public class RolePermission
{
    public int RoleId { get; set; }
    public Role? RoleRef { get; set; }
    public string Permission { get; set; } = "";
}
