namespace GuestEvents.Services;

/// <summary>What each logged action means: its category, how serious it is, and a plain-language name in English and Arabic.</summary>
public static class AuditCatalog
{
    public record Info(string Category, string Severity, string En, string Ar);

    public static readonly string[] Categories = { "Auth", "Events", "Visits", "Research", "Approvals", "Admin", "Access", "KPI", "Data export", "System" };
    public static readonly string[] Severities = { "Info", "Warning", "Error", "Security" };

    private static readonly Dictionary<string, Info> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // sign-in and access
        ["Login"] = new("Auth", "Info", "Signed in", "تسجيل دخول"),
        ["LoginFailed"] = new("Auth", "Warning", "Failed sign-in", "فشل تسجيل الدخول"),
        ["Logout"] = new("Auth", "Info", "Signed out", "تسجيل خروج"),
        ["PasswordHelpRequested"] = new("Auth", "Info", "Asked IT for a password reset", "طلب إعادة تعيين كلمة المرور"),
        ["AccessDenied"] = new("Auth", "Security", "Blocked: no permission", "رفض: لا توجد صلاحية"),
        // events
        ["Submit"] = new("Events", "Info", "Event submitted", "إرسال فعالية"),
        ["SaveDraft"] = new("Events", "Info", "Event draft saved", "حفظ مسودة فعالية"),
        ["ApproveStep"] = new("Approvals", "Info", "Approved a step", "اعتماد خطوة"),
        ["Approve"] = new("Approvals", "Info", "Event fully approved", "اعتماد نهائي للفعالية"),
        ["Reject"] = new("Approvals", "Warning", "Event returned", "إعادة فعالية"),
        ["FoodRequest"] = new("Events", "Info", "Food request sent", "إرسال طلب طعام"),
        ["FoodStatus"] = new("Events", "Info", "Food request status changed", "تغيير حالة طلب الطعام"),
        // visits
        ["SubmitVisit"] = new("Visits", "Info", "Visit submitted", "إرسال زيارة"),
        ["SaveVisitDraft"] = new("Visits", "Info", "Visit draft saved", "حفظ مسودة زيارة"),
        ["ApproveVisit"] = new("Approvals", "Info", "Visit approved", "اعتماد زيارة"),
        ["RejectVisit"] = new("Approvals", "Warning", "Visit returned", "إعادة زيارة"),
        ["ConfirmBus"] = new("Visits", "Info", "Bus confirmed", "تأكيد الحافلة"),
        // research
        ["ResearchUpload"] = new("Research", "Info", "Research uploaded", "رفع بحث"),
        ["ResearchDownload"] = new("Research", "Info", "Research downloaded", "تنزيل بحث"),
        // exports
        ["Export"] = new("Data export", "Info", "Data exported", "تصدير بيانات"),
        ["KpiExport"] = new("Data export", "Info", "KPI report exported", "تصدير تقرير المؤشرات"),
        ["AuditExport"] = new("Data export", "Security", "Audit log exported", "تصدير سجل التدقيق"),
        // admin
        ["SettingChanged"] = new("Admin", "Info", "Setting changed", "تغيير إعداد"),
        ["ConnectionsChanged"] = new("Admin", "Security", "LDAP / email connection changed", "تغيير اتصال LDAP / البريد"),
        ["TestLdap"] = new("Admin", "Info", "Tested LDAP", "اختبار LDAP"),
        ["TestSmtp"] = new("Admin", "Info", "Tested email", "اختبار البريد"),
        ["FetchLdapGroups"] = new("Admin", "Info", "Loaded AD groups", "جلب مجموعات AD"),
        ["LogoChanged"] = new("Admin", "Info", "Logo changed", "تغيير الشعار"),
        ["LogoRemoved"] = new("Admin", "Info", "Logo removed", "إزالة الشعار"),
        ["ApprovalStepAdded"] = new("Admin", "Info", "Approval step added", "إضافة خطوة موافقة"),
        ["ApprovalStepChanged"] = new("Admin", "Info", "Approval step changed", "تغيير خطوة موافقة"),
        ["ApprovalStepMoved"] = new("Admin", "Info", "Approval steps reordered", "إعادة ترتيب خطوات الموافقة"),
        ["ApprovalStepDeleted"] = new("Admin", "Warning", "Approval step deleted", "حذف خطوة موافقة"),
        // roles and permissions
        ["RoleAssigned"] = new("Access", "Security", "Role given to a person", "منح دور لشخص"),
        ["RoleRemoved"] = new("Access", "Security", "Role taken from a person", "سحب دور من شخص"),
        ["KpiRoleSaved"] = new("Access", "Security", "KPI permissions changed", "تغيير صلاحيات المؤشرات"),
        ["KpiRoleDeleted"] = new("Access", "Security", "KPI role deleted", "حذف دور مؤشرات"),
        // KPI
        ["KpiCreated"] = new("KPI", "Info", "KPI added", "إضافة مؤشر"),
        ["KpiUpdated"] = new("KPI", "Info", "KPI edited", "تعديل مؤشر"),
        ["KpiDeleted"] = new("KPI", "Warning", "KPI deleted", "حذف مؤشر"),
        ["TrackingSaved"] = new("KPI", "Info", "KPI tracking updated", "تحديث متابعة مؤشر"),
        ["KpiImport"] = new("KPI", "Info", "KPIs imported from Excel", "استيراد المؤشرات من Excel"),
        ["KpiEntityAdded"] = new("KPI", "Info", "KPI department added", "إضافة جهة مؤشرات"),
        ["KpiEntityToggled"] = new("KPI", "Info", "KPI department switched on/off", "تفعيل/إيقاف جهة مؤشرات"),
        ["KpiReviewToggled"] = new("KPI", "Info", "Review item updated", "تحديث بند مراجعة"),
        // system
        ["ServerError"] = new("System", "Error", "Server error", "خطأ في الخادم"),
        ["UnhandledException"] = new("System", "Error", "Unhandled exception", "استثناء غير معالج"),
        ["SlowRequest"] = new("System", "Warning", "Slow request", "طلب بطيء"),
        ["AuditPurged"] = new("System", "Security", "Old log entries deleted", "حذف سجلات قديمة"),
    };

    public static Info Describe(string action) =>
        Map.TryGetValue(action ?? "", out var i) ? i : new("General", "Info", action ?? "", action ?? "");
    public static string Label(string action, bool ar) => Map.TryGetValue(action ?? "", out var i) ? (ar ? i.Ar : i.En) : action ?? "";
    public static IEnumerable<string> Actions => Map.Keys.OrderBy(k => k);
}
