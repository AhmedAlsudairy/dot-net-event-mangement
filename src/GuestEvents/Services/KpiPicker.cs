using Kpi.Dashboard.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Services;

/// <summary>Feeds the "Which KPI does this help?" dropdown on event and visit forms from the KPI dashboard database.</summary>
public class KpiPicker
{
    private readonly AppDbContext _kpi;
    public KpiPicker(AppDbContext kpi) { _kpi = kpi; }

    public record Option(int Id, string Label, string Group);

    private static string Short(string s, int max) { s = (s ?? "").Replace('\n', ' ').Trim(); return s.Length <= max ? s : s[..(max - 1)] + "…"; }

    public async Task<List<Option>> OptionsAsync()
    {
        // The model's query filter already hides out-of-plan KPIs.
        var rows = await _kpi.Kpis.AsNoTracking().OrderBy(k => k.Domain).ThenBy(k => k.Sn).Select(k => new { k.Id, k.Sn, k.KpiText, k.Domain }).ToListAsync();
        return rows.Select(k => new Option(k.Id, Short($"{k.Sn} - {k.KpiText}", 140), string.IsNullOrWhiteSpace(k.Domain) ? "-" : k.Domain)).ToList();
    }

    /// <summary>Text saved with the event/visit; null when nothing (or an unknown KPI) is chosen.</summary>
    public async Task<string?> LabelAsync(int? id)
    {
        if (id is null) return null;
        var k = await _kpi.Kpis.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Sn, x.KpiText }).FirstOrDefaultAsync();
        return k is null ? null : Short($"{k.Sn} - {k.KpiText}", 400);
    }
}
