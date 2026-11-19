namespace Kpi.Dashboard.Web.Models;

/// <summary>An open question from the workbook's review tab that a person must confirm.</summary>
public class ReviewItem
{
    public int Id { get; set; }
    public int Seq { get; set; }
    public string Category { get; set; } = "";
    public string Item { get; set; } = "";
    public string Details { get; set; } = "";
    public string SuggestedAction { get; set; } = "";
    public bool IsResolved { get; set; }
}
