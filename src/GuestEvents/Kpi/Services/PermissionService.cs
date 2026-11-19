using System.Security.Claims;
using Kpi.Dashboard.Web.Data;
using Kpi.Dashboard.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Kpi.Dashboard.Web.Services;

/// <summary>
/// KPI permissions follow the sign-in of the events app (Active Directory roles). Each directory role
/// (Admin, HOD, Staff, Media, Research) maps to the KPI role with the SAME NAME. The Admin role holds every
/// permission; a directory role with no KPI role of its own gets the read-only Viewer permissions, so nothing
/// is hidden by accident. An administrator can create a KPI role named e.g. "HOD" at /Kpi/Admin/Roles to widen it.
/// Permissions are read from the database (cached, invalidated when a role is edited), not from the cookie.
/// </summary>
public class PermissionService
{
    private static readonly object Gate = new();
    private static Dictionary<string, HashSet<string>> _cache = new(StringComparer.OrdinalIgnoreCase);

    private readonly AppDbContext _db;

    public PermissionService(AppDbContext db)
    {
        _db = db;
    }

    public static void Invalidate()
    {
        lock (Gate) { _cache = new(StringComparer.OrdinalIgnoreCase); }
    }

    public HashSet<string> ForRoleName(string roleName)
    {
        lock (Gate)
        {
            if (_cache.TryGetValue(roleName, out var hit)) return hit;
        }

        var perms = _db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleRef!.Name == roleName).Select(rp => rp.Permission)
            .ToHashSet();
        if (perms.Count == 0 && !_db.Roles.Any(r => r.Name == roleName))
            perms = _db.RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleRef!.Name == SystemRoles.Viewer).Select(rp => rp.Permission).ToHashSet();

        lock (Gate) { _cache[roleName] = perms; }
        return perms;
    }

    public HashSet<string> For(ClaimsPrincipal user)
    {
        var all = new HashSet<string>();
        foreach (var role in user.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct())
            all.UnionWith(ForRoleName(role));
        return all;
    }

    public bool Has(ClaimsPrincipal user, string permission) => For(user).Contains(permission);

    /// <summary>
    /// True when this user may only touch their own entity's rows: they can edit
    /// their own entity but hold neither edit.all nor assign.
    /// </summary>
    public bool IsEntityScoped(ClaimsPrincipal user)
    {
        var p = For(user);
        return !p.Contains(Permissions.TrackingEditAll) && !p.Contains(Permissions.TrackingAssign)
               && p.Contains(Permissions.TrackingEditOwn);
    }
}

public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string permission) => Permission = permission;
}

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly PermissionService _perms;

    public PermissionHandler(PermissionService perms)
    {
        _perms = perms;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (_perms.Has(context.User, requirement.Permission)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

public static class ClaimsPrincipalExtensions
{
    /// <summary>The KPI app has no accounts of its own any more, so there is no KPI user id.</summary>
    public static int? GetUserId(this ClaimsPrincipal user) => null;

    /// <summary>The KPI department a Department KPI officer works for (set when the Admin gives the role; carried as a claim).</summary>
    public static int? GetKpiEntityId(this ClaimsPrincipal user) => GetEntityId(user);
    public static int? GetEntityId(this ClaimsPrincipal user) => int.TryParse(user.FindFirst("kpi_entity")?.Value, out var id) ? id : null;
}
