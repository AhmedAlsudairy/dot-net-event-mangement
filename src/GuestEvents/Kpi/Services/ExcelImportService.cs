using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Kpi.Dashboard.Web.Data;
using Kpi.Dashboard.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Kpi.Dashboard.Web.Services;

public class SheetImportResult
{
    public string SheetName { get; set; } = "";
    public string Kind { get; set; } = "";
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
}

public class ImportResult
{
    public List<SheetImportResult> Sheets { get; } = new();
    public int Inserted => Sheets.Sum(s => s.Inserted);
    public int Updated => Sheets.Sum(s => s.Updated);
    public int Skipped => Sheets.Sum(s => s.Skipped);
}

/// <summary>
/// Reads the multi-tab alignment workbook. Tabs are recognised by their header row, not their
/// position, and reference KPI data only — never the interactive tracking fields.
///
///  * KPI_Alignment: three blocks in one sheet. Rows with a KPI are the branch's operational KPIs;
///    rows without one are annual-plan projects or Ejada indicators that have no operational
///    counterpart (KpiGroup.AnnualOnly / EjadaOnly).
///  * "Outside branch plan" tab (first header "Prev. SN"): KPIs from the old 121 list no longer in
///    the plan. Stored as KpiGroup.OutsideBranchPlan with Sn "OLD-n" so numbers never collide.
///  * Review tab (headers # / الفئة / البند ...): open questions, stored as ReviewItems.
///  * Change-log tab: summary formulas only; nothing to import.
///
/// Fix vs. the original dashboard: the "kpi" column matcher also matched "KPI Classification (ESG)",
/// so it now also excludes "Classification".
/// </summary>
public class ExcelImportService
{
    private readonly AppDbContext _db;

    public ExcelImportService(AppDbContext db)
    {
        _db = db;
    }

    public ImportResult Import(Stream fileStream)
    {
        using var workbook = new XLWorkbook(fileStream);
        var result = new ImportResult();

        // Include out-of-plan KPIs so re-importing updates them instead of colliding on Sn.
        var existingBySn = _db.Kpis.IgnoreQueryFilters().ToDictionary(k => k.Sn, k => k);

        foreach (var ws in workbook.Worksheets)
        {
            var table = ReadTable(ws);
            if (table is null) continue;

            if (table.Header.Any(h => h == "S.N."))
                result.Sheets.Add(ImportAlignment(ws.Name, table, existingBySn));
            else if (table.Header.FirstOrDefault(h => !string.IsNullOrEmpty(h))?.StartsWith("Prev. SN") == true)
                result.Sheets.Add(ImportOutsidePlan(ws.Name, table, existingBySn));
            else if (table.Header.Length >= 5 && table.Header[0] == "#" && table.Header.Any(h => h.Contains("الإجراء")))
                result.Sheets.Add(ImportReview(ws.Name, table));
        }

        if (result.Sheets.Count == 0)
            throw new InvalidOperationException("تعذّر العثور على ورقة مؤشرات (عمود S.N.) في الملف — تحقق من أسماء الأعمدة.");
        if (!result.Sheets.Any(s => s.Kind == "KPI"))
            throw new InvalidOperationException("تعذّر العثور على عمودي S.N. و KPI في الملف — تحقق من أسماء الأعمدة.");

        _db.SaveChanges();
        return result;
    }

    // ---- sheet readers -------------------------------------------------------------------

    private sealed class Table
    {
        public string[] Header { get; init; } = Array.Empty<string>();
        public List<string[]> Rows { get; init; } = new();

        public int Col(string[] mustInclude, string[]? mustExclude = null)
        {
            mustExclude ??= Array.Empty<string>();
            for (int c = 0; c < Header.Length; c++)
            {
                var text = Header[c];
                if (string.IsNullOrEmpty(text)) continue;
                if (mustInclude.All(text.Contains) && mustExclude.All(x => !text.Contains(x))) return c;
            }
            return -1;
        }
    }

