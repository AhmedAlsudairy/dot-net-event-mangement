using GuestEvents.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuestEvents.Controllers;

[Authorize(Policy = "Viewer")]
public class HelpController : AppController
{
    public IActionResult Index(string? tab)
    {
        // open the tab that matches the person's main role unless a tab was asked for
        ViewBag.Tab = tab ?? (IsAdmin ? "admin" : User.IsInRole(Roles.Hod) ? "hod" : User.IsInRole(Roles.Media) ? "media" : User.IsInRole(Roles.Research) && !User.IsInRole(Roles.Staff) ? "research" : "staff");
        return View();
    }
}
