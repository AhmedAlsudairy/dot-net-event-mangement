using Kpi.Dashboard.Web.Models;
using Kpi.Dashboard.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kpi.Dashboard.Web.Pages;

public class IndexModel : PageModel
{
    private readonly PermissionService _perms;

    public IndexModel(PermissionService perms)
    {
        _perms = perms;
    }

    /// <summary>Lands each user on the first page their role can actually open.</summary>
    public IActionResult OnGet()
    {
        var p = _perms.For(User);
        if (p.Contains(Permissions.DashboardView)) return RedirectToPage("/Kpi/Dashboard/Index");
        if (p.Contains(Permissions.TrackingView)) return RedirectToPage("/Kpi/Tracking/Index");
        return RedirectToPage("/Kpi/Help/SiteMap");
    }
}
