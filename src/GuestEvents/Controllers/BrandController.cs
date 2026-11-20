using GuestEvents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuestEvents.Controllers;

/// <summary>Serves the uploaded logo to everyone, including the login and public check-in pages.</summary>
[AllowAnonymous]
public class BrandController : Controller
{
    /// <summary>Imported fonts, served to every page (including login and public pages).</summary>
    [HttpGet("/brand/font/{id}")]
    public IActionResult Font(string id)
    {
        var f = System.Text.RegularExpressions.Regex.IsMatch(id ?? "", "^[a-f0-9]{12}$") ? Fonts.Find(id!) : null;
        if (f == null || !System.IO.File.Exists(Fonts.FilePath(f))) return NotFound();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(Fonts.FilePath(f), Fonts.Mime(f.Ext));
    }

    [HttpGet("/brand/logo")]
    public IActionResult Logo()
    {
        if (!Brand.Has) return NotFound();
        Response.Headers.CacheControl = "public, max-age=86400";   // the page adds ?v=<version>, so a new upload is picked up at once
        return PhysicalFile(Brand.FilePath, "image/png");
    }
}
