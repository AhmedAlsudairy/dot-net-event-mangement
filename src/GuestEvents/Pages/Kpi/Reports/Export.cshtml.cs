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

    private readonly GuestEvents.Services.AuditService _audit;
    public ExportModel(ExcelExportService exporter, PermissionService perms, GuestEvents.Services.AuditService audit)
    {
        _audit = audit;
        _exporter = exporter;
        _perms = perms;
    }

    public IActionResult OnGet()
    {
        // Entity-scoped users only ever export their own entity's rows.
        var scope = _perms.IsEntityScoped(User) ? User.GetEntityId() : null;
        var bytes = _exporter.ExportFullReport(scope);
        _audit.Log(User.Identity?.Name ?? "?", "KpiExport", scope.HasValue ? $"KPI execution report (department {scope})" : "KPI execution report (all departments)");
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"KPI_Execution_Report_{DateTime.Now:yyyy-MM-dd}.xlsx");
    }
}
