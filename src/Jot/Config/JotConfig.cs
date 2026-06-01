using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jot.Config;

/// <summary>
/// The system-wide configuration. One file applies to every file Jot edits; there is no
/// per-project configuration. Unknown keys are preserved on save so a newer config is never
/// silently truncated by an older build.
/// </summary>
public sealed class JotConfig
{
    public int IndentSize { get; set; } = 4;
    public bool InsertSpaces { get; set; } = true;

    /// <summary>"same-line" or "next-line"; applies to languages Jot reformats structurally.</summary>
    public string BraceStyle { get; set; } = "same-line";

    public bool TrimTrailingWhitespace { get; set; } = true;
    public bool InsertFinalNewline { get; set; } = true;
    public bool WordWrap { get; set; } = false;

    public string Theme { get; set; } = "dark";
    public string FontFamily { get; set; } = "Cascadia Code";
    public double FontSize { get; set; } = 13;

    /// <summary>The global hotkey that opens the file selected in File Explorer.</summary>
    public string Hotkey { get; set; } = "Ctrl+Space";

    /// <summary>Keep a warm background instance so windows open instantly.</summary>
    public bool BackgroundAgent { get; set; } = true;

    public bool AutoCloseBrackets { get; set; } = true;

    /// <summary>Per-language indentation overrides, keyed by language id (for example "go").</summary>
    public Dictionary<string, LanguageOverride> LanguageOverrides { get; set; } = new();

    /// <summary>
    /// Per-language external formatter commands, keyed by language id. The command receives the
    /// document on standard input and must write the formatted result to standard output, for
    /// example "clang-format" or "prettier --stdin-filepath x.css".
    /// </summary>
    public Dictionary<string, string> ExternalFormatters { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalKeys { get; set; }

    /// <summary>The indent string for a language, honouring any per-language override.</summary>
    public string IndentUnitFor(string? languageId)
    {
        var size = IndentSize;
        var spaces = InsertSpaces;

        if (languageId is not null && LanguageOverrides.TryGetValue(languageId, out var over))
        {
            if (over.IndentSize is { } s) size = s;
            if (over.InsertSpaces is { } b) spaces = b;
        }

        return spaces ? new string(' ', Math.Max(0, size)) : "\t";
    }
}

public sealed class LanguageOverride
{
    public int? IndentSize { get; set; }
    public bool? InsertSpaces { get; set; }
}