    private static Table? ReadTable(IXLWorksheet worksheet)
    {
        var usedRange = worksheet.RangeUsed();
        if (usedRange is null) return null;
        var rows = usedRange.RowsUsed().ToList();
        if (rows.Count == 0) return null;

        int colCount = usedRange.ColumnCount();
        string[] Values(IXLRangeRow row) =>
            Enumerable.Range(1, colCount).Select(c => Clean(row.Cell(c).GetString())).ToArray();

        int headerIdx = 0;
        for (int i = 0; i < Math.Min(rows.Count, 6); i++)
            if (Values(rows[i]).Any(v => v == "S.N.")) { headerIdx = i; break; }

        return new Table { Header = Values(rows[headerIdx]), Rows = rows.Skip(headerIdx + 1).Select(Values).ToList() };
    }

    private static string Get(string[] row, int idx) => idx >= 0 && idx < row.Length ? row[idx] : "";

    private SheetImportResult ImportAlignment(string sheetName, Table t, Dictionary<string, Models.Kpi> existingBySn)
    {
        var classificationCol = t.Col(new[] { "ESG" });
        if (classificationCol < 0) classificationCol = t.Col(new[] { "Environmental" });

        int sn = t.Col(new[] { "S.N." }), domain = t.Col(new[] { "Domain" }), goal = t.Col(new[] { "SP Goal" }),
            objective = t.Col(new[] { "Strategic Objective" }),
            kpi = t.Col(new[] { "KPI" }, new[] { "Ejada", "Plan", "Classification" }),
            opPlan = t.Col(new[] { "Operational Plan" }, new[] { "Responsible", "Deadline", "Status" }),
            ejada = t.Col(new[] { "Ejada" }, new[] { "Responsible", "Deadline", "Status" }),
            annual = t.Col(new[] { "Annual Plan" }, new[] { "Responsible", "Deadline", "Status" }),
            accreditation = t.Col(new[] { "Accreditation" }),
            branchTarget = t.Col(new[] { "Branch Target" }), notes = t.Col(new[] { "Update Notes" }),
            prevSn = t.Col(new[] { "Prev" });

        if (kpi < 0 || sn < 0)
            throw new InvalidOperationException("تعذّر العثور على عمودي S.N. و KPI في الملف — تحقق من أسماء الأعمدة.");

        var result = new SheetImportResult { SheetName = sheetName, Kind = "KPI" };
        string lastClassification = "", lastDomain = "";

        foreach (var row in t.Rows)
        {
            var rowSn = Get(row, sn);
            var kpiText = Get(row, kpi);
            if (string.IsNullOrWhiteSpace(rowSn) && string.IsNullOrWhiteSpace(kpiText)) { result.Skipped++; continue; }

            var annualText = Get(row, annual);
            var ejadaText = Get(row, ejada);

            KpiGroup group;
            if (!string.IsNullOrWhiteSpace(kpiText)) group = KpiGroup.BranchPlan;
            else if (!string.IsNullOrWhiteSpace(annualText)) group = KpiGroup.AnnualOnly;
            else if (!string.IsNullOrWhiteSpace(ejadaText)) group = KpiGroup.EjadaOnly;
            else { result.Skipped++; continue; }

            var classification = Get(row, classificationCol);
            var rowDomain = Get(row, domain);
            if (group == KpiGroup.BranchPlan)
            {
                // Forward-fill only inside the operational block; the other blocks carry no ESG/Domain.
                if (!string.IsNullOrWhiteSpace(classification)) lastClassification = classification; else classification = lastClassification;
                if (!string.IsNullOrWhiteSpace(rowDomain)) lastDomain = rowDomain; else rowDomain = lastDomain;
            }

            // Annual-only / Ejada-only rows have no KPI text; show the project / indicator itself.
            if (group == KpiGroup.AnnualOnly) kpiText = annualText;
            if (group == KpiGroup.EjadaOnly) kpiText = ejadaText;

            Upsert(existingBySn, result, rowSn, k =>
            {
                k.Group = group;
                k.Classification = classification;
                k.Domain = rowDomain;
                k.Goal = Get(row, goal);
                k.Objective = Get(row, objective);
                k.KpiText = kpiText;
                k.OperationalPlanText = Get(row, opPlan);
                k.AnnualPlanText = annualText;
                k.EjadaPlanText = ejadaText;
                k.AccreditationCriteria = Get(row, accreditation);
                // Older files lack these columns; don't blank values already stored.
                if (branchTarget >= 0) k.BranchTarget2026 = Get(row, branchTarget);
                if (notes >= 0) k.UpdateNotes = Get(row, notes);
                if (prevSn >= 0) k.PrevSn = Get(row, prevSn);
            });
        }
        return result;
    }

