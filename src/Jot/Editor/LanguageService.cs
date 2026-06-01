using System.IO;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace Jot.Editor;

/// <summary>
/// Resolves and applies syntax highlighting. The language is chosen by file extension first,
/// then by content (shebang, leading markers), and can be overridden manually. Highlighting is
/// backed by TextMate grammars, so coverage matches the grammar set shipped with the editor.
/// </summary>
public sealed class LanguageService
{
    private readonly RegistryOptions _registry;
    private TextMate.Installation? _installation;

    public LanguageService(ThemeName theme = ThemeName.DarkPlus)
    {
        _registry = new RegistryOptions(theme);
    }

    public string CurrentLanguageId { get; private set; } = PlainText;
    public string CurrentDisplay { get; private set; } = "Plain Text";

    public const string PlainText = "plaintext";

    public void Install(TextEditor editor)
    {
        _installation = editor.InstallTextMate(_registry);
    }

    /// <summary>All grammar-backed languages, ordered by display name, for the manual picker.</summary>
    public IReadOnlyList<LanguageChoice> AvailableLanguages()
    {
        var choices = new List<LanguageChoice> { new(PlainText, "Plain Text") };
        choices.AddRange(_registry.GetAvailableLanguages()
            .Select(l => new LanguageChoice(l.Id, DisplayName(l)))
            .DistinctBy(c => c.Id));
        return choices.OrderBy(c => c.Display, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public string DetectAndApply(string? path, string text)
    {
        var languageId = Detect(path, text);
        ApplyById(languageId);
        return CurrentDisplay;
    }

    public void ApplyById(string languageId)
    {
        if (string.IsNullOrEmpty(languageId) || languageId == PlainText)
        {
            SetGrammarSafe(null);
            CurrentLanguageId = PlainText;
            CurrentDisplay = "Plain Text";
            return;
        }

        var scope = _registry.GetScopeByLanguageId(languageId);
        SetGrammarSafe(scope);
        CurrentLanguageId = languageId;
        var lang = _registry.GetAvailableLanguages().FirstOrDefault(l => l.Id == languageId);
        CurrentDisplay = lang is null ? languageId : DisplayName(lang);
    }

    private void SetGrammarSafe(string? scope)
    {
        if (_installation is null) return;
        try { _installation.SetGrammar(scope); }
        catch { /* unknown or missing grammar: fall back to no highlighting */ }
    }

    /// <summary>Resolves a language id from the path then the content, or plain text.</summary>
    public string Detect(string? path, string text)
    {
        if (!string.IsNullOrEmpty(path))
        {
            var byName = DetectBySpecialName(Path.GetFileName(path));
            if (byName is not null) return byName;

            var ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext))
            {
                var lang = _registry.GetLanguageByExtension(ext);
                if (lang is not null) return lang.Id;
            }
        }

        return DetectByContent(text) ?? PlainText;
    }

    /// <summary>Common files whose type is conveyed by name rather than extension.</summary>
    private static string? DetectBySpecialName(string fileName) => fileName.ToLowerInvariant() switch
    {
        "dockerfile" => "dockerfile",
        "makefile" or "gnumakefile" => "makefile",
        "cmakelists.txt" => "cmake",
        ".gitignore" or ".gitattributes" or ".dockerignore" => "ignore",
        ".bashrc" or ".bash_profile" or ".zshrc" or ".profile" => "shellscript",
        _ => null,
    };

    private static string? DetectByContent(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.Length == 0) return null;

        if (trimmed.StartsWith("#!"))
        {
            var firstLine = trimmed[..trimmed.IndexOfAny(['\n', '\r']).ClampToLength(trimmed.Length)];
            if (firstLine.Contains("python")) return "python";
            if (firstLine.Contains("node")) return "javascript";
            if (firstLine.Contains("ruby")) return "ruby";
            if (firstLine.Contains("perl")) return "perl";
            if (firstLine.Contains("pwsh") || firstLine.Contains("powershell")) return "powershell";
            if (firstLine.Contains("bash") || firstLine.Contains("/sh") || firstLine.Contains("zsh"))
                return "shellscript";
            return "shellscript";
        }

        if (trimmed.StartsWith("<?xml")) return "xml";
        if (trimmed.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase)) return "html";
        if (trimmed.StartsWith("---\n") || trimmed.StartsWith("---\r\n")) return "yaml";
        if ((trimmed[0] == '{' || trimmed[0] == '[') && LooksLikeJson(trimmed)) return "json";

        return null;
    }

    private static bool LooksLikeJson(string trimmed)
    {
        // Cheap structural sanity check; avoids a full parse on large files.
        var last = trimmed.TrimEnd();
        return (trimmed[0] == '{' && last.EndsWith('}')) || (trimmed[0] == '[' && last.EndsWith(']'));
    }

    private static string DisplayName(Language lang)
    {
        var alias = lang.Aliases?.FirstOrDefault();
        var name = !string.IsNullOrWhiteSpace(alias) ? alias : lang.Id;
        return name.Length > 0 ? char.ToUpperInvariant(name[0]) + name[1..] : name;
    }
}

public readonly record struct LanguageChoice(string Id, string Display)
{
    public override string ToString() => Display;
}

internal static class IntExtensions
{
    public static int ClampToLength(this int index, int length) => index < 0 ? length : index;
}
