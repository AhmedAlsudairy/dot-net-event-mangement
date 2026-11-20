using Kpi.Dashboard.Web.Data;
using Kpi.Dashboard.Web.Models;
using Kpi.Dashboard.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kpi.Dashboard.Web.Pages.Admin;

[Authorize(Policy = Permissions.EntityManage)]
public class EntitiesModel : PageModel
{

    private string T(string en, string ar) => (Request.Cookies["lang"] ?? "en") == "ar" ? ar : en;
    private readonly AppDbContext _db;

    private readonly GuestEvents.Services.AuditService _audit;
    public EntitiesModel(AppDbContext db, GuestEvents.Services.AuditService audit)
    {
        _audit = audit;
        _db = db;
    }

    public List<Entity> Entities { get; set; } = new();

    public void OnGet()
    {
        Entities = _db.Entities.OrderBy(e => e.NameAr).ToList();
    }

    public IActionResult OnPostAdd(string name)
    {
        if (!string.IsNullOrWhiteSpace(name) && !_db.Entities.Any(e => e.NameAr == name))
        {
            _db.Entities.Add(new Entity { NameAr = name.Trim(), IsActive = true });
            _db.SaveChanges();
            SeedData.EnsureDepartmentRoles(_db);
            _audit.Log(User.Identity?.Name ?? "?", "KpiEntityAdded", name.Trim());
            TempData["Toast"] = T("Department added, with its own role.", "تمت إضافة الجهة وإنشاء دور خاص بها.");
        }
        return RedirectToPage();
    }

    public IActionResult OnPostToggle(int id)
    {
        var entity = _db.Entities.Find(id);
        if (entity is not null)
        {
            entity.IsActive = !entity.IsActive;
            _db.SaveChanges();
            _audit.Log(User.Identity?.Name ?? "?", "KpiEntityToggled", $"{entity.NameAr} -> {(entity.IsActive ? "active" : "off")}");
        }
        return RedirectToPage();
    }
}