    private SheetImportResult ImportOutsidePlan(string sheetName, Table t, Dictionary<string, Models.Kpi> existingBySn)
    {
        int prev = 0,
            classification = t.Col(new[] { "Classification" }), domain = t.Col(new[] { "Domain" }), goal = t.Col(new[] { "SP Goal" }),
            objective = t.Col(new[] { "Strategic Objective" }),
            kpi = t.Col(new[] { "KPI" }, new[] { "Classification" }),
            annual = t.Col(new[] { "Annual Plan" }), ejada = t.Col(new[] { "Ejada" }),
            accreditation = t.Col(new[] { "Accreditation" });

        var result = new SheetImportResult { SheetName = sheetName, Kind = "OutsidePlan" };
        foreach (var row in t.Rows)
        {
            var prevSn = Get(row, prev);
            var kpiText = Get(row, kpi);
            if (string.IsNullOrWhiteSpace(prevSn) || string.IsNullOrWhiteSpace(kpiText)) { result.Skipped++; continue; }

            Upsert(existingBySn, result, "OLD-" + prevSn, k =>
            {
                k.Group = KpiGroup.OutsideBranchPlan;
                k.Classification = Get(row, classification);
                k.Domain = Get(row, domain);
                k.Goal = Get(row, goal);
                k.Objective = Get(row, objective);
                k.KpiText = kpiText;
                k.AnnualPlanText = Get(row, annual);
                k.EjadaPlanText = Get(row, ejada);
                k.AccreditationCriteria = Get(row, accreditation);
                k.PrevSn = prevSn;
            });
        }
        return result;
    }

    private SheetImportResult ImportReview(string sheetName, Table t)
    {
        var result = new SheetImportResult { SheetName = sheetName, Kind = "Review" };
        var existing = _db.ReviewItems.ToList();

        foreach (var row in t.Rows)
        {
            if (!int.TryParse(Get(row, 0), out var seq) || string.IsNullOrWhiteSpace(Get(row, 2))) { result.Skipped++; continue; }

            // Keep a person's "resolved" mark across re-imports of the same item.
            var match = existing.FirstOrDefault(r => r.Category == Get(row, 1) && r.Item == Get(row, 2));
            if (match is null)
            {
                _db.ReviewItems.Add(new ReviewItem { Seq = seq, Category = Get(row, 1), Item = Get(row, 2), Details = Get(row, 3), SuggestedAction = Get(row, 4) });
                result.Inserted++;
            }
            else
            {
                match.Seq = seq; match.Details = Get(row, 3); match.SuggestedAction = Get(row, 4);
                result.Updated++;
            }
        }
        return result;
    }

    private void Upsert(Dictionary<string, Models.Kpi> bySn, SheetImportResult result, string sn, Action<Models.Kpi> apply)
    {
        if (bySn.TryGetValue(sn, out var existing))
        {
            apply(existing);
            result.Updated++;
        }
        else
        {
            var k = new Models.Kpi { Sn = sn };
            apply(k);
            _db.Kpis.Add(k);
            bySn[sn] = k;
            result.Inserted++;
        }
    }

    private static string Clean(string? v) => Regex.Replace(v ?? "", @"\s+", " ").Trim();
}
