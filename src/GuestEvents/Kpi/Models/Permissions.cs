namespace Kpi.Dashboard.Web.Models;

/// <summary>
/// The fixed permission catalog. Permissions are code (each one gates a concrete
/// page or action); roles are data (which permissions a role holds).
/// </summary>
public static class Permissions
{
    public const string DashboardView = "dashboard.view";
    public const string TrackingView = "tracking.view";
    public const string TrackingEditOwn = "tracking.edit.own";
    public const string TrackingEditAll = "tracking.edit.all";
    public const string TrackingAssign = "tracking.assign";
    public const string KpiManage = "kpi.manage";
    public const string KpiDelete = "kpi.delete";
    public const string KpiImport = "kpi.import";
    public const string ReportExport = "report.export";
    public const string EntityManage = "entity.manage";
    public const string UserManage = "user.manage";
    public const string RoleManage = "role.manage";

    public sealed record Info(string Key, string Group, string LabelAr, string DescriptionAr, string GroupEn = "", string LabelEn = "", string DescriptionEn = "")
    {
        public string GroupT(bool ar) => ar || GroupEn == "" ? Group : GroupEn;
        public string LabelT(bool ar) => ar || LabelEn == "" ? LabelAr : LabelEn;
        public string DescriptionT(bool ar) => ar || DescriptionEn == "" ? DescriptionAr : DescriptionEn;
    }

    public static readonly IReadOnlyList<Info> Catalog = new[]
    {
        new Info(DashboardView, "المتابعة", "عرض لوحة المتابعة", "الاطلاع على الرسوم والإحصاءات.", "Tracking", "View the dashboard", "See the charts and figures."),
        new Info(TrackingView, "المتابعة", "عرض جدول التتبع", "الاطلاع على جدول المؤشرات والإسنادات.", "Tracking", "View the tracking table", "See the KPI table and assignments."),
        new Info(TrackingEditOwn, "المتابعة", "تحديث بيانات جهته", "تعديل المستهدف والموعد والموقف لصف جهة المستخدم فقط.", "Tracking", "Update own department", "Edit target, deadline and status for the user's department only."),
        new Info(TrackingEditAll, "المتابعة", "تحديث بيانات أي جهة", "تعديل المستهدف والموعد والموقف لأي جهة مُسندة.", "Tracking", "Update any department", "Edit target, deadline and status for any assigned department."),
        new Info(TrackingAssign, "المؤشرات", "إسناد المؤشرات للجهات", "إضافة جهات إلى مؤشر/خطة أو إزالتها (يتضمن تعديل بياناتها).", "KPIs", "Assign KPIs to departments", "Add or remove departments on a KPI/plan (includes editing their data)."),
        new Info(KpiManage, "المؤشرات", "إضافة وتعديل المؤشرات", "إنشاء مؤشر جديد وتعديل بياناته المرجعية.", "KPIs", "Add and edit KPIs", "Create a KPI and edit its reference data."),
        new Info(KpiDelete, "المؤشرات", "حذف المؤشرات", "حذف مؤشر مع كل بيانات متابعته.", "KPIs", "Delete KPIs", "Delete a KPI with all of its tracking data."),
        new Info(KpiImport, "المؤشرات", "استيراد Excel", "رفع ملف المؤشرات وتحديث المؤشرات جماعيًا.", "KPIs", "Import from Excel", "Upload the KPI workbook and update KPIs in bulk."),
        new Info(ReportExport, "التقارير", "تصدير التقارير", "تنزيل تقرير التنفيذ الكامل بصيغة Excel.", "Reports", "Export reports", "Download the full execution report as Excel."),
        new Info(EntityManage, "الإدارة", "إدارة الجهات", "إضافة الجهات وتفعيلها أو إيقافها.", "Administration", "Manage departments", "Add departments and switch them on or off."),
        new Info(UserManage, "الإدارة", "إدارة المستخدمين", "إضافة المستخدمين وتغيير أدوارهم وجهاتهم وتفعيلهم.", "Administration", "Manage users", "Add users and change their roles and departments."),
        new Info(RoleManage, "الإدارة", "إدارة الأدوار والصلاحيات", "إنشاء أدوار جديدة وتحديد صلاحياتها.", "Administration", "Manage roles and permissions", "Create roles and choose their permissions."),
    };

    public static readonly IReadOnlySet<string> All = Catalog.Select(p => p.Key).ToHashSet();

    public static readonly string[] ViewerSet = { DashboardView, TrackingView };
    public static readonly string[] EntityEditorSet = { DashboardView, TrackingView, TrackingEditOwn, ReportExport };
}

public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string EntityEditor = "EntityEditor";
    public const string Viewer = "Viewer";
}
