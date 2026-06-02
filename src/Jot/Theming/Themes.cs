namespace Jot.Theming;

/// <summary>The built-in themes. The first (dark) is the default and matches earlier releases.</summary>
public static class Themes
{
    public const string DefaultId = "dark";

    public static readonly IReadOnlyList<JotTheme> All =
    [
        new JotTheme
        {
            Id = "dark",
            Name = "Dark",
            IsDark = true,
            TextMateTheme = "DarkPlus",
            Background = "#0d1117",
            Foreground = "#e6edf3",
            Panel = "#161b22",
            Muted = "#8b949e",
            Accent = "#4493f8",
            LineNumber = "#6e7681",
            Selection = "#264f78",
            CurrentLine = "#161b22",
            Border = "#30363d",
        },
        new JotTheme
        {
            Id = "light",
            Name = "Light",
            IsDark = false,
            TextMateTheme = "LightPlus",
            Background = "#ffffff",
            Foreground = "#1f2328",
            Panel = "#f6f8fa",
            Muted = "#59636e",
            Accent = "#0969da",
            LineNumber = "#818b98",
            Selection = "#b6e3ff",
            CurrentLine = "#f3f4f6",
            Border = "#d1d9e0",
        },
        new JotTheme
        {
            Id = "rose",
            Name = "Rose",
            IsDark = true,
            TextMateTheme = "DarkPlus",
            Background = "#2a1620",
            Foreground = "#ffe0ec",
            Panel = "#3a2030",
            Muted = "#d9a8bb",
            Accent = "#ff7eb6",
            LineNumber = "#a76d83",
            Selection = "#5a2a40",
            CurrentLine = "#34202c",
            Border = "#4a2c3a",
        },
        new JotTheme
        {
            Id = "lilac",
            Name = "Lilac",
            IsDark = true,
            TextMateTheme = "Dracula",
            Background = "#1b1830",
            Foreground = "#e9e2ff",
            Panel = "#272145",
            Muted = "#b3a8d8",
            Accent = "#c8a2ff",
            LineNumber = "#7d72a6",
            Selection = "#3a3070",
            CurrentLine = "#232043",
            Border = "#393158",
        },
        new JotTheme
        {
            Id = "aurora",
            Name = "Aurora",
            IsDark = true,
            TextMateTheme = "OneDark",
            Background = "#0c1320",
            Foreground = "#eaf2ff",
            Panel = "#0f1830",
            Muted = "#aebfd9",
            Accent = "#5ad1e6",
            LineNumber = "#8a9cc0",
            Selection = "#1f3a5c",
            CurrentLine = "#13203a",
            Border = "#22324f",
            BackgroundImage = "themes/aurora.png",
            SurfaceOpacity = 0.5,
            UnderlayPeak = "#2a5a7a",
        },
        new JotTheme
        {
            Id = "ember",
            Name = "Ember",
            IsDark = true,
            TextMateTheme = "Monokai",
            Background = "#190f14",
            Foreground = "#ffeede",
            Panel = "#221318",
            Muted = "#e0bdb0",
            Accent = "#ff9e64",
            LineNumber = "#bd9385",
            Selection = "#4a2a24",
            CurrentLine = "#1f1418",
            Border = "#3a2620",
            BackgroundImage = "themes/ember.png",
            SurfaceOpacity = 0.5,
            UnderlayPeak = "#7a3a2a",
        },
        new JotTheme
        {
            Id = "synthwave",
            Name = "Synthwave",
            IsDark = true,
            TextMateTheme = "Dracula",
            Background = "#160a18",
            Foreground = "#ffe6f4",
            Panel = "#1c1230",
            Muted = "#e0b6d2",
            Accent = "#ff7ad0",
            LineNumber = "#c188ad",
            Selection = "#4a2050",
            CurrentLine = "#1e1230",
            Border = "#3a2048",
            BackgroundImage = "themes/synthwave.png",
            SurfaceOpacity = 0.55,
            UnderlayPeak = "#803d50",
        },
        new JotTheme
        {
            Id = "winter",
            Name = "Winter",
            IsDark = true,
            TextMateTheme = "OneDark",
            Background = "#120e22",
            Foreground = "#efe9ff",
            Panel = "#201a3a",
            Muted = "#c8bdee",
            Accent = "#b69cff",
            LineNumber = "#9b8fc4",
            Selection = "#342a5a",
            CurrentLine = "#1c1838",
            Border = "#342c52",
            BackgroundImage = "themes/winter.png",
            SurfaceOpacity = 0.55,
            UnderlayPeak = "#71627f",
        },
        new JotTheme
        {
            Id = "acrylic",
            Name = "Acrylic",
            IsDark = true,
            TextMateTheme = "DarkPlus",
            Background = "#0d1117",
            Foreground = "#eef2f6",
            Panel = "#161b22",
            Muted = "#c2cad2",
            Accent = "#6cb0ff",
            LineNumber = "#b0bac4",
            Selection = "#264f78",
            CurrentLine = "#161b22",
            Border = "#30363d",
            Acrylic = true,
            SurfaceOpacity = 0.7,
            UnderlayPeak = "#ffffff",
        },
    ];

    private static readonly Dictionary<string, JotTheme> ById =
        All.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);

    public static JotTheme Get(string? id) =>
        id is not null && ById.TryGetValue(id, out var theme) ? theme : ById[DefaultId];
}
