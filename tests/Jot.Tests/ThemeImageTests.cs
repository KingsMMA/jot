using System.IO;
using Jot.Theming;
using SkiaSharp;
using Xunit;

namespace Jot.Tests;

/// <summary>
/// Verifies that the bundled backdrop images never get brighter than the theme's declared
/// <see cref="JotTheme.UnderlayPeak"/>. That declared peak is what the contrast tests assume sits
/// behind the translucent editor surface, so this keeps the AA guarantee honest even if an image is
/// ever regenerated.
/// </summary>
public class ThemeImageTests
{
    public static IEnumerable<object[]> BackdropThemes() =>
        Themes.All.Where(t => t.BackgroundImage is not null).Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(BackdropThemes))]
    public void BackdropImage_NoPixelBrighterThanDeclaredPeak(JotTheme theme)
    {
        var path = FindAsset(theme.BackgroundImage!);
        using var bitmap = SKBitmap.Decode(path);
        Assert.NotNull(bitmap);

        var peak = Contrast.Parse(theme.UnderlayPeak!);
        var peakLuminance = Contrast.RelativeLuminance(peak.R, peak.G, peak.B);

        var maxLuminance = 0.0;
        for (var y = 0; y < bitmap.Height; y += 5)
        for (var x = 0; x < bitmap.Width; x += 5)
        {
            var c = bitmap.GetPixel(x, y);
            maxLuminance = Math.Max(maxLuminance,
                Contrast.RelativeLuminance(c.Red / 255.0, c.Green / 255.0, c.Blue / 255.0));
        }

        // Small tolerance for bicubic overshoot during the image's upscaling.
        Assert.True(maxLuminance <= peakLuminance + 0.01,
            $"Theme '{theme.Id}': brightest backdrop pixel luminance {maxLuminance:F3} exceeds the declared underlay peak {peakLuminance:F3}.");
    }

    private static string FindAsset(string relative)
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "src", "Jot", "Assets", relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }
        throw new FileNotFoundException($"Could not locate bundled asset '{relative}'.");
    }
}
