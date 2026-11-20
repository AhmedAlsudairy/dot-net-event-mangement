using GuestEvents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuestEvents.Controllers;

/// <summary>Serves the uploaded logo to everyone, including the login and public check-in pages.</summary>
[AllowAnonymous]
public class BrandController : Controller
{
    [HttpGet("/brand/logo")]
    public IActionResult Logo()
    {
        if (!Brand.Has) return NotFound();
        Response.Headers.CacheControl = "public, max-age=86400";   // the page adds ?v=<version>, so a new upload is picked up at once
        return PhysicalFile(Brand.FilePath, "image/png");
    }
}
