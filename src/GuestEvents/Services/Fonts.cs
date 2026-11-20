using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GuestEvents.Services;

/// <summary>
/// The typeface of the platform, set by the Admin at Admin > Appearance: one font for English pages and one for Arabic pages
/// (chosen from a built-in list or imported as a font file), plus the default text size.
/// Built-in fonts load from Google Fonts; imported fonts are kept in App_Data/fonts and served from this app (so they also work offline).
/// </summary>
public static class Fonts
{
    public record Builtin(string Key, string Name, string? Google, bool Arabic);
    public record Custom(string Id, string Name, string Ext, long Size);

    private const string SystemStack = "-apple-system,\"Segoe UI\",Roboto,\"Helvetica Neue\",Tahoma,Arial,sans-serif";

    public static readonly Builtin[] Latin =
    {
        new("Inter", "Inter", "Inter:wght@400;500;600;700", false), new("Roboto", "Roboto", "Roboto:wght@400;500;700", false),
        new("Open Sans", "Open Sans", "Open+Sans:wght@400;500;600;700", false), new("Poppins", "Poppins", "Poppins:wght@400;500;600;700", false),
        new("Lato", "Lato", "Lato:wght@400;700", false), new("Nunito", "Nunito", "Nunito:wght@400;500;600;700", false),
        new("Montserrat", "Montserrat", "Montserrat:wght@400;500;600;700", false),
    };
    public static readonly Builtin[] ArabicCapable =
    {
        new("Cairo", "Cairo", "Cairo:wght@400;500;600;700", true), new("Tajawal", "Tajawal", "Tajawal:wght@400;500;700", true),
        new("IBM Plex Sans Arabic", "IBM Plex Sans Arabic", "IBM+Plex+Sans+Arabic:wght@400;500;600;700", true),
        new("Noto Sans Arabic", "Noto Sans Arabic", "Noto+Sans+Arabic:wght@400;500;600;700", true), new("Almarai", "Almarai", "Almarai:wght@400;700", true),
        new("Rubik", "Rubik", "Rubik:wght@400;500;600;700", true), new("Readex Pro", "Readex Pro", "Readex+Pro:wght@400;500;600;700", true),
        new("Noto Kufi Arabic", "Noto Kufi Arabic", "Noto+Kufi+Arabic:wght@400;500;600;700", true), new("Amiri", "Amiri (serif)", "Amiri:wght@400;700", true),
    };
    public static IEnumerable<Builtin> AllBuiltin => Latin.Concat(ArabicCapable);
    public static readonly int[] Scales = { 90, 100, 110, 120 };

    private static string _dir = "";
    private static List<Custom> _custom = new();
    public static string Css { get; private set; } = "";
    public static string HeadHtml { get; private set; } = "";
    public static int Scale { get; private set; } = 100;
    public static string En { get; private set; } = "system";
    public static string Ar { get; private set; } = "system";
    public static IReadOnlyList<Custom> CustomFonts => _custom;
    public static string Dir => _dir;
    public static long Version { get; private set; }

    public static void Init(string contentRoot) { _dir = Path.Combine(contentRoot, "App_Data", "fonts"); Directory.CreateDirectory(_dir); }

    public static List<Custom> ParseList(string? json)
    {
        try { return string.IsNullOrWhiteSpace(json) ? new() : (JsonSerializer.Deserialize<List<Custom>>(json) ?? new()).Where(c => Regex.IsMatch(c.Id, "^[a-f0-9]{12}$")).ToList(); }
        catch { return new(); }
    }
    public static string ToJson(List<Custom> list) => JsonSerializer.Serialize(list);
    public static string FilePath(Custom c) => Path.Combine(_dir, c.Id + c.Ext);
    public static Custom? Find(string id) => _custom.FirstOrDefault(c => c.Id == id);
    public static string Mime(string ext) => ext switch { ".woff2" => "font/woff2", ".woff" => "font/woff", ".otf" => "font/otf", _ => "font/ttf" };
    public static string FaceName(Custom c) => "UserFont-" + c.Id;

    /// <summary>"system", a built-in name, or "custom:{id}" of an imported font that still exists.</summary>
    public static bool IsValidKey(string? key) =>
        key == "system" || AllBuiltin.Any(b => b.Key == key) || (key != null && key.StartsWith("custom:") && _custom.Any(c => "custom:" + c.Id == key));

