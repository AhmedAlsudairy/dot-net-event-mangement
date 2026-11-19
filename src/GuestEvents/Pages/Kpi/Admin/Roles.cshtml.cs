using Kpi.Dashboard.Web.Data;
using Kpi.Dashboard.Web.Models;
using Kpi.Dashboard.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kpi.Dashboard.Web.Pages.Admin;

[Authorize(Policy = Permissions.RoleManage)]
public class RolesModel : PageModel
{

    private string T(string en, string ar) => (Request.Cookies["lang"] ?? "en") == "ar" ? ar : en;
    private readonly AppDbContext _db;

    public RolesModel(AppDbContext db)
    {
        _db = db;
    }

    public List<Role> Roles { get; set; } = new();
    public Role? Editing { get; set; }
    public HashSet<string> EditingPermissions { get; set; } = new();
    public string? Error { get; set; }

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty(SupportsGet = true)] public bool New { get; set; }
    /// <summary>Prefilled role name when the page is opened from an app role row ("Customize").</summary>
    [BindProperty(SupportsGet = true)] public string? Name { get; set; }

    private void Load()
    {
        Roles = _db.Roles.Include(r => r.Permissions).Include(r => r.Users).OrderBy(r => r.Id).ToList();
    }

    public void OnGet()
    {
        Load();
        if (Id.HasValue)
        {
            Editing = Roles.FirstOrDefault(r => r.Id == Id.Value);
            EditingPermissions = Editing?.Permissions.Select(p => p.Permission).ToHashSet() ?? new();
        }
        else if (New && !string.IsNullOrWhiteSpace(Name))
        {
            // Customizing an app role: start from what it gets today (the Viewer default).
            EditingPermissions = Roles.FirstOrDefault(r => r.Name == SystemRoles.Viewer)?.Permissions.Select(p => p.Permission).ToHashSet() ?? new();
        }
    }

    public IActionResult OnPostSave(int? id, string name, string? description, string[]? permissions, bool applyToAllDepartments = false)
    {
        name = (name ?? "").Trim();
        // Only keys from the catalog are ever stored, whatever the client posts.
        var wanted = (permissions ?? Array.Empty<string>()).Where(Permissions.All.Contains).ToHashSet();

        Load();
        if (string.IsNullOrWhiteSpace(name))
        {
            Error = T("Enter a role name.", "اسم الدور مطلوب.");
            Id = id; New = !id.HasValue;
            Editing = id.HasValue ? Roles.FirstOrDefault(r => r.Id == id) : null;
            EditingPermissions = wanted;
            return Page();
        }
        if (Roles.Any(r => r.Name == name && r.Id != id))
        {
            Error = T("That role name is already used.", "اسم الدور مستخدم بالفعل.");
            Id = id; New = !id.HasValue;
            Editing = id.HasValue ? Roles.FirstOrDefault(r => r.Id == id) : null;
            EditingPermissions = wanted;
            return Page();
        }

        Role role;
        if (id.HasValue)
        {
            role = Roles.FirstOrDefault(r => r.Id == id.Value) ?? throw new InvalidOperationException();
            if (role.Name == SystemRoles.Admin)
            {
                TempData["Toast"] = T("The administrator role is fixed and cannot be changed.", "دور مدير النظام ثابت ولا يمكن تعديله.");
                return RedirectToPage();
            }
            if ((role.IsSystem || role.EntityId.HasValue) && role.Name != name) name = role.Name; // referenced by code / tied to its department
            role.Description = description?.Trim() ?? "";
            _db.RolePermissions.RemoveRange(role.Permissions);
        }
        else
        {
            role = new Role { Name = name, IsSystem = false, Description = description?.Trim() ?? "" };
            _db.Roles.Add(role);
        }

        role.Name = name;
        foreach (var p in wanted) role.Permissions.Add(new RolePermission { Permission = p });

        // One edit for every department: copy this department role's permissions onto all the others.
        int applied = 0;
        if (applyToAllDepartments && role.EntityId.HasValue)
        {
            foreach (var other in Roles.Where(r => r.EntityId.HasValue && r.Id != role.Id))
            {
                _db.RolePermissions.RemoveRange(other.Permissions);
                foreach (var p in wanted) other.Permissions.Add(new RolePermission { Permission = p });
                applied++;
            }
        }
        _db.SaveChanges();
        PermissionService.Invalidate();

        TempData["Toast"] = applied > 0 ? T($"Role saved and applied to {applied} other department role(s).", $"تم حفظ الدور وتطبيق صلاحياته على {applied} دورًا آخر للجهات.") : id.HasValue ? T("Role saved.", "تم حفظ الدور.") : T("Role created.", "تم إنشاء الدور.");
        return RedirectToPage();
    }

    public IActionResult OnPostDelete(int id)
    {
        var role = _db.Roles.Include(r => r.Users).FirstOrDefault(r => r.Id == id);
        if (role is null) return RedirectToPage();
        if (role.IsSystem) { TempData["Toast"] = T("A system role cannot be deleted.", "لا يمكن حذف دور نظامي."); return RedirectToPage(); }
        if (role.EntityId.HasValue) { TempData["Toast"] = T("A department role goes with its department; it cannot be deleted by hand.", "دور الجهة يُحذف تلقائيًا مع الجهة ولا يُحذف يدويًا."); return RedirectToPage(); }
        if (role.Users.Count > 0) { TempData["Toast"] = T("This role still has users; move them first.", "لا يمكن حذف دور مُسند لمستخدمين — انقل المستخدمين أولًا."); return RedirectToPage(); }

        _db.Roles.Remove(role);
        _db.SaveChanges();
        PermissionService.Invalidate();
        TempData["Toast"] = T("Role deleted.", "تم حذف الدور.");
        return RedirectToPage();
    }
}
