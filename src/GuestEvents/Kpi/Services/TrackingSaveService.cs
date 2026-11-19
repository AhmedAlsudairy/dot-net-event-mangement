using Kpi.Dashboard.Web.Data;
using Kpi.Dashboard.Web.Models;

namespace Kpi.Dashboard.Web.Services;

public enum SaveOutcome { Saved, Forbidden, Invalid }

/// <summary>
/// Applies an entity-assignment save for one KPI/plan. Never trusts the
/// client for the permission check. What each part of the payload may change
/// depends on the caller's permissions (assign / edit-all / edit-own); parts the
/// caller isn't allowed to touch are ignored outright, not just re-validated.
/// </summary>
public class TrackingSaveService
{
    private readonly AppDbContext _db;

    public TrackingSaveService(AppDbContext db)
    {
        _db = db;
    }

    public SaveOutcome Save(SaveTrackingRequest request, ISet<string> permissions, int? userEntityId, int? userId)
    {
        // Assigning implies being able to fill in the assigned row's fields.
        bool canAssign = permissions.Contains(Permissions.TrackingAssign);
        bool canEditAny = canAssign || permissions.Contains(Permissions.TrackingEditAll);
        bool canEditOwn = permissions.Contains(Permissions.TrackingEditOwn) && userEntityId.HasValue;

        if (!canEditAny && !canEditOwn) return SaveOutcome.Forbidden;
        if (!_db.Kpis.Any(k => k.Id == request.KpiId)) return SaveOutcome.Invalid;

        var existing = _db.TrackingItems.Where(t => t.KpiId == request.KpiId && t.Plan == request.Plan).ToList();
        var submitted = request.Items.GroupBy(i => i.EntityId).Select(g => g.Last()).ToDictionary(i => i.EntityId);

        bool IsOwn(int entityId) => canEditOwn && entityId == userEntityId;

        // Removing an assignment or adding a new one: assign right, or own entity row only.
        foreach (var row in existing.Where(e => !submitted.ContainsKey(e.EntityId)))
            if (canAssign || IsOwn(row.EntityId)) _db.TrackingItems.Remove(row);

        foreach (var (entityId, dto) in submitted)
        {
            var match = existing.FirstOrDefault(e => e.EntityId == entityId);
            if (match is null)
            {
                if (canAssign || IsOwn(entityId)) _db.TrackingItems.Add(NewItem(request.KpiId, request.Plan, dto, userId));
            }
            else if (canEditAny || IsOwn(entityId))
            {
                ApplyFields(match, dto, userId);
            }
            // Anything else in the payload is ignored outright, never trusted.
        }

        _db.SaveChanges();
        return SaveOutcome.Saved;
    }

    private static TrackingItem NewItem(int kpiId, PlanType plan, TrackingItemDto dto, int? userId) => new()
    {
        KpiId = kpiId,
        Plan = plan,
        EntityId = dto.EntityId,
        Target = dto.Target ?? "",
        Deadline = ParseDate(dto.Deadline),
        Status = ParseStatus(dto.Status),
        UpdatedByUserId = userId,
        UpdatedAtUtc = DateTime.UtcNow
    };

    private static void ApplyFields(TrackingItem item, TrackingItemDto dto, int? userId)
    {
        item.Target = dto.Target ?? "";
        item.Deadline = ParseDate(dto.Deadline);
        item.Status = ParseStatus(dto.Status);
        item.UpdatedByUserId = userId;
        item.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static DateOnly? ParseDate(string? s) =>
        !string.IsNullOrWhiteSpace(s) && DateOnly.TryParse(s, out var d) ? d : null;

    private static ExecutionStatus? ParseStatus(string? s) =>
        !string.IsNullOrWhiteSpace(s) && Enum.TryParse<ExecutionStatus>(s, out var st) ? st : null;
}
