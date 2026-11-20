using Kpi.Dashboard.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Services;

/// <summary>Feeds the "Which KPI does this help?" dropdown on event and visit forms from the KPI dashboard database.</summary>
public class KpiPicker
{
    private readonly AppDbContext _kpi;
    public KpiPicker(AppDbContext kpi) { _kpi = kpi; }

    public record Option(int Id, string Sn, string Text, string Domain, string Goal, string Label);

    private static string Short(string s, int max) { s = (s ?? "").Replace('\n', ' ').Trim(); return s.Length <= max ? s : s[..(max - 1)] + "…"; }

    private static readonly System.Text.RegularExpressions.Regex CodeRx = new(@"^\s*(\d+(?:\.\d+){1,6})[\s.:)-]+(.+)$", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>The imported workbook often starts the KPI text with its real code ("2.1.6.1 Percentage of..."), while Sn is only a row number. Show the real code as the badge.</summary>
    public static (string code, string text) CodeAndText(string? sn, string? kpiText)
    {
        var t = (kpiText ?? "").Replace('\n', ' ').Trim();
        var m = CodeRx.Match(t);
        return m.Success ? (m.Groups[1].Value, m.Groups[2].Value.Trim()) : ((sn ?? "").Trim(), t);
    }

    /// <summary>Splits a saved "code - text" label back into its two parts for display.</summary>
    public static (string code, string text) SplitLabel(string? label)
    {
        var s = (label ?? "").Trim(); var i = s.IndexOf(" - ", StringComparison.Ordinal);
        return i > 0 && i <= 14 ? (s[..i], s[(i + 3)..]) : ("", s);
    }

    private static readonly System.Text.RegularExpressions.Regex DomainRx = new(@"^\s*Domain\s*(\d+)\s*[:\-]?\s*(.*)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>"Domain1: Governance" and "Domain 1: Governance" are the same domain: show them the same way, and order domains by their number.</summary>
    private static (int order, string name) DomainInfo(string? domain)
    {
        var d = (domain ?? "").Trim();
        var m = DomainRx.Match(d);
        return m.Success ? (int.Parse(m.Groups[1].Value), $"Domain {m.Groups[1].Value}: {m.Groups[2].Value}".TrimEnd(' ', ':')) : (int.MaxValue, d);
    }

    /// <summary>Compares KPI numbers like 1.2.10 and 1.2.9 the way people read them (by number, not letter by letter).</summary>
    private static int Natural(string a, string b)
    {
        var x = (a ?? "").Split('.', '-', ' '); var y = (b ?? "").Split('.', '-', ' ');
        for (int i = 0; i < Math.Min(x.Length, y.Length); i++)
        {
            var c = int.TryParse(x[i], out var p) && int.TryParse(y[i], out var q) ? p.CompareTo(q) : string.Compare(x[i], y[i], StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
        }
        return x.Length.CompareTo(y.Length);
    }

    public async Task<List<Option>> OptionsAsync()
    {
        // The model's query filter already hides out-of-plan KPIs.
        var rows = await _kpi.Kpis.AsNoTracking().Select(k => new { k.Id, k.Sn, k.KpiText, k.Domain, k.Goal }).ToListAsync();
        return rows.Select(k => new { k, dom = DomainInfo(k.Domain), ct = CodeAndText(k.Sn, k.KpiText) })
            .OrderBy(x => x.dom.order).ThenBy(x => x.dom.name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.ct.code, Comparer<string>.Create(Natural))
            .Select(x => new Option(x.k.Id, x.ct.code, Short(x.ct.text, 300), x.dom.name, Short(x.k.Goal, 200), Short($"{x.ct.code} - {x.ct.text}", 140))).ToList();
    }

    /// <summary>Text saved with the event/visit; null when nothing (or an unknown KPI) is chosen.</summary>
    public async Task<string?> LabelAsync(int? id)
    {
        if (id is null) return null;
        var k = await _kpi.Kpis.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Sn, x.KpiText }).FirstOrDefaultAsync();
        if (k is null) return null;
        var (code, text) = CodeAndText(k.Sn, k.KpiText);
        return Short($"{code} - {text}", 400);
    }
}
