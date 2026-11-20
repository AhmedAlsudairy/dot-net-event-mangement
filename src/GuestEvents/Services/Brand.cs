using SkiaSharp;

namespace GuestEvents.Services;

/// <summary>
/// The app logo the Admin uploads in Settings. Stored as App_Data/branding/logo.png (resized to fit 256 px),
/// served to everyone at /brand/logo. Without one, the built-in calendar icon is shown.
/// </summary>
public static class Brand
{
    private static string _path = "";
    public static bool Has { get; private set; }
    /// <summary>Changes on every upload so browsers fetch the new image.</summary>
    public static long Version { get; private set; }
    public static string FilePath => _path;

    public static void Init(string contentRoot)
    {
        var dir = Path.Combine(contentRoot, "App_Data", "branding");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "logo.png");
        Refresh();
    }

    private static void Refresh() { Has = File.Exists(_path); Version = Has ? File.GetLastWriteTimeUtc(_path).Ticks : 0; }

    /// <summary>Returns null when saved, otherwise the reason the file was refused.</summary>
    public static string? Save(Stream upload, long length)
    {
        if (length <= 0) return "Choose an image file first.";
        if (length > 3_000_000) return "The image is too large (3 MB at most).";
        using var data = new MemoryStream();
        upload.CopyTo(data);
        data.Position = 0;
        using var src = SKBitmap.Decode(data);   // decoding is the real check that it is an image (PNG, JPG, WEBP, GIF...)
        if (src == null || src.Width < 16 || src.Height < 16) return "That file is not a usable image (use PNG, JPG or WEBP, at least 16x16).";
        var scale = Math.Min(1.0, 256.0 / Math.Max(src.Width, src.Height));
        var w = Math.Max(1, (int)Math.Round(src.Width * scale)); var h = Math.Max(1, (int)Math.Round(src.Height * scale));
        using var resized = src.Resize(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul), new SKSamplingOptions(SKCubicResampler.Mitchell)) ?? src.Copy();
        using var img = SKImage.FromBitmap(resized);
        using var png = img.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(_path, png.ToArray());
        Refresh();
        return null;
    }

    public static void Remove() { if (File.Exists(_path)) File.Delete(_path); Refresh(); }
}
