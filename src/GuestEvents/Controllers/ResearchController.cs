using GuestEvents.Data;
using GuestEvents.Models;
using GuestEvents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuestEvents.Controllers;

/// <summary>
/// Research uploads: Staff upload a paper or proposal, it is emailed to the research in-charge and recorded.
/// There is no approval step. The Research role (and Admin) browse the archive; staff see their own uploads.
/// </summary>
[Authorize(Policy = "Viewer")]
public class ResearchController : AppController
{
    private readonly AppDb _db;
    private readonly MailService _mail;
    private readonly SettingsService _settings;
    private readonly WorkflowService _wf;
    public ResearchController(AppDb db, MailService mail, SettingsService settings, WorkflowService wf) { _db = db; _mail = mail; _settings = settings; _wf = wf; }

    static readonly string[] Ext = { ".pdf", ".doc", ".docx" };
    const long MailAttachLimit = 10L * 1024 * 1024;

    bool SeesAll => IsAdmin || User.IsInRole(Roles.Research);

    // Archive: everything for the research in-charge and Admin, own uploads for staff.
    public async Task<IActionResult> Index()
    {
        var q = _db.ResearchDocs.AsNoTracking().AsQueryable();
        if (!SeesAll) q = q.Where(r => r.UploaderUsername == Username);
        ViewBag.SeesAll = SeesAll;
        return View(await q.OrderByDescending(r => r.UploadedAt).ToListAsync());
    }

    [Authorize(Policy = "Requester")]
    public async Task<IActionResult> Upload()
    {
        ViewBag.MaxMb = SettingsService.Int(await _settings.AllAsync(), "ResearchMaxMb");
        return View(new ResearchDoc());
    }

    [Authorize(Policy = "Requester"), HttpPost, RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> Upload(ResearchDoc m, IFormFile? file)
    {
        var s = await _settings.AllAsync();
        var maxMb = SettingsService.Int(s, "ResearchMaxMb");
        ViewBag.MaxMb = maxMb;
        ModelState.Remove("file");
        ModelState.Remove("OriginalFileName"); ModelState.Remove("FilePath");
        if (m.DocType != "research" && m.DocType != "proposal") m.DocType = "research";
        if (file == null || file.Length == 0) ModelState.AddModelError("file", "Choose a PDF or Word file.");
        else
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!Ext.Contains(ext)) ModelState.AddModelError("file", "Only PDF, DOC or DOCX files are accepted.");
            else if (file.Length > maxMb * 1024L * 1024) ModelState.AddModelError("file", $"The file is larger than {maxMb} MB.");
            else if (!LooksRight(file, ext)) ModelState.AddModelError("file", "The file content does not match its type.");
        }
        if (!ModelState.IsValid) return View(m);

        var ext2 = Path.GetExtension(file!.FileName).ToLowerInvariant();
        var rel = Path.Combine("research", Guid.NewGuid().ToString("N") + ext2);
        Directory.CreateDirectory(_wf.AppData("research"));
        await using (var fs = System.IO.File.Create(_wf.AppData(rel))) await file.CopyToAsync(fs);

        var d = new ResearchDoc
        {
            Title = m.Title.Trim(), DocType = m.DocType, Authors = m.Authors?.Trim(), Summary = m.Summary?.Trim(),
            OriginalFileName = Path.GetFileName(file.FileName), FilePath = rel, FileSize = file.Length,
            UploaderUsername = Username, UploaderName = DisplayName, UploaderDepartment = Department, UploaderEmail = UserEmail,
        };
        _db.ResearchDocs.Add(d);
        await _db.SaveChangesAsync();
        await _wf.Audit(Username, "ResearchUpload", $"Research {d.Id}: {d.Title}");

        // Email the research in-charge (file attached when small enough) and record whether it went out.
        var to = MailService.Split(s["ResearchEmail"]);
        if (to.Count == 0) to = new List<string> { s["AdminMailbox"] };
        var kind = d.DocType == "proposal" ? "research proposal" : "research paper";
        var attach = d.FileSize <= MailAttachLimit;
        var body = $"New {kind} uploaded.\nTitle: {d.Title}\nAuthors: {d.Authors}\nUploaded by: {d.UploaderName}" +
                   (string.IsNullOrWhiteSpace(d.UploaderDepartment) ? "" : $" ({d.UploaderDepartment})") +
                   $"\nFile: {d.OriginalFileName} ({d.FileSize / 1024} KB)\n\n{d.Summary}\n\n" +
                   (attach ? "The file is attached. " : "The file is too large to attach. ") + $"Archive: {_wf.InternalBase}/Research";
        d.EmailSent = await _mail.SendAsync(null, to, MailService.Split(s["ResearchCc"]), $"Research {(d.DocType == "proposal" ? "proposal" : "paper")}: {d.Title}", body,
            attach ? _wf.AppData(rel) : null, d.OriginalFileName);
        await _db.SaveChangesAsync();

        if (SettingsService.Bool(s, "ResearchNotifySubmitter") && !string.IsNullOrEmpty(d.UploaderEmail))
            await _mail.SendAsync(null, new[] { d.UploaderEmail }, null, $"Research recorded: {d.Title}", $"Your {kind} \"{d.Title}\" was recorded and sent to the research in-charge.\n{_wf.InternalBase}/Research");

        TempData["Saved"] = "1";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Download(int id)
    {
        var d = await _db.ResearchDocs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (d == null || !(SeesAll || d.UploaderUsername == Username)) return NotFound();
        var path = _wf.AppData(d.FilePath);
        if (!System.IO.File.Exists(path)) return NotFound();
        var type = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            _ => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        };
        await _wf.Audit(Username, "ResearchDownload", $"Research {id}");
        return PhysicalFile(path, type, d.OriginalFileName);
    }

    private static bool LooksRight(IFormFile f, string ext)
    {
        using var s = f.OpenReadStream();
        Span<byte> h = stackalloc byte[4];
        if (s.Read(h) < 4) return false;
        return ext switch
        {
            ".pdf" => h[0] == 0x25 && h[1] == 0x50 && h[2] == 0x44 && h[3] == 0x46,               // %PDF
            ".docx" => h[0] == 0x50 && h[1] == 0x4B,                                              // PK (zip)
            _ => h[0] == 0xD0 && h[1] == 0xCF && h[2] == 0x11 && h[3] == 0xE0,                    // old Word
        };
    }
}
