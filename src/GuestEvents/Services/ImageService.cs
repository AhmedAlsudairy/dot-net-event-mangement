using GuestEvents.Models;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace GuestEvents.Services;

/// <summary>
/// Renders the fixed event-image template (1080x1500 PNG, bilingual). Layout is hard-coded here (Admin owns
/// colours / fonts / titles through settings); staff only supply content. HarfBuzz is used so Arabic is shaped correctly.
/// </summary>
public class ImageService
{
    private readonly IWebHostEnvironment _env;
    public ImageService(IWebHostEnvironment env) => _env = env;

    const int W = 1080, H = 1500;

    public byte[] Render(EventRequest e, byte[]? qrPng, Dictionary<string, string> s)
    {
        using var bmp = new SKBitmap(W, H);
        using var c = new SKCanvas(bmp);
        var primary = SKColor.Parse(s["ImgPrimary"]); var accent = SKColor.Parse(s["ImgAccent"]);
        var family = s["ImgFont"];
        c.Clear(SKColors.White);

        using (var p = new SKPaint { Color = primary }) c.DrawRect(0, 0, W, 300, p);
        using (var p = new SKPaint { Color = accent }) c.DrawRect(0, 300, W, 10, p);

        Text(c, s["ImgTitleEn"], family, 54, SKColors.White, W / 2f, 110, bold: true);
        Text(c, s["ImgTitleAr"], family, 54, SKColors.White, W / 2f, 200, bold: true);

        float y = 400;
        Text(c, e.Name, family, 48, primary, W / 2f, y, bold: true, maxWidth: W - 120); y += 80;
        Text(c, e.Presenter, family, 40, SKColors.DimGray, W / 2f, y, maxWidth: W - 120); y += 90;

        // company + logo
        if (!string.IsNullOrEmpty(e.CompanyLogoPath))
        {
            var path = Path.Combine(_env.ContentRootPath, "App_Data", e.CompanyLogoPath);
            if (File.Exists(path))
            {
                using var logo = SKBitmap.Decode(path);
                if (logo != null)
                {
                    float scale = Math.Min(260f / logo.Width, 160f / logo.Height);
                    var dst = SKRect.Create(W / 2f - logo.Width * scale / 2, y, logo.Width * scale, logo.Height * scale);
                    using var img = SKImage.FromBitmap(logo);
                    c.DrawImage(img, dst, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
                    y += 180;
                }
            }
        }
        Text(c, e.CompanyName, family, 36, primary, W / 2f, y, maxWidth: W - 120); y += 70;

        using (var p = new SKPaint { Color = accent, StrokeWidth = 3 }) c.DrawLine(160, y, W - 160, y, p);
        y += 60;

        var ar = new System.Globalization.CultureInfo("ar");
        var en = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        var times = $"{e.StartTime:hh\\:mm} - {e.EndTime:hh\\:mm}";
        if (e.IsMultiDay)
        {
            Text(c, $"{e.Date.ToString("d MMMM", en)} - {e.LastDate.ToString("d MMMM yyyy", en)}", family, 36, SKColors.Black, W / 2f, y); y += 55;
            Text(c, $"من {e.Date.ToString("d MMMM", ar)} إلى {e.LastDate.ToString("d MMMM yyyy", ar)}", family, 36, SKColors.Black, W / 2f, y); y += 60;
            Text(c, times, family, 36, SKColors.Black, W / 2f, y); y += 48;
            // the word "daily" in both languages, drawn separately so mixed text cannot be reordered
            Text(c, "Daily", family, 30, SKColors.DimGray, W / 2f - 90, y); Text(c, "يومياً", family, 30, SKColors.DimGray, W / 2f + 90, y); y += 60;
        }
        else
        {
            Text(c, e.Date.ToString("dddd, d MMMM yyyy", en), family, 36, SKColors.Black, W / 2f, y); y += 55;
            Text(c, $"{e.Date.ToString("dddd d MMMM yyyy", ar)}", family, 36, SKColors.Black, W / 2f, y); y += 65;
            Text(c, times, family, 36, SKColors.Black, W / 2f, y); y += 65;
        }
        Text(c, e.Venue, family, 36, SKColors.Black, W / 2f, y, maxWidth: W - 120); y += 80;

        // content (both languages)
        foreach (var t in new[] { e.ContentEn, e.ContentAr })
        {
            if (string.IsNullOrWhiteSpace(t)) continue;
            foreach (var line in Wrap(t, family, 30, W - 200).Take(4)) { Text(c, line, family, 30, SKColors.DimGray, W / 2f, y); y += 44; }
            y += 14;
        }

        // QR
        // With the rotating QR the printed image carries no QR (the venue screen shows it instead).
        if (qrPng != null)
        {
            using var qr = SKBitmap.Decode(qrPng);
            float size = 300; float top = H - size - 110;
            using var bg = new SKPaint { Color = SKColors.White };
            c.DrawRect(W / 2f - size / 2 - 12, top - 12, size + 24, size + 24, bg);
            using var img = SKImage.FromBitmap(qr);
            c.DrawImage(img, SKRect.Create(W / 2f - size / 2, top, size, size), new SKSamplingOptions(SKFilterMode.Nearest));
            Text(c, "Scan to check in", family, 28, primary, W / 2f - 220, H - 50, bold: true);
            Text(c, "امسح لتسجيل الحضور", family, 28, primary, W / 2f + 220, H - 50, bold: true);
        }

        using var data = SKImage.FromBitmap(bmp).Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static SKTypeface Face(string family, bool bold) =>
        SKTypeface.FromFamilyName(family, bold ? SKFontStyle.Bold : SKFontStyle.Normal) ?? SKTypeface.Default;

    private static void Text(SKCanvas c, string text, string family, float size, SKColor color, float cx, float baseline, bool bold = false, float maxWidth = 0)
    {
        if (string.IsNullOrEmpty(text)) return;
        using var tf = Face(family, bold);
        using var font = new SKFont(tf, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        using var shaper = new SKShaper(tf);
        text = FixRtlDigits(text);
        float w = shaper.Shape(text, font).Width;
        // shrink to fit one line
        if (maxWidth > 0 && w > maxWidth) { font.Size = size * maxWidth / w; w = maxWidth; }
        c.DrawShapedText(shaper, text, cx - w / 2, baseline, SKTextAlign.Left, font, paint);
    }

    private static readonly System.Text.RegularExpressions.Regex Digits = new(@"\d+");
    // HarfBuzz lays an Arabic run out right-to-left; reverse digit runs so numbers still read left-to-right.
    private static string FixRtlDigits(string t) =>
        t.Any(ch => ch >= '؀' && ch <= 'ۿ')
            ? Digits.Replace(t, m => new string(m.Value.Reverse().ToArray()))
            : t;

    private static IEnumerable<string> Wrap(string text, string family, float size, float maxWidth)
    {
        using var tf = Face(family, false);
        using var font = new SKFont(tf, size);
        foreach (var para in text.Replace("\r", "").Split('\n'))
        {
            var words = para.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cur = "";
            foreach (var w in words)
            {
                var next = cur.Length == 0 ? w : cur + " " + w;
                if (font.MeasureText(next) > maxWidth && cur.Length > 0) { yield return cur; cur = w; }
                else cur = next;
            }
            if (cur.Length > 0) yield return cur;
        }
    }
}
