using Kpi.Dashboard.Web.Models;
using Kpi.Dashboard.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kpi.Dashboard.Web.Pages.Reports;

[Authorize(Policy = Permissions.ReportExport)]
public class ExportModel : PageModel
{
    private readonly ExcelExportService _exporter;
    private readonly PermissionService _perms;

    public ExportModel(ExcelExportService exporter, PermissionService perms)
    {
        _exporter = exporter;
        _perms = perms;
    }

    public IActionResult OnGet()
    {
        // Entity-scoped users only ever export their own entity's rows.
        var scope = _perms.IsEntityScoped(User) ? User.GetEntityId() : null;
        var bytes = _exporter.ExportFullReport(scope);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"KPI_Execution_Report_{DateTime.Now:yyyy-MM-dd}.xlsx");
    }
}
