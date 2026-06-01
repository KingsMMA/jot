using System.IO;
using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Jot.Diagnostics;

/// <summary>A single problem found in the document: a range to underline and a message.</summary>
public readonly record struct Diagnostic(int Offset, int Length, string Message);

/// <summary>
/// Very basic, in-process error checking for the structured formats people edit most often. It only
/// reports genuine parse errors (never style opinions), runs synchronously in a few milliseconds for
/// typical files, and pulls in no language servers, so it does not compromise Jot's responsiveness.
/// </summary>
public static class DiagnosticsAnalyzer
{
    public static IReadOnlyList<Diagnostic> Analyze(string languageId, string text)
    {
        if (string.IsNullOrEmpty(text)) return [];

        return languageId switch
        {
            "json" or "jsonc" => AnalyzeJson(text),
            "yaml" => AnalyzeYaml(text),
            _ => [],
        };
    }

    private static IReadOnlyList<Diagnostic> AnalyzeJson(string text)
    {
        try
        {
            using var _ = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            return [];
        }
        catch (JsonException ex)
        {
            var line = (int)(ex.LineNumber ?? 0);
            var column = (int)(ex.BytePositionInLine ?? 0);
            var offset = OffsetFromLineColumn(text, line, column);
            var length = LengthToLineEnd(text, offset);
            return [new Diagnostic(offset, length, CleanJsonMessage(ex.Message))];
        }
    }

    private static IReadOnlyList<Diagnostic> AnalyzeYaml(string text)
    {
        try
        {
            new YamlStream().Load(new StringReader(text));
            return [];
        }
        catch (YamlException ex)
        {
            var start = (int)ex.Start.Index;
            var end = (int)ex.End.Index;
            var offset = Math.Clamp(start, 0, text.Length);
            var length = end > start
                ? Math.Min(end - start, text.Length - offset)
                : LengthToLineEnd(text, offset);
            return [new Diagnostic(offset, Math.Max(1, length), CleanYamlMessage(ex.Message))];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Converts a zero-based line and column to a character offset, clamped to the text.</summary>
    public static int OffsetFromLineColumn(string text, int line, int column)
    {
        var offset = 0;
        var currentLine = 0;
        while (currentLine < line && offset < text.Length)
        {
            var newline = text.IndexOf('\n', offset);
            if (newline < 0) { offset = text.Length; break; }
            offset = newline + 1;
            currentLine++;
        }
        return Math.Clamp(offset + column, 0, text.Length);
    }

    private static int LengthToLineEnd(string text, int offset)
    {
        if (offset >= text.Length) return Math.Min(1, text.Length);
        var newline = text.IndexOf('\n', offset);
        var end = newline < 0 ? text.Length : newline;
        return Math.Max(1, end - offset);
    }

    private static string CleanJsonMessage(string message)
    {
        // Drop the trailing "LineNumber: x | BytePositionInLine: y." that the runtime appends.
        var bar = message.IndexOf(" LineNumber:", StringComparison.Ordinal);
        return bar < 0 ? message : message[..bar].TrimEnd();
    }

    private static string CleanYamlMessage(string message)
    {
        // YamlDotNet prefixes "(Line: x, Col: y, Idx: z) - ..."; keep just the explanation.
        var dash = message.IndexOf(") - ", StringComparison.Ordinal);
        return dash < 0 ? message : message[(dash + 4)..];
    }
}
