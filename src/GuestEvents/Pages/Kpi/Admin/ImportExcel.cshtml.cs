using Kpi.Dashboard.Web.Data;
using Kpi.Dashboard.Web.Models;
using Kpi.Dashboard.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kpi.Dashboard.Web.Pages.Admin;

[Authorize(Policy = Permissions.KpiImport)]
public class ImportExcelModel : PageModel
{

    private string T(string en, string ar) => (Request.Cookies["lang"] ?? "en") == "ar" ? ar : en;
    private readonly ExcelImportService _importer;
    private readonly ExcelExportService _exporter;
    private readonly AppDbContext _db;

    public ImportExcelModel(ExcelImportService importer, ExcelExportService exporter, AppDbContext db)
    {
        _importer = importer;
        _exporter = exporter;
        _db = db;
    }

    public int CurrentKpiCount { get; set; }
    public ImportResult? Result { get; set; }
    public string? Error { get; set; }

    public void OnGet()
    {
        CurrentKpiCount = _db.Kpis.Count();
    }

    public async Task<IActionResult> OnPostAsync(IFormFile? file)
    {
        CurrentKpiCount = _db.Kpis.Count();
        if (file is null || file.Length == 0)
        {
            Error = T("Choose an Excel file first.", "اختر ملف Excel أولًا.");
            return Page();
        }

        try
        {
            await using var stream = file.OpenReadStream();
            Result = _importer.Import(stream);
            CurrentKpiCount = _db.Kpis.Count();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }

        return Page();
    }

    public IActionResult OnGetTemplate()
    {
        var bytes = _exporter.GenerateImportTemplate();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "KPI_Import_Template.xlsx");
    }
}
