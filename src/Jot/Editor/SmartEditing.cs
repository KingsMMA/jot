using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace Jot.Editor;

/// <summary>
/// Adds the "common sense" editing behaviours: auto-closing brackets and quotes, typing through a
/// closer that is already there, wrapping a selection in a pair, deleting an empty pair with one
/// backspace, and brace-aware indentation when pressing Enter. All behaviour respects
/// <see cref="EditingOptions"/> so it follows the user's indent settings.
/// </summary>
public sealed class SmartEditing
{
    private static readonly Dictionary<char, char> Pairs = new()
    {
        ['('] = ')', ['['] = ']', ['{'] = '}', ['"'] = '"', ['\''] = '\'', ['`'] = '`',
    };

    private static readonly HashSet<char> Closers = [')', ']', '}'];
    private static readonly HashSet<char> Quotes = ['"', '\'', '`'];

    private readonly TextEditor _editor;
    private readonly EditingOptions _options;

    public SmartEditing(TextEditor editor, EditingOptions options)
    {
        _editor = editor;
        _options = options;
        _editor.TextArea.TextEntering += OnTextEntering;
        // Tunnel so we pre-empt the editor's own Enter/Backspace handling.
        _editor.AddHandler(InputElement.KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private TextDocument Doc => _editor.Document;

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (!_options.AutoCloseBrackets || e.Text is not { Length: 1 }) return;
        var c = e.Text[0];
        var caret = _editor.CaretOffset;

        // Wrap a selection in the typed pair.
        if (_editor.SelectionLength > 0 && Pairs.TryGetValue(c, out var closer))
        {
            var start = _editor.SelectionStart;
            var selected = _editor.SelectedText;
            Doc.Replace(start, _editor.SelectionLength, c + selected + closer);
            _editor.SelectionStart = start + 1;
            _editor.SelectionLength = selected.Length;
            e.Handled = true;
            return;
        }

        if (_editor.SelectionLength > 0) return;

        var next = caret < Doc.TextLength ? Doc.GetCharAt(caret) : '\0';

        // Type through an existing closer or quote rather than inserting a duplicate.
        if ((Closers.Contains(c) || Quotes.Contains(c)) && next == c)
        {
            _editor.CaretOffset = caret + 1;
            e.Handled = true;
            return;
        }

        // Auto-close an opener when it makes sense.
        if (Pairs.TryGetValue(c, out var auto) && ShouldAutoClose(c, caret, next))
        {
            Doc.Insert(caret, c.ToString() + auto);
            _editor.CaretOffset = caret + 1;
            e.Handled = true;
        }
    }

    private bool ShouldAutoClose(char opener, int caret, char next)
    {
        // Do not close right before an identifier character; it is usually unwanted.
        if (IsWordChar(next)) return false;

        if (Quotes.Contains(opener))
        {
            var prev = caret > 0 ? Doc.GetCharAt(caret - 1) : '\0';
            // Avoid turning an apostrophe in "don't" into a pair, and avoid pairing inside words.
            if (IsWordChar(prev)) return false;
        }

        return true;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || _editor.SelectionLength > 0) return;

        var caret = _editor.CaretOffset;
        var prev = caret > 0 ? Doc.GetCharAt(caret - 1) : '\0';
        var next = caret < Doc.TextLength ? Doc.GetCharAt(caret) : '\0';

        switch (e.Key)
        {
            case Key.Back when _options.AutoCloseBrackets && IsEmptyPair(prev, next):
                Doc.Remove(caret - 1, 2);
                e.Handled = true;
                break;

            case Key.Enter:
                HandleEnter(caret, prev, next);
                e.Handled = true;
                break;
        }
    }

    private void HandleEnter(int caret, char prev, char next)
    {
        var line = Doc.GetLineByOffset(caret);
        var indent = LeadingWhitespace(Doc.GetText(line));
        var unit = _options.IndentUnit;

        if (IsEmptyPair(prev, next))
        {
            // Expand the pair onto three lines with the caret on the indented middle line.
            var insert = "\n" + indent + unit + "\n" + indent;
            Doc.Insert(caret, insert);
            _editor.CaretOffset = caret + 1 + indent.Length + unit.Length;
        }
        else if (prev is '{' or '[' or '(')
        {
            var insert = "\n" + indent + unit;
            Doc.Insert(caret, insert);
            _editor.CaretOffset = caret + insert.Length;
        }
        else
        {
            var insert = "\n" + indent;
            Doc.Insert(caret, insert);
            _editor.CaretOffset = caret + insert.Length;
        }
    }

    private static bool IsEmptyPair(char prev, char next) =>
        (prev == '{' && next == '}') || (prev == '[' && next == ']') || (prev == '(' && next == ')');

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static string LeadingWhitespace(string lineText)
    {
        var i = 0;
        while (i < lineText.Length && (lineText[i] == ' ' || lineText[i] == '\t')) i++;
        return lineText[..i];
    }
}
