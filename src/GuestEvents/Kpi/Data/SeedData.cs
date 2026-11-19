using Kpi.Dashboard.Web.Models;
using Kpi.Dashboard.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Kpi.Dashboard.Web.Data;

public static class SeedData
{
    public static readonly string[] EntityNames =
    {
        "مساعد الرئيس بالفرع (AVC)",
        "نائب مساعد الرئيس للشؤون الأكاديمية (DAVCAA)",
        "نائب مساعد الرئيس للأنظمة الإلكترونية وشؤون الطلبة (DAVCESSS)",
        "نائب مساعد الرئيس للدراسات العليا والبحث العلمي والابتكار (DAVCPRI)",
        "مدير الشؤون الإدارية والمالية (DAFA)",
        "قسم التنسيق والمتابعة",
        "قسم التواصل والإعلام",
        "قسم العلاقات العامة",
        "قسم البريد والوثائق",
        "قسم ضمان الجودة",
        "قسم المالية",
        "قسم الشؤون الإدارية",
        "قسم الموارد البشرية",
        "قسم الصحة والسلامة",
        "قسم الشؤون القانونية",
        "مركز الدراسات التحضيرية",
        "قسم المتطلبات المساندة",
        "قسم الهندسة والتكنولوجيا",
        "قسم علوم الحاسوب والمعلومات",
        "قسم التعليم المستمر وخدمة المجتمع",
        "مركز القبول والتسجيل",
        "مركز شؤون الطلبة",
        "مركز التدريب والتوجيه الوظيفي",
        "مركز نظم المعلومات وتقنيات التعليم",
        "قسم البحوث والاستشارات",
        "قسم الابتكار ونقل التكنولوجيا"
    };

    /// <summary>
    /// Creates the three system roles on first run, and on every start re-grants the
    /// Admin role the full catalog so permissions added in later releases are never
    /// missing from it. Other roles are never touched after creation.
    /// </summary>
    public static void EnsureRoles(AppDbContext db)
    {
        void Ensure(string name, string description, IEnumerable<string> perms, bool alwaysSync)
        {
            var role = db.Roles.Include(r => r.Permissions).FirstOrDefault(r => r.Name == name);
            if (role is null)
            {
                role = new Role { Name = name, Description = description, IsSystem = true };
                db.Roles.Add(role);
                foreach (var p in perms) role.Permissions.Add(new RolePermission { Permission = p });
            }
            else if (alwaysSync)
            {
                foreach (var p in perms.Where(p => role.Permissions.All(x => x.Permission != p)))
                    role.Permissions.Add(new RolePermission { Permission = p });
            }
        }

        Ensure(SystemRoles.Admin, "كل الصلاحيات — لا يمكن تعديل صلاحياته.", Permissions.All, alwaysSync: true);
        Ensure(SystemRoles.EntityEditor, "تحديث بيانات جهته فقط.", Permissions.EntityEditorSet, alwaysSync: false);
        Ensure(SystemRoles.Viewer, "اطلاع فقط.", Permissions.ViewerSet, alwaysSync: false);
        Ensure("Kpi", "فريق المؤشرات: يتابع ويحدّث المؤشرات ويستوردها ويصدّرها.", new[] { Permissions.DashboardView, Permissions.TrackingView, Permissions.TrackingEditAll, Permissions.TrackingAssign, Permissions.KpiManage, Permissions.KpiImport, Permissions.ReportExport }, alwaysSync: false);
        Ensure("KpiManager", "مدير المؤشرات: يعرّف المؤشرات ويستوردها ويسندها للجهات ويدير الجهات.", new[] { Permissions.DashboardView, Permissions.TrackingView, Permissions.KpiManage, Permissions.KpiDelete, Permissions.KpiImport, Permissions.TrackingAssign, Permissions.EntityManage, Permissions.ReportExport }, alwaysSync: false);
        Ensure("KpiEditor", "محدّث المتابعة: يحدّث المستهدف والموعد والموقف لأي جهة.", new[] { Permissions.DashboardView, Permissions.TrackingView, Permissions.TrackingEditAll, Permissions.ReportExport }, alwaysSync: false);
        Ensure("KpiOfficer", "مسؤول مؤشرات الجهة: يحدّث بيانات جهته فقط.", new[] { Permissions.DashboardView, Permissions.TrackingView, Permissions.TrackingEditOwn, Permissions.ReportExport }, alwaysSync: false);
        Ensure("KpiViewer", "مطّلع على المؤشرات مع التصدير.", new[] { Permissions.DashboardView, Permissions.TrackingView, Permissions.ReportExport }, alwaysSync: false);
        db.SaveChanges();
        PermissionService.Invalidate();
    }

    public static string DepartmentRoleName(Entity e) => "جهة: " + e.NameAr;

    /// <summary>Gives every department (entity) its own role, starting with the entity-editor permissions.</summary>
    public static void EnsureDepartmentRoles(AppDbContext db)
    {
        var have = db.Roles.Where(r => r.EntityId != null).Select(r => r.EntityId!.Value).ToHashSet();
        foreach (var e in db.Entities.Where(e => !have.Contains(e.Id)).ToList())
        {
            var role = new Role { Name = DepartmentRoleName(e), Description = "دور الجهة — مستخدموه يعدّلون بيانات هذه الجهة فقط ما لم تُمنح صلاحيات أوسع.", EntityId = e.Id };
            foreach (var p in Permissions.EntityEditorSet) role.Permissions.Add(new RolePermission { Permission = p });
            db.Roles.Add(role);
        }
        db.SaveChanges();
        PermissionService.Invalidate();
    }

    public static void EnsureSeeded(AppDbContext db)
    {
        EnsureRoles(db);
        var roleIds = db.Roles.ToDictionary(r => r.Name, r => r.Id);

        if (!db.Entities.Any())
        {
            foreach (var name in EntityNames)
                db.Entities.Add(new Entity { NameAr = name, IsActive = true });
            db.SaveChanges();
        }

        EnsureDepartmentRoles(db);
    }
}
