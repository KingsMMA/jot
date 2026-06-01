namespace Jot.Theming;

/// <summary>The colours the Markdown preview needs, derived from a theme.</summary>
public readonly record struct MarkdownPalette(
    string Background,
    string Foreground,
    string CodeBackground,
    string Border,
    string Link,
    string Muted);

/// <summary>
/// A colour theme. Themes are either flat (a solid background) or have a backdrop — a bundled,
/// blurred background image or the system acrylic material — over which the editor and chrome sit on
/// a translucent surface. The translucent surface keeps a controllable, contrast-checked tint behind
/// the text so it stays clearly legible no matter how pretty the backdrop is.
/// </summary>
public sealed class JotTheme
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool IsDark { get; init; } = true;

    /// <summary>The bundled TextMate theme used for syntax token colours.</summary>
    public string TextMateTheme { get; init; } = "DarkPlus";

    // Core palette (all opaque).
    public required string Background { get; init; }
    public required string Foreground { get; init; }
    public required string Panel { get; init; }
    public required string Muted { get; init; }
    public required string Accent { get; init; }
    public required string LineNumber { get; init; }
    public required string Selection { get; init; }
    public required string CurrentLine { get; init; }
    public required string Border { get; init; }

    /// <summary>An <c>avares</c>-relative path to a background image, or null for a flat theme.</summary>
    public string? BackgroundImage { get; init; }

    /// <summary>Use the system acrylic/Mica material as the backdrop (frosts the desktop behind the window).</summary>
    public bool Acrylic { get; init; }

    /// <summary>Opacity of the editor and chrome surfaces when a backdrop is present.</summary>
    public double SurfaceOpacity { get; init; } = 1.0;

    /// <summary>The lightest colour that can sit behind the surface (image peak, or white for acrylic).</summary>
    public string? UnderlayPeak { get; init; }

    public bool HasBackdrop => BackgroundImage is not null || Acrylic;

    /// <summary>The editor surface brush colour, translucent when there is a backdrop.</summary>
    public string SurfaceArgb() => HasBackdrop ? Contrast.WithAlpha(Background, SurfaceOpacity) : Background;

    /// <summary>The status-bar surface brush colour, translucent when there is a backdrop.</summary>
    public string PanelArgb() => HasBackdrop ? Contrast.WithAlpha(Panel, SurfaceOpacity) : Panel;

    /// <summary>
    /// The opaque colour effectively behind the text — the surface composited over the worst-case
    /// underlay. Used both for contrast checks and as the (solid) Markdown preview background.
    /// </summary>
    public string EffectiveTextBackground() =>
        HasBackdrop && UnderlayPeak is not null
            ? Contrast.CompositeHex(SurfaceArgb(), UnderlayPeak)
            : Background;

    public MarkdownPalette Markdown() => new(
        Background: EffectiveTextBackground(),
        Foreground: Foreground,
        CodeBackground: Panel,
        Border: Border,
        Link: Accent,
        Muted: Muted);
}
