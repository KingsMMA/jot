using System.IO;
using System.Text;

namespace Jot.Editor;

/// <summary>
/// Detected line-ending style of a loaded file.
/// </summary>
public enum LineEnding
{
    Crlf,
    Lf,
    Cr,
}

/// <summary>
/// Options that govern how a document is written back to disk. These mirror the
/// system-wide configuration so a caller can pass the user's preferences straight through.
/// </summary>
public readonly record struct SaveOptions(bool TrimTrailingWhitespace, bool InsertFinalNewline)
{
    public static readonly SaveOptions Default = new(true, true);
}

/// <summary>
/// Loads a text file while remembering its encoding, byte-order mark, and dominant line
/// ending so that saving round-trips them exactly. This avoids the classic data-loss bugs
/// where an editor silently rewrites CRLF as LF or strips a BOM.
/// </summary>
public sealed class FileDocument
{
    private FileDocument(string? path, string text, Encoding encoding, bool hasBom, LineEnding lineEnding, bool hadFinalNewline)
    {
        Path = path;
        Text = text;
        Encoding = encoding;
        HasBom = hasBom;
        LineEnding = lineEnding;
        HadFinalNewline = hadFinalNewline;
    }

    public string? Path { get; private set; }
    public string Text { get; }
    public Encoding Encoding { get; }
    public bool HasBom { get; }
    public LineEnding LineEnding { get; }
    public bool HadFinalNewline { get; }

    /// <summary>An empty, unsaved document using sensible Windows defaults.</summary>
    public static FileDocument Empty() =>
        new(null, string.Empty, new UTF8Encoding(false), false, LineEnding.Crlf, false);

    public static FileDocument Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var (encoding, hasBom, bomLength) = DetectEncoding(bytes);
        var text = encoding.GetString(bytes, bomLength, bytes.Length - bomLength);

        var lineEnding = DetectLineEnding(text);
        var hadFinalNewline = text.Length > 0 && (text[^1] == '\n' || text[^1] == '\r');

        // Normalise to '\n' in memory; the original ending is restored on save.
        var normalised = NormaliseToLf(text);
        return new FileDocument(path, normalised, encoding, hasBom, lineEnding, hadFinalNewline);
    }

    /// <summary>
    /// Writes <paramref name="text"/> to <paramref name="path"/>, restoring the original encoding,
    /// BOM, and line ending, and applying the supplied save options.
    /// </summary>
    public static void Save(string path, string text, Encoding encoding, bool hasBom, LineEnding lineEnding, SaveOptions options)
    {
        var normalised = NormaliseToLf(text);

        if (options.TrimTrailingWhitespace)
            normalised = TrimTrailingWhitespacePerLine(normalised);

        if (options.InsertFinalNewline && normalised.Length > 0 && normalised[^1] != '\n')
            normalised += "\n";

        var withEndings = ApplyLineEnding(normalised, lineEnding);

        var withBom = hasBom switch
        {
            true when encoding is UTF8Encoding => new UTF8Encoding(true),
            true => encoding,
            false when encoding is UTF8Encoding => new UTF8Encoding(false),
            false => encoding,
        };

        var preamble = withBom.GetPreamble();
        var body = withBom.GetBytes(withEndings);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        if (hasBom && preamble.Length > 0)
            stream.Write(preamble, 0, preamble.Length);
        stream.Write(body, 0, body.Length);
    }

    public static (Encoding encoding, bool hasBom, int bomLength) DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
            return (new UTF32Encoding(false, true), true, 4);
        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            return (new UTF32Encoding(true, true), true, 4);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return (new UTF8Encoding(true), true, 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return (new UnicodeEncoding(false, true), true, 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return (new UnicodeEncoding(true, true), true, 2);

        // No BOM: default to UTF-8 without BOM, the dominant convention.
        return (new UTF8Encoding(false), false, 0);
    }

    public static LineEnding DetectLineEnding(string text)
    {
        int crlf = 0, lf = 0, cr = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') { crlf++; i++; }
                else cr++;
            }
            else if (text[i] == '\n')
            {
                lf++;
            }
        }

        // No line breaks at all defaults to CRLF, the Windows convention.
        if (crlf >= lf && crlf >= cr) return LineEnding.Crlf;
        return lf >= cr ? LineEnding.Lf : LineEnding.Cr;
    }

    public static string NormaliseToLf(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n');

    public static string ApplyLineEnding(string lfText, LineEnding ending) => ending switch
    {
        LineEnding.Lf => lfText,
        LineEnding.Cr => lfText.Replace('\n', '\r'),
        _ => lfText.Replace("\n", "\r\n"),
    };

    public static string TrimTrailingWhitespacePerLine(string lfText)
    {
        var lines = lfText.Split('\n');
        for (var i = 0; i < lines.Length; i++)
            lines[i] = lines[i].TrimEnd(' ', '\t');
        return string.Join('\n', lines);
    }
}
