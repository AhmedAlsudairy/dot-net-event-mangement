using ClosedXML.Excel;
using Kpi.Dashboard.Web.Data;
using Kpi.Dashboard.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Kpi.Dashboard.Web.Services;

/// <summary>Server-side replacement for the original exportFullExcelReport().</summary>
public class ExcelExportService
{
    private readonly AppDbContext _db;
    private static readonly Dictionary<PlanType, string> PlanLabelAr = new()
    {
        [PlanType.Operational] = "الخطة التشغيلية",
        [PlanType.Annual] = "الخطة السنوية",
        [PlanType.Ejada] = "الإجادة المؤسسية"
    };
    private static readonly Dictionary<ExecutionStatus, string> StatusLabelAr = new()
    {
        [ExecutionStatus.Done] = "منفذ",
        [ExecutionStatus.InProgress] = "قيد التنفيذ",
        [ExecutionStatus.NotStarted] = "لم ينفذ"
    };

    public ExcelExportService(AppDbContext db)
    {
        _db = db;
    }

    public byte[] ExportFullReport(int? scopeEntityId = null)
    {
        var kpis = _db.Kpis.AsNoTracking().OrderBy(k => k.Sn).ToList();
        var itemsQuery = _db.TrackingItems.AsNoTracking().Include(t => t.EntityRef).AsQueryable();
        if (scopeEntityId.HasValue) itemsQuery = itemsQuery.Where(t => t.EntityId == scopeEntityId.Value);
        var itemsByKpiPlan = itemsQuery.ToList()
            .GroupBy(t => (t.KpiId, t.Plan))
            .ToDictionary(g => g.Key, g => g.ToList());

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("KPI_Execution_Report");
        string[] headers = { "S.N.", "Classification", "Domain", "SP Goal", "Strategic Objective", "KPI",
            "Plan Type", "Plan Text", "Entity", "Target", "Deadline", "Status" };
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;

        int r = 2;
        foreach (var kpi in kpis)
        {
            foreach (PlanType plan in Enum.GetValues<PlanType>())
            {
                var planText = kpi.PlanText(plan);
                if (string.IsNullOrWhiteSpace(planText)) continue;

                var items = itemsByKpiPlan.TryGetValue((kpi.Id, plan), out var list) ? list : new List<TrackingItem>();
                if (items.Count == 0)
                {
                    if (scopeEntityId.HasValue) continue;
                    WriteRow(ws, r++, kpi, plan, planText, "", "", "", "");
                }
                else
                {
                    foreach (var it in items)
                    {
                        WriteRow(ws, r++, kpi, plan, planText,
                            it.EntityRef?.NameAr ?? "",
                            it.Target,
                            it.Deadline?.ToString("yyyy-MM-dd") ?? "",
                            it.Status.HasValue ? StatusLabelAr[it.Status.Value] : "");
                    }
                }
            }
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteRow(IXLWorksheet ws, int r, Models.Kpi kpi, PlanType plan, string planText,
        string entity, string target, string deadline, string status)
    {
        ws.Cell(r, 1).Value = kpi.Sn;
        ws.Cell(r, 2).Value = kpi.Classification;
        ws.Cell(r, 3).Value = kpi.Domain;
        ws.Cell(r, 4).Value = kpi.Goal;
        ws.Cell(r, 5).Value = kpi.Objective;
        ws.Cell(r, 6).Value = kpi.KpiText;
        ws.Cell(r, 7).Value = PlanLabelAr[plan];
        ws.Cell(r, 8).Value = planText;
        ws.Cell(r, 9).Value = entity;
        ws.Cell(r, 10).Value = target;
        ws.Cell(r, 11).Value = deadline;
        ws.Cell(r, 12).Value = status;
    }

    /// <summary>
    /// A downloadable starting point with exactly the headers the importer
    /// looks for (ExcelImportService.FindCol matches by header text, not
    /// column position, so order doesn't matter — but the header *text* must
    /// match). Includes two worked examples showing the forward-fill
    /// convention for Classification/Domain (leave the cell blank on rows
    /// that continue the same group, same as a merged cell would look).
    /// </summary>
    public byte[] GenerateImportTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sheet1");

        string[] headers =
        {
            "KPI Classification (ESG)", "S.N.", "Domain", "SP Goal Ref", "Strategic Objective",
            "KPI", "Operational Plan", "Annual Plan", "Ejada", "Accreditation Criteria",
            "Branch Target 2026", "Update Notes", "Prev. SN"
        };
        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        var headerRow = ws.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#1f3c88");
        headerRow.Style.Font.FontColor = XLColor.White;

        object[][] sample =
        {
            new object[] { "Governance", "1", "Domain1: Governance & Management", "Strategic Goal 1: Enhance institutional governance", "SO1: Enhance regulatory frameworks",
                "1.1.1.1 Percentage of work-related regulations developed and reviewed", "SO1: Enhance regulatory frameworks — 1.1.1.1 Percentage of work-related regulations developed and reviewed",
                "", "", "Criterion 1.7: Indicator (a) — formal documented policy process." },
            // Same Classification/Domain group as row above: leave both blank to continue it (forward-fill),
            // exactly like the real source file does for consecutive rows under one domain.
            new object[] { "", "2", "", "Strategic Goal 1: Enhance institutional governance", "SO1: Enhance regulatory frameworks",
                "1.1.1.2 Percentage of compliance with approved regulations", "SO1: Enhance regulatory frameworks — 1.1.1.2 Percentage of compliance with approved regulations",
                "نسبة الالتزام بالسياسات المعتمدة", "", "Criterion 1.7: Indicator (g) — policies deployed in practice." },
            new object[] { "Social", "3", "Domain 5: Student Experience", "Strategic Goal 1: Provide comprehensive student support", "SO1: Enhance student services",
                "5.1.1.1 Student satisfaction rate with support services", "SO1: Enhance student services — 5.1.1.1 Student satisfaction rate with support services",
                "", "نسبة رضا الطلبة عن الخدمات المساندة", "" }
        };
        for (int r = 0; r < sample.Length; r++)
            for (int c = 0; c < sample[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = sample[r][c]?.ToString() ?? "";

        ws.Columns().AdjustToContents();
        foreach (var col in ws.ColumnsUsed()) if (col.Width > 55) col.Width = 55;

        var notes = workbook.Worksheets.Add("ملاحظات - Notes");
        notes.Cell(1, 1).Value = "ملاحظات على نموذج الاستيراد";
        notes.Cell(1, 1).Style.Font.Bold = true;
        notes.Cell(1, 1).Style.Font.FontSize = 14;
        string[] lines =
        {
            "• يُقرأ من الملف: التصنيف، المجال، الهدف الاستراتيجي، الهدف الفرعي، نص المؤشر، ونص كل خطة (تشغيلية/سنوية/إجادة)، ومعيار الاعتماد.",
            "• أسماء الأعمدة في الصف الأول هي التي تُحدد كل عمود — وليس ترتيبها، لذا يمكن إعادة ترتيب الأعمدة بحرية طالما بقيت الأسماء كما هي.",
            "• عند تكرار نفس التصنيف أو المجال لعدة أسطر متتالية، اترك الخلية فارغة في الأسطر التالية (كما في الصف الثاني من هذا النموذج) بدلاً من تكرار القيمة — يتم تعبئتها تلقائيًا من آخر قيمة ظاهرة.",
            "• عمود S.N. مطلوب ويجب أن يكون فريدًا — إعادة استيراد ملف بنفس أرقام S.N. يُحدّث المؤشرات الموجودة بدلاً من تكرارها.",
            "• إسناد الجهات، والمستهدف، والموعد النهائي، وموقف التنفيذ لا تُقرأ من ملف Excel إطلاقًا — تُدخل من صفحة \"جدول التتبع\" بعد الاستيراد.",
            "• الورقة الأولى فقط هي التي تُقرأ؛ يفضَّل تسميتها بنفس اسم ورقة هذا النموذج أو ترك الاسم \"Sheet1\"."
        };
        for (int i = 0; i < lines.Length; i++) notes.Cell(i + 3, 1).Value = lines[i];
        notes.Column(1).Width = 110;
        notes.Style.Alignment.WrapText = true;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
