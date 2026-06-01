using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Jot.Config;

namespace Jot.Formatting;

public readonly record struct FormatResult(string Text, string Message, bool Changed);

/// <summary>
/// Formats a document according to the user's configuration. JSON and XML-family files are
/// reformatted structurally; an external formatter runs when one is configured for the language;
/// everything else gets safe whitespace normalisation. The original text is never replaced with a
/// broken result: on a parse error the input is returned unchanged with an explanatory message.
/// </summary>
public static class DocumentFormatter
{
    private static readonly HashSet<string> XmlLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "xml", "html", "xhtml", "svg", "xsl", "xaml",
    };

    public static FormatResult Format(string languageId, string text, JotConfig config)
    {
        var indent = config.IndentUnitFor(languageId);

        if (config.ExternalFormatters.TryGetValue(languageId, out var command)
            && ExternalFormatter.TryRun(command, text, out var external))
        {
            return Result(text, external, $"Formatted with external formatter");
        }

        if (languageId == "json" || languageId == "jsonc")
        {
            try { return Result(text, JsonFormatter.Format(text, indent), "Formatted JSON"); }
            catch (Exception ex) { return Unchanged(text, $"Invalid JSON: {ex.Message}"); }
        }

        if (XmlLanguages.Contains(languageId))
        {
            try { return Result(text, FormatXml(text, indent), "Formatted XML"); }
            catch (Exception ex) { return Unchanged(text, $"Could not format as XML: {ex.Message}"); }
        }

        var normalised = WhitespaceNormaliser.Normalise(text, config, indent);
        return Result(text, normalised, "Tidied whitespace");
    }

    private static FormatResult Result(string original, string formatted, string message) =>
        new(formatted, message, formatted != original);

    private static FormatResult Unchanged(string original, string message) =>
        new(original, message, false);

    private static string FormatXml(string xml, string indent)
    {
        var declaration = xml.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);
        var document = XDocument.Parse(xml, LoadOptions.None);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = indent,
            OmitXmlDeclaration = !declaration,
            NewLineChars = "\n",
            Encoding = new UTF8Encoding(false),
        };

        using var writer = new StringWriter();
        using (var xmlWriter = XmlWriter.Create(writer, settings))
            document.Save(xmlWriter);
        return writer.ToString();
    }
}

/// <summary>Trims trailing whitespace, normalises leading indentation, and applies the final-newline rule.</summary>
public static class WhitespaceNormaliser
{
    public static string Normalise(string text, JotConfig config, string indentUnit)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (config.InsertSpaces)
                line = ConvertLeadingTabs(line, indentUnit);
            if (config.TrimTrailingWhitespace)
                line = line.TrimEnd(' ', '\t');
            lines[i] = line;
        }

        var result = string.Join('\n', lines);
        if (config.InsertFinalNewline && result.Length > 0 && result[^1] != '\n')
            result += "\n";
        return result;
    }

    private static string ConvertLeadingTabs(string line, string indentUnit)
    {
        var i = 0;
        while (i < line.Length && line[i] == '\t') i++;
        return i == 0 ? line : string.Concat(Enumerable.Repeat(indentUnit, i)) + line[i..];
    }
}

/// <summary>Runs a configured external formatter, feeding the document on stdin and reading stdout.</summary>
public static class ExternalFormatter
{
    public static bool TryRun(string command, string input, out string output)
    {
        output = string.Empty;
        var (exe, args) = SplitCommand(command);
        if (string.IsNullOrWhiteSpace(exe)) return false;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            if (!process.Start()) return false;
            process.StandardInput.Write(input);
            process.StandardInput.Close();

            var result = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                try { process.Kill(); } catch { /* ignore */ }
                return false;
            }

            if (process.ExitCode != 0 || string.IsNullOrEmpty(result)) return false;
            output = result.Replace("\r\n", "\n").Replace('\r', '\n');
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static (string exe, string args) SplitCommand(string command)
    {
        command = command.Trim();
        if (command.Length == 0) return (string.Empty, string.Empty);

        if (command[0] == '"')
        {
            var end = command.IndexOf('"', 1);
            if (end > 0) return (command[1..end], command[(end + 1)..].Trim());
        }

        var space = command.IndexOf(' ');
        return space < 0 ? (command, string.Empty) : (command[..space], command[(space + 1)..].Trim());
    }
}
