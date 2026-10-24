using GuestEvents.Models;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Data;

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> o) : base(o) { }
    public DbSet<EventRequest> Events => Set<EventRequest>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<EventImage> Images => Set<EventImage>();
    public DbSet<QRCodeInfo> QRCodes => Set<QRCodeInfo>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<FoodRequest> FoodRequests => Set<FoodRequest>();
    public DbSet<ExternalVisit> Visits => Set<ExternalVisit>();
    public DbSet<ResearchDoc> ResearchDocs => Set<ResearchDoc>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<QRCodeInfo>().HasIndex(q => q.Token).IsUnique();
        b.Entity<RoleAssignment>().HasIndex(r => new { r.Username, r.Role }).IsUnique();
        b.Entity<QRCodeInfo>().HasOne(q => q.EventRequest).WithOne(e => e.QR).HasForeignKey<QRCodeInfo>(q => q.EventRequestId);
        b.Entity<EventImage>().HasOne<EventRequest>().WithOne(e => e.Image).HasForeignKey<EventImage>(i => i.EventRequestId);
        b.Entity<Attendance>().HasIndex(a => new { a.EventRequestId, a.DeviceHash });
        b.Entity<Evaluation>().HasIndex(a => new { a.EventRequestId, a.DeviceHash });
        // Attendance and Evaluation intentionally have no relationship to each other.
        b.Entity<Attendance>().HasOne<EventRequest>().WithMany().HasForeignKey(a => a.EventRequestId);
        b.Entity<Evaluation>().HasOne<EventRequest>().WithMany().HasForeignKey(a => a.EventRequestId);
    }
}
