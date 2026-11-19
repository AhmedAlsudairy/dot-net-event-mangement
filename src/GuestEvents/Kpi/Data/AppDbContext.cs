using Kpi.Dashboard.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Kpi.Dashboard.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Kpi.Dashboard.Web.Models.Kpi> Kpis => Set<Kpi.Dashboard.Web.Models.Kpi>();
    public DbSet<Entity> Entities => Set<Entity>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<TrackingItem> TrackingItems => Set<TrackingItem>();
    public DbSet<ReviewItem> ReviewItems => Set<ReviewItem>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Kpi.Dashboard.Web.Models.Kpi>(e =>
        {
            e.HasIndex(k => k.Sn).IsUnique();
            e.Property(k => k.Classification).HasMaxLength(100);
            e.Property(k => k.Sn).HasMaxLength(20);
            e.Property(k => k.Group).HasConversion<string>().HasMaxLength(30);
            // Out-of-plan KPIs stay in the database but are invisible to dashboard, tracking,
            // export and saves. Admin pages opt back in with IgnoreQueryFilters().
            e.HasQueryFilter(k => k.Group != KpiGroup.OutsideBranchPlan);
        });

        modelBuilder.Entity<Entity>(e =>
        {
            e.Property(x => x.NameAr).HasMaxLength(300);
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasOne(u => u.RoleRef).WithMany(r => r.Users).HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(u => u.EntityRef).WithMany(x => x.Users).HasForeignKey(u => u.EntityId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ReviewItem>(e =>
        {
            e.Property(r => r.Category).HasMaxLength(100);
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.HasIndex(r => r.Name).IsUnique();
            e.Property(r => r.Name).HasMaxLength(300);
            e.HasIndex(r => r.EntityId).IsUnique();
            e.HasOne(r => r.EntityRef).WithMany().HasForeignKey(r => r.EntityId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.HasKey(rp => new { rp.RoleId, rp.Permission });
            e.Property(rp => rp.Permission).HasMaxLength(60);
            e.HasOne(rp => rp.RoleRef).WithMany(r => r.Permissions).HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrackingItem>(e =>
        {
            e.HasQueryFilter(t => t.KpiRef!.Group != KpiGroup.OutsideBranchPlan);
            e.Property(t => t.Plan).HasConversion<string>();
            e.Property(t => t.Status).HasConversion<string?>();
            e.HasOne(t => t.KpiRef).WithMany(k => k.TrackingItems).HasForeignKey(t => t.KpiId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.EntityRef).WithMany(x => x.TrackingItems).HasForeignKey(t => t.EntityId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(t => new { t.KpiId, t.Plan, t.EntityId }).IsUnique();
        });
    }
}
