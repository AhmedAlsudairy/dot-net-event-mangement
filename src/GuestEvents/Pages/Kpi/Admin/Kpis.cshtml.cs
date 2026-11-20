using Kpi.Dashboard.Web.Data;
using Kpi.Dashboard.Web.Models;
using Kpi.Dashboard.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kpi.Dashboard.Web.Pages.Admin;

[Authorize(Policy = Permissions.KpiManage)]
public class KpisModel : PageModel
{

    private string T(string en, string ar) => (Request.Cookies["lang"] ?? "en") == "ar" ? ar : en;
    private readonly AppDbContext _db;
    private readonly PermissionService _perms;
    private readonly GuestEvents.Services.AuditService _audit;

    public KpisModel(AppDbContext db, PermissionService perms, GuestEvents.Services.AuditService audit)
    {
        _audit = audit;
        _perms = perms;
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Group { get; set; }

    public List<Kpi.Dashboard.Web.Models.Kpi> Kpis { get; set; } = new();
    public bool CanDelete => _perms.Has(User, Permissions.KpiDelete);

    public void OnGet()
    {
        var query = _db.Kpis.IgnoreQueryFilters().AsQueryable();
        if (!string.IsNullOrEmpty(Group) && Enum.TryParse<KpiGroup>(Group, out var g)) query = query.Where(k => k.Group == g);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            query = query.Where(k => k.KpiText.Contains(s) || k.Sn.Contains(s) || k.Domain.Contains(s) || k.Goal.Contains(s));
        }
        Kpis = query.ToList()
            .OrderBy(k => int.TryParse(k.Sn, out var n) ? n : int.MaxValue).ThenBy(k => k.Sn)
            .ToList();
    }

    public IActionResult OnPostDelete(int id)
    {
        if (!_perms.Has(User, Permissions.KpiDelete)) return Forbid();
        var kpi = _db.Kpis.IgnoreQueryFilters().FirstOrDefault(k => k.Id == id);
        if (kpi is not null)
        {
            _db.Kpis.Remove(kpi); // cascades to TrackingItems for this KPI
            _db.SaveChanges();
            _audit.Log(User.Identity?.Name ?? "?", "KpiDeleted", $"KPI {kpi.Sn}: {(kpi.KpiText.Length > 120 ? kpi.KpiText[..120] : kpi.KpiText)}");
            TempData["Toast"] = T("The KPI and its tracking data were deleted.", "تم حذف المؤشر وكل بيانات متابعته.");
        }
        return RedirectToPage();
    }
}
