using Kpi.Dashboard.Web.Data;
using Kpi.Dashboard.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Kpi.Dashboard.Web.Pages.Admin;

[Authorize(Policy = Permissions.KpiManage)]
public class ReviewModel : PageModel
{
    private readonly AppDbContext _db;

    private readonly GuestEvents.Services.AuditService _audit;
    public ReviewModel(AppDbContext db, GuestEvents.Services.AuditService audit)
    {
        _audit = audit;
        _db = db;
    }

    public List<ReviewItem> Items { get; set; } = new();

    public void OnGet() => Items = _db.ReviewItems.OrderBy(r => r.IsResolved).ThenBy(r => r.Seq).ToList();

    public IActionResult OnPostToggle(int id)
    {
        var item = _db.ReviewItems.Find(id);
        if (item is not null)
        {
            item.IsResolved = !item.IsResolved;
            _db.SaveChanges();
            _audit.Log(User.Identity?.Name ?? "?", "KpiReviewToggled", $"Review item {item.Seq} -> {(item.IsResolved ? "resolved" : "reopened")}");
        }
        return RedirectToPage();
    }
}
