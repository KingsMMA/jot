using System.Globalization;

namespace Jot.Theming;

/// <summary>
/// WCAG colour-contrast helpers used to guarantee that text stays clearly legible on every theme,
/// including the ones with translucent surfaces over a background image.
/// </summary>
public static class Contrast
{
    /// <summary>WCAG AA threshold for normal-size text.</summary>
    public const double AaNormal = 4.5;

    public static (double R, double G, double B, double A) Parse(string hex)
    {
        hex = hex.TrimStart('#');
        // Accept RGB, RRGGBB, and AARRGGBB / RRGGBBAA-free forms; alpha defaults to opaque.
        if (hex.Length == 3)
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);

        double a = 1.0;
        if (hex.Length == 8)
        {
            // #AARRGGBB
            a = Hex(hex.Substring(0, 2)) / 255.0;
            hex = hex.Substring(2);
        }

        var r = Hex(hex.Substring(0, 2)) / 255.0;
        var g = Hex(hex.Substring(2, 2)) / 255.0;
        var b = Hex(hex.Substring(4, 2)) / 255.0;
        return (r, g, b, a);
    }

    private static int Hex(string s) => int.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    /// <summary>Composites <paramref name="foreground"/> (which may be translucent) over <paramref name="background"/>.</summary>
    public static (double R, double G, double B) Composite(string foreground, string background)
    {
        var f = Parse(foreground);
        var b = Parse(background);
        var r = f.R * f.A + b.R * (1 - f.A);
        var g = f.G * f.A + b.G * (1 - f.A);
        var bl = f.B * f.A + b.B * (1 - f.A);
        return (r, g, bl);
    }

    public static double RelativeLuminance(double r, double g, double b)
    {
        static double Channel(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
    }

    public static double Ratio(string foreground, string background)
    {
        // Flatten any alpha in the foreground over the background first.
        var (r, g, b) = Composite(foreground, background);
        var (br, bg, bb, _) = Parse(background);
        var l1 = RelativeLuminance(r, g, b);
        var l2 = RelativeLuminance(br, bg, bb);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    public static bool MeetsAa(string foreground, string background) =>
        Ratio(foreground, background) >= AaNormal;

    /// <summary>Returns <paramref name="hex"/> as #AARRGGBB with the given opacity (0..1).</summary>
    public static string WithAlpha(string hex, double opacity)
    {
        var (r, g, b, _) = Parse(hex);
        var a = (int)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
        return $"#{a:X2}{To255(r):X2}{To255(g):X2}{To255(b):X2}";
    }

    /// <summary>Flattens a (possibly translucent) colour over a background to an opaque #RRGGBB.</summary>
    public static string CompositeHex(string foreground, string background)
    {
        var (r, g, b) = Composite(foreground, background);
        return $"#{To255(r):X2}{To255(g):X2}{To255(b):X2}";
    }

    private static int To255(double c) => (int)Math.Round(Math.Clamp(c, 0, 1) * 255);
}
