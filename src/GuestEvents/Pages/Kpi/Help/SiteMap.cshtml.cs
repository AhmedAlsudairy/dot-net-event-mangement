using Kpi.Dashboard.Web.Models;
using Kpi.Dashboard.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kpi.Dashboard.Web.Pages.Help;

public class SiteMapModel : PageModel
{
    public sealed record Node(string Title, string Url, string? Permission, string Purpose, string TitleEn, string PurposeEn);
    public sealed record Section(string Title, IReadOnlyList<Node> Nodes, string TitleEn);

    // Keep in sync with docs/SITEMAP.md.
    public static readonly IReadOnlyList<Section> Sections = new[]
    {
        new Section("المتابعة", new[]
        {
            new Node("لوحة المتابعة", "/Kpi/Dashboard", Permissions.DashboardView, "رسوم وإحصاءات الإنجاز والمواءمة والمخاطر.", "Dashboard", "Charts and figures on completion, alignment and risk."),
            new Node("جدول التتبع", "/Kpi/Tracking", Permissions.TrackingView, "عرض المؤشرات وإسناد الجهات وتحديث المستهدف والموعد والموقف.", "Tracking", "See the KPIs, assign departments, update target, deadline and status."),
            new Node("تصدير التقرير", "/Kpi/Reports/Export", Permissions.ReportExport, "تنزيل تقرير التنفيذ بصيغة Excel.", "Export report", "Download the execution report as Excel."),
        }, "Tracking"),
        new Section("إدارة المؤشرات", new[]
        {
            new Node("قائمة المؤشرات", "/Kpi/Admin/Kpis", Permissions.KpiManage, "بحث وتعديل وحذف المؤشرات.", "KPI list", "Search, edit and delete KPIs."),
            new Node("إضافة / تعديل مؤشر", "/Kpi/Admin/KpiForm", Permissions.KpiManage, "نموذج البيانات المرجعية للمؤشر.", "Add / edit a KPI", "The KPI reference data form."),
            new Node("بنود المراجعة", "/Kpi/Admin/Review", Permissions.KpiManage, "نقاط من ورقة «للمراجعة» تحتاج تأكيدًا.", "Review items", "Points from the review sheet that need confirming."),
            new Node("استيراد Excel", "/Kpi/Admin/ImportExcel", Permissions.KpiImport, "رفع ملف المؤشرات وتحميل النموذج.", "Import from Excel", "Upload the KPI workbook and download the template."),
        }, "Managing KPIs"),
        new Section("إدارة النظام", new[]
        {
            new Node("الجهات", "/Kpi/Admin/Entities", Permissions.EntityManage, "إضافة الجهات وتفعيلها.", "Departments", "Add departments and switch them on."),
            new Node("الأدوار والصلاحيات", "/Kpi/Admin/Roles", Permissions.RoleManage, "إنشاء الأدوار وتحديد صلاحياتها.", "Roles and permissions", "Create roles and choose their permissions."),
        }, "Administration"),
        new Section("عام", new[]
        {
            new Node("خريطة الموقع", "/Kpi/Help/SiteMap", null, "هذه الصفحة.", "Site map", "This page."),
        }, "General"),
    };

    public HashSet<string> Mine { get; private set; } = new();
    private readonly PermissionService _perms;

    public SiteMapModel(PermissionService perms)
    {
        _perms = perms;
    }

    public void OnGet() => Mine = _perms.For(User);
}
