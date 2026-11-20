using System.Globalization;
using System.Text;

namespace GuestEvents.Services;

/// <summary>
/// The look of the whole platform, set by the Admin at Admin > Appearance: four colours, light / dark / automatic mode and the corner style.
/// All colours the pages use come from CSS variables; this class turns the chosen colours into those variables
/// (including the softer tints and the dark-mode versions) and keeps the result ready for the page layouts.
/// </summary>
public static class Theme
{
    public record Preset(string Key, string En, string Ar, string Primary, string Secondary, string Accent, string Bg);

    public static readonly Preset[] Presets =
    {
        new("ocean", "Ocean", "المحيط", "#0B3D5C", "#14587F", "#E0A526", "#F4F6F9"),
        new("emerald", "Emerald", "الزمرد", "#0F5132", "#1B7F52", "#F2B705", "#F3F7F5"),
        new("royal", "Royal purple", "الأرجواني", "#3B2A7A", "#5B45B0", "#F5A524", "#F6F5FA"),
        new("crimson", "Crimson", "القرمزي", "#7A1F2B", "#A8323F", "#E8B04B", "#F8F5F5"),
        new("teal", "Teal", "الفيروزي", "#0B5F66", "#0E8A92", "#F59E0B", "#F2F7F7"),
        new("sunset", "Sunset", "الغروب", "#9A3412", "#C2410C", "#FBBF24", "#FAF5F0"),
        new("rose", "Rose", "الوردي", "#9D174D", "#BE185D", "#F59E0B", "#FAF5F7"),
        new("graphite", "Graphite", "الرمادي الداكن", "#24303D", "#3F5266", "#E0A526", "#F3F4F6"),
    };

    public static readonly string[] Modes = { "light", "dark", "auto" };
    public static readonly string[] Radii = { "soft", "sharp", "round" };

    /// <summary>The CSS variables for the saved theme, ready to put in a style tag.</summary>
    public static string Css { get; private set; } = Build("ocean-default", Presets[0].Primary, Presets[0].Secondary, Presets[0].Accent, Presets[0].Bg, "soft", ":root", "[data-bs-theme=dark]");
    public static string Mode { get; private set; } = "light";
    /// <summary>When true, people cannot choose their own light/dark mode; everyone gets the Admin's.</summary>
    public static bool Locked { get; private set; }
    public static long Version { get; private set; }

    /// <summary>Reads the theme settings (ThemePrimary, ThemeSecondary, ThemeAccent, ThemeBg, ThemeMode, ThemeRadius, ThemeLock).</summary>
    public static void Apply(Dictionary<string, string> s)
    {
        string C(string k, string d) => IsHex(s.GetValueOrDefault(k)) ? s[k].Trim().ToUpperInvariant() : d;
        var d0 = Presets[0];
        var mode = s.GetValueOrDefault("ThemeMode") ?? "light"; if (!Modes.Contains(mode)) mode = "light";
        var radius = s.GetValueOrDefault("ThemeRadius") ?? "soft"; if (!Radii.Contains(radius)) radius = "soft";
        Css = Build("saved", C("ThemePrimary", d0.Primary), C("ThemeSecondary", d0.Secondary), C("ThemeAccent", d0.Accent), C("ThemeBg", d0.Bg), radius, ":root", "[data-bs-theme=dark]");
        Mode = mode;
        Locked = string.Equals(s.GetValueOrDefault("ThemeLock"), "true", StringComparison.OrdinalIgnoreCase);
        Version = DateTime.UtcNow.Ticks;
    }

    /// <summary>Null when the choice is fine, otherwise what is wrong with it.</summary>
    public static string? Validate(string primary, string secondary, string accent, string bg, string mode, string radius)
    {
        foreach (var (name, v) in new[] { ("Primary", primary), ("Secondary", secondary), ("Accent", accent), ("Background", bg) })
            if (!IsHex(v)) return $"{name} colour must look like #0B3D5C.";
        if (!Modes.Contains(mode)) return "Choose light, dark or automatic.";
        if (!Radii.Contains(radius)) return "Choose a corner style.";
        if (Contrast(primary, "#FFFFFF") < 3.0) return "The primary colour is too light: white text on it would be hard to read (it needs a contrast of at least 3:1, this is " + Contrast(primary, "#FFFFFF").ToString("0.0", CultureInfo.InvariantCulture) + ":1).";
        if (Contrast(secondary, "#FFFFFF") < 3.0) return "The secondary colour is too light: white text on it would be hard to read (it needs a contrast of at least 3:1).";
        return null;
    }

    public static bool IsHex(string? v) => v != null && System.Text.RegularExpressions.Regex.IsMatch(v.Trim(), "^#[0-9a-fA-F]{6}$");

    // ------------------------------------------------------------------ colour maths
    static (double r, double g, double b) P(string hex) { hex = hex.Trim().TrimStart('#'); return (Convert.ToInt32(hex[..2], 16), Convert.ToInt32(hex[2..4], 16), Convert.ToInt32(hex[4..6], 16)); }
    static string Hex((double r, double g, double b) c) => "#" + string.Concat(new[] { c.r, c.g, c.b }.Select(x => Math.Clamp((int)Math.Round(x), 0, 255).ToString("X2")));
    /// <summary>t = how much of b is mixed into a (0..1).</summary>
    static string Mix(string a, string b, double t) { var x = P(a); var y = P(b); return Hex((x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t)); }
    static string Rgb(string hex) { var c = P(hex); return $"{(int)c.r},{(int)c.g},{(int)c.b}"; }
    static double Lum(string hex)
    {
        var c = P(hex); double F(double v) { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
        return 0.2126 * F(c.r) + 0.7152 * F(c.g) + 0.0722 * F(c.b);
    }
    public static double Contrast(string a, string b) { var l1 = Lum(a); var l2 = Lum(b); return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05); }
    /// <summary>Darkens a colour until white text on it reaches the contrast (for buttons).</summary>
    static string ForWhiteText(string c, double min)
    {
        for (int i = 0; i < 12 && Contrast(c, "#FFFFFF") < min; i++) c = Mix(c, "#000000", 0.08);
        return c;
    }

