using GuestEvents.Models;

namespace GuestEvents.Services;

/// <summary>Plain-language names and one-line meanings of every role, used wherever a person sees their roles.</summary>
public static class RoleInfo
{
    public record Info(string Key, string En, string Ar, string DescEn, string DescAr);

    public static readonly Info[] All =
    {
        new(Roles.Staff, "Staff", "موظف", "Ask for events, bus trips and upload research.", "اطلب الفعاليات ورحلات الحافلات وارفع الأبحاث."),
        new(Roles.Hod, "Head of department", "رئيس القسم", "Approve event requests at the steps given to your role.", "اعتمد طلبات الفعاليات في الخطوات المسندة لدورك."),
        new(Roles.Dean, "Dean", "العميد", "Give the final approval on event requests.", "قدّم الاعتماد النهائي على طلبات الفعاليات."),
        new(Roles.Media, "Media officer", "مسؤول الإعلام", "See approved events and download the image and QR.", "اطّلع على الفعاليات المعتمدة ونزّل الصورة وQR."),
        new(Roles.Research, "Research in-charge", "مسؤول الأبحاث", "See every research document and proposal.", "اطّلع على كل الأبحاث والمقترحات."),
        new(Roles.Kpi, "KPI team", "فريق المؤشرات", "Follow and update every department's KPIs, assign and manage them.", "تابع وحدّث مؤشرات كل الجهات وأسندها وأدرها."),
        new(Roles.KpiManager, "KPI manager", "مدير المؤشرات", "Define the KPIs, import them and assign them to departments.", "عرّف المؤشرات واستوردها وأسندها للجهات."),
        new(Roles.KpiEditor, "KPI editor", "محدّث المتابعة", "Update target, deadline and status for any department.", "حدّث المستهدف والموعد والموقف لأي جهة."),
        new(Roles.KpiOfficer, "Department KPI officer", "مسؤول مؤشرات الجهة", "Update the KPIs of your own department.", "حدّث مؤشرات جهتك."),
        new(Roles.KpiViewer, "KPI viewer", "مطّلع على المؤشرات", "See KPI progress and export the Excel report.", "اطّلع على تقدّم المؤشرات وصدّر تقرير Excel."),
        new(Roles.Admin, "Administrator", "مدير النظام", "Everything: settings, approval flow, roles, reports.", "كل شيء: الإعدادات ومسار الموافقات والأدوار والتقارير."),
    };

    public static Info? Get(string key) => All.FirstOrDefault(i => i.Key == key);
    public static string Name(string key, bool ar) => Get(key) is { } i ? (ar ? i.Ar : i.En) : key;
}
