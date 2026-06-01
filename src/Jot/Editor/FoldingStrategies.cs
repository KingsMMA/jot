using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace Jot.Editor;

/// <summary>
/// Produces fold regions for a document. Brace-delimited languages fold on matched
/// <c>{}</c> and <c>[]</c> pairs (skipping braces inside strings and comments); indentation-based
/// languages such as YAML fold on increasing indentation. Both are single-pass and allocation-light
/// to keep folding cheap on large files.
/// </summary>
public static class FoldingStrategies
{
    private static readonly HashSet<string> IndentLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "yaml", "python", "coffeescript", "sass", "jade", "pug", "fsharp", "haskell", "nim",
    };

    public static IEnumerable<NewFolding> Create(string languageId, TextDocument document) =>
        IndentLanguages.Contains(languageId)
            ? IndentFoldings(document)
            : BraceFoldings(document);

    /// <summary>Folds matched curly and square brackets that span more than one line.</summary>
    public static List<NewFolding> BraceFoldings(TextDocument document)
    {
        var text = document.Text;
        var foldings = new List<NewFolding>();
        var stack = new Stack<(char open, int offset)>();

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '"' or '\'' or '`':
                    i = SkipString(text, i, c);
                    break;
                case '/' when i + 1 < text.Length && text[i + 1] == '/':
                    i = SkipToLineEnd(text, i);
                    break;
                case '#':
                    i = SkipToLineEnd(text, i);
                    break;
                case '/' when i + 1 < text.Length && text[i + 1] == '*':
                    i = SkipBlockComment(text, i);
                    break;
                case '{' or '[':
                    stack.Push((c, i));
                    break;
                case '}' or ']':
                    if (stack.Count > 0)
                    {
                        var (open, offset) = stack.Pop();
                        if (Matches(open, c) && document.GetLineByOffset(offset).LineNumber
                                != document.GetLineByOffset(i).LineNumber)
                        {
                            foldings.Add(new NewFolding(offset, i + 1)
                            {
                                Name = open == '{' ? "{…}" : "[…]",
                            });
                        }
                    }
                    break;
            }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }

    /// <summary>Folds blocks introduced by deeper indentation, as used by YAML and Python.</summary>
    public static List<NewFolding> IndentFoldings(TextDocument document)
    {
        var foldings = new List<NewFolding>();
        var lineCount = document.LineCount;

        // Indentation (in columns) for each line; -1 marks a blank line.
        var indents = new int[lineCount + 1];
        for (var n = 1; n <= lineCount; n++)
        {
            var line = document.GetLineByNumber(n);
            indents[n] = LeadingIndent(document, line);
        }

        for (var n = 1; n <= lineCount; n++)
        {
            if (indents[n] < 0) continue;

            // Find the next non-blank line; if it is more indented, this line opens a block.
            var next = n + 1;
            while (next <= lineCount && indents[next] < 0) next++;
            if (next > lineCount || indents[next] <= indents[n]) continue;

            var end = next;
            for (var m = next; m <= lineCount; m++)
            {
                if (indents[m] < 0) continue;
                if (indents[m] <= indents[n]) break;
                end = m;
            }

            var startLine = document.GetLineByNumber(n);
            var endLine = document.GetLineByNumber(end);
            foldings.Add(new NewFolding(startLine.EndOffset, endLine.EndOffset) { Name = "…" });
        }

        return foldings;
    }

    private static int LeadingIndent(TextDocument document, DocumentLine line)
    {
        var text = document.GetText(line);
        var indent = 0;
        foreach (var c in text)
        {
            if (c == ' ') indent++;
            else if (c == '\t') indent += 4;
            else return indent;
        }
        return -1; // entirely whitespace
    }

    private static bool Matches(char open, char close) =>
        (open == '{' && close == '}') || (open == '[' && close == ']');

    private static int SkipString(string text, int i, char quote)
    {
        for (i++; i < text.Length; i++)
        {
            if (text[i] == '\\') { i++; continue; }
            if (text[i] == quote) return i;
            if (text[i] == '\n') return i - 1; // unterminated single-line string
        }
        return text.Length;
    }

    private static int SkipToLineEnd(string text, int i)
    {
        var nl = text.IndexOf('\n', i);
        return nl < 0 ? text.Length : nl;
    }

    private static int SkipBlockComment(string text, int i)
    {
        var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
        return end < 0 ? text.Length : end + 1;
    }
}