    /// <summary>
    /// Builds the variables block. <paramref name="light"/> is the selector for light mode (":root", or "#themePreview" in the preview),
    /// <paramref name="dark"/> the selector for dark mode.
    /// </summary>
    public static string Build(string _, string primary, string secondary, string accent, string bg, string radius, string light, string dark, bool scoped = false)
    {
        var (r, rsm) = radius switch { "sharp" => ("6px", "4px"), "round" => ("22px", "16px"), _ => ("14px", "10px") };
        var accentInk = Lum(accent) > 0.35 ? "#1D2733" : "#FFFFFF";
        var sb = new StringBuilder();
        sb.Append(light).Append('{')
          .Append($"--brand:{primary};--brand-2:{secondary};--accent:{accent};--bg:{bg};--brand-rgb:{Rgb(primary)};--brand-2-rgb:{Rgb(secondary)};--accent-rgb:{Rgb(accent)};")
          .Append($"--brand-ink:{Mix(primary, "#000000", .22)};--brand-soft:{Mix(primary, "#FFFFFF", .90)};--brand-softer:{Mix(primary, "#FFFFFF", .95)};--brand-line:{Mix(primary, "#FFFFFF", .80)};--brand-bar:{Mix(secondary, "#FFFFFF", .70)};")
          .Append($"--nav-from:{primary};--nav-to:{secondary};--brand-text:{primary};--accent-ink:{accentInk};--radius:{r};--radius-sm:{rsm};")
          .Append($"--bs-primary:{primary};--bs-primary-rgb:{Rgb(primary)};--bs-link-color:{secondary};--bs-link-color-rgb:{Rgb(secondary)};--bs-link-hover-color:{primary};--bs-focus-ring-color:rgba({Rgb(secondary)},.25);")
          .Append(scoped ? "--surface:#FFFFFF;--surface-rgb:255,255,255;--card:#FFFFFF;--surface-2:#F8FAFC;--surface-3:#EEF1F5;--line:#E5E9EF;--line-2:#D5DBE4;--ink:#1D2733;--ink-2:#33475B;--muted:#6B7785;--muted-2:#B8C2CF;--ok:#1A8F5B;--warn:#C98A00;--bad:#C7384A;--ok-soft:#E3F4EC;--warn-soft:#FDF3DC;--bad-soft:#FBE7EA;--shadow:0 1px 2px rgba(16,24,40,.05),0 4px 14px rgba(16,24,40,.06);--bs-body-bg:#FFFFFF;--bs-body-color:#1D2733;--bs-border-color:#E5E9EF;" : "")
          .Append('}');
        // dark mode: a deep tint of the primary colour as the page, lighter surfaces on top, brighter brand colours for text and buttons
        var bgd = Mix("#0A0E13", primary, .12);
        var surface = Mix(bgd, "#FFFFFF", .06);
        var btn = ForWhiteText(Mix(secondary, "#FFFFFF", .16), 4.5);
        sb.Append(dark).Append('{')
          .Append($"--bg:{bgd};--surface:{surface};--surface-rgb:{Rgb(surface)};--card:{surface};--surface-2:{Mix(bgd, "#FFFFFF", .09)};--surface-3:{Mix(bgd, "#FFFFFF", .14)};")
          .Append($"--line:{Mix(bgd, "#FFFFFF", .16)};--line-2:{Mix(bgd, "#FFFFFF", .24)};--ink:#E7EDF4;--ink-2:#C3CFDB;--muted:#9AA9BA;--muted-2:#5D6C7E;")
          .Append($"--brand:{btn};--brand-2:{Mix(secondary, "#FFFFFF", .50)};--brand-text:{Mix(secondary, "#FFFFFF", .62)};--brand-rgb:{Rgb(btn)};--brand-2-rgb:{Rgb(Mix(secondary, "#FFFFFF", .50))};")
          .Append($"--brand-ink:{Mix(btn, "#000000", .2)};--brand-soft:{Mix(bgd, secondary, .30)};--brand-softer:{Mix(bgd, secondary, .16)};--brand-line:{Mix(bgd, secondary, .42)};--brand-bar:{Mix(bgd, secondary, .60)};")
          .Append($"--nav-from:{Mix(primary, "#000000", .45)};--nav-to:{Mix(primary, "#000000", .12)};")
          .Append("--ok:#3FBF86;--warn:#E3B341;--bad:#F0707F;--ok-soft:#12372A;--warn-soft:#3B2F10;--bad-soft:#3D1A21;")
          .Append("--shadow:0 1px 2px rgba(0,0,0,.45),0 4px 14px rgba(0,0,0,.35);")
          .Append($"--bs-body-bg:{bgd};--bs-body-color:#E7EDF4;--bs-border-color:{Mix(bgd, "#FFFFFF", .16)};--bs-link-color:{Mix(secondary, "#FFFFFF", .5)};--bs-link-color-rgb:{Rgb(Mix(secondary, "#FFFFFF", .5))};--bs-link-hover-color:{Mix(secondary, "#FFFFFF", .7)};")
          .Append('}');
        return sb.ToString();
    }
}