    /// <summary>The CSS font stack for a choice, always ending in the system fonts.</summary>
    public static string Stack(string key)
    {
        if (key.StartsWith("custom:") && _custom.FirstOrDefault(c => "custom:" + c.Id == key) is { } c) return $"\"{FaceName(c)}\",{SystemStack}";
        var b = AllBuiltin.FirstOrDefault(x => x.Key == key);
        return b != null ? $"\"{b.Name.Replace(" (serif)", "")}\",{SystemStack}" : SystemStack;
    }
    public static string? GoogleSpec(string key) => AllBuiltin.FirstOrDefault(x => x.Key == key)?.Google;

    public static void Apply(Dictionary<string, string> s)
    {
        _custom = ParseList(s.GetValueOrDefault("CustomFonts"));
        var en = s.GetValueOrDefault("FontEn") ?? "system"; var ar = s.GetValueOrDefault("FontAr") ?? "system";
        En = IsValidKey(en) ? en : "system"; Ar = IsValidKey(ar) ? ar : "system";
        Scale = int.TryParse(s.GetValueOrDefault("FontScale"), out var sc) && Scales.Contains(sc) ? sc : 100;
        Version = DateTime.UtcNow.Ticks;
        var css = new StringBuilder();
        foreach (var c in _custom)
            css.Append($"@font-face{{font-family:\"{FaceName(c)}\";src:url(/brand/font/{c.Id}?v={c.Size}) format(\"{(c.Ext == ".woff2" ? "woff2" : c.Ext == ".woff" ? "woff" : c.Ext == ".otf" ? "opentype" : "truetype")}\");font-display:swap}}");
        css.Append($":root{{--font-en:{Stack(En)};--font-ar:{Stack(Ar)};--font-scale:{(Scale / 100.0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)};--bs-body-font-family:var(--font-body)}}");
        css.Append("html[lang=ar]{--font-body:var(--font-ar),var(--font-en)}html:not([lang=ar]){--font-body:var(--font-en),var(--font-ar)}");
        Css = css.ToString();
        var fams = new[] { En, Ar }.Distinct().Select(GoogleSpec).Where(x => x != null).ToList();
        HeadHtml = fams.Count == 0 ? "" : "<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\" /><link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin />"
            + $"<link rel=\"stylesheet\" href=\"https://fonts.googleapis.com/css2?{string.Join("&", fams.Select(f => "family=" + f))}&display=swap\" />";
    }

    /// <summary>Checks a chosen font file and saves it. The type is decided by the file's first bytes, not by its name.</summary>
    public static string? TryAdd(Stream upload, long length, string fileName, string? displayName, int existing, out Custom? added)
    {
        added = null;
        if (existing >= 10) return "You can import up to 10 fonts. Delete one first.";
        if (length <= 0) return "Choose a font file first.";
        if (length > 3_000_000) return "The font file is too large (3 MB at most).";
        using var ms = new MemoryStream(); upload.CopyTo(ms); var b = ms.ToArray();
        if (b.Length < 16) return "That file is not a font.";
        string? ext = null;
        if (b[0] == 'w' && b[1] == 'O' && b[2] == 'F' && b[3] == '2') ext = ".woff2";
        else if (b[0] == 'w' && b[1] == 'O' && b[2] == 'F' && b[3] == 'F') ext = ".woff";
        else if (b[0] == 'O' && b[1] == 'T' && b[2] == 'T' && b[3] == 'O') ext = ".otf";
        else if ((b[0] == 0 && b[1] == 1 && b[2] == 0 && b[3] == 0) || (b[0] == 't' && b[1] == 'r' && b[2] == 'u' && b[3] == 'e')) ext = ".ttf";
        if (ext == null) return "That file is not a usable font. Use WOFF2, WOFF, TTF or OTF.";
        var name = Regex.Replace((string.IsNullOrWhiteSpace(displayName) ? Path.GetFileNameWithoutExtension(fileName) : displayName).Trim(), @"[^\p{L}\p{N} ._-]", "");
        if (name.Length == 0) name = "Imported font";
        if (name.Length > 40) name = name[..40];
        var id = Guid.NewGuid().ToString("N")[..12];
        File.WriteAllBytes(Path.Combine(_dir, id + ext), b);
        added = new Custom(id, name, ext, b.Length);
        return null;
    }

    public static void DeleteFile(Custom c) { try { File.Delete(FilePath(c)); } catch { } }
}
