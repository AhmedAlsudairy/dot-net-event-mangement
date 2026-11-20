using Kpi.Dashboard.Web.Data;
using Kpi.Dashboard.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kpi.Dashboard.Web.Pages.Admin;

[Authorize(Policy = Permissions.KpiManage)]
public class KpiFormModel : PageModel
{

    private string T(string en, string ar) => (Request.Cookies["lang"] ?? "en") == "ar" ? ar : en;
    private readonly AppDbContext _db;

    private readonly GuestEvents.Services.AuditService _audit;
    public KpiFormModel(AppDbContext db, GuestEvents.Services.AuditService audit)
    {
        _audit = audit;
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty] public string Sn { get; set; } = "";
    [BindProperty] public string Classification { get; set; } = "";
    [BindProperty] public string Domain { get; set; } = "";
    [BindProperty] public string Goal { get; set; } = "";
    [BindProperty] public string Objective { get; set; } = "";
    [BindProperty] public string KpiText { get; set; } = "";
    [BindProperty] public string OperationalPlanText { get; set; } = "";
    [BindProperty] public string AnnualPlanText { get; set; } = "";
    [BindProperty] public string EjadaPlanText { get; set; } = "";
    [BindProperty] public string AccreditationCriteria { get; set; } = "";
    [BindProperty] public string BranchTarget2026 { get; set; } = "";
    [BindProperty] public string UpdateNotes { get; set; } = "";
    [BindProperty] public Kpi.Dashboard.Web.Models.KpiGroup Group { get; set; } = Kpi.Dashboard.Web.Models.KpiGroup.BranchPlan;
    [BindProperty] public string PrevSn { get; set; } = "";

    public bool IsEdit => Id.HasValue;
    public string? Error { get; set; }

    public IActionResult OnGet()
    {
        if (Id.HasValue)
        {
            var kpi = _db.Kpis.IgnoreQueryFilters().FirstOrDefault(k => k.Id == Id.Value);
            if (kpi is null) return RedirectToPage("Kpis");
            Sn = kpi.Sn;
            Classification = kpi.Classification;
            Domain = kpi.Domain;
            Goal = kpi.Goal;
            Objective = kpi.Objective;
            KpiText = kpi.KpiText;
            OperationalPlanText = kpi.OperationalPlanText;
            AnnualPlanText = kpi.AnnualPlanText;
            EjadaPlanText = kpi.EjadaPlanText;
            AccreditationCriteria = kpi.AccreditationCriteria;
            BranchTarget2026 = kpi.BranchTarget2026;
            UpdateNotes = kpi.UpdateNotes;
            PrevSn = kpi.PrevSn;
            Group = kpi.Group;
        }
        return Page();
    }

    public IActionResult OnPost()
    {
        Sn = Sn?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(Sn))
        {
            Error = T("Enter the number (S.N.).", "الرقم (S.N.) مطلوب.");
            return Page();
        }
        if (string.IsNullOrWhiteSpace(KpiText))
        {
            Error = T("Enter the KPI text.", "نص المؤشر مطلوب.");
            return Page();
        }

        var duplicate = _db.Kpis.IgnoreQueryFilters().FirstOrDefault(k => k.Sn == Sn && (!Id.HasValue || k.Id != Id.Value));
        if (duplicate is not null)
        {
            Error = T($"The number \"{Sn}\" is already used by another KPI.", $"الرقم \"{Sn}\" مستخدم بالفعل لمؤشر آخر.");
            return Page();
        }

        Kpi.Dashboard.Web.Models.Kpi kpi;
        if (Id.HasValue)
        {
            kpi = _db.Kpis.IgnoreQueryFilters().First(k => k.Id == Id.Value);
        }
        else
        {
            kpi = new Kpi.Dashboard.Web.Models.Kpi();
            _db.Kpis.Add(kpi);
        }

        kpi.Sn = Sn;
        kpi.Classification = Classification?.Trim() ?? "";
        kpi.Domain = Domain?.Trim() ?? "";
        kpi.Goal = Goal?.Trim() ?? "";
        kpi.Objective = Objective?.Trim() ?? "";
        kpi.KpiText = KpiText.Trim();
        kpi.OperationalPlanText = OperationalPlanText?.Trim() ?? "";
        kpi.AnnualPlanText = AnnualPlanText?.Trim() ?? "";
        kpi.EjadaPlanText = EjadaPlanText?.Trim() ?? "";
        kpi.AccreditationCriteria = AccreditationCriteria?.Trim() ?? "";
        kpi.BranchTarget2026 = BranchTarget2026?.Trim() ?? "";
        kpi.UpdateNotes = UpdateNotes?.Trim() ?? "";
        kpi.PrevSn = PrevSn?.Trim() ?? "";
        kpi.Group = Group;

        _db.SaveChanges();
        _audit.Log(User.Identity?.Name ?? "?", Id.HasValue ? "KpiUpdated" : "KpiCreated", $"KPI {kpi.Sn}: {(kpi.KpiText.Length > 120 ? kpi.KpiText[..120] : kpi.KpiText)}");
        TempData["Toast"] = Id.HasValue ? T("KPI saved.", "تم حفظ تعديلات المؤشر.") : T("KPI added.", "تمت إضافة المؤشر.");
        return RedirectToPage("Kpis");
    }
}
