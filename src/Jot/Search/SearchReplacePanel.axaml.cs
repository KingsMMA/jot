using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;

namespace Jot.Search;

/// <summary>
/// A compact find-and-replace overlay for the editor. Supports case-sensitive, whole-word, and
/// regular-expression matching, find next/previous with wrap-around, replace, and replace all.
/// </summary>
public partial class SearchReplacePanel : UserControl
{
    private TextEditor? _editor;
    private readonly TextBox _findBox;
    private readonly TextBox _replaceBox;
    private readonly ToggleButton _caseToggle;
    private readonly ToggleButton _wordToggle;
    private readonly ToggleButton _regexToggle;
    private readonly TextBlock _countText;
    private readonly StackPanel _replaceStack;

    public SearchReplacePanel()
    {
        AvaloniaXamlLoader.Load(this);
        _findBox = this.FindControl<TextBox>("FindBox")!;
        _replaceBox = this.FindControl<TextBox>("ReplaceBox")!;
        _caseToggle = this.FindControl<ToggleButton>("CaseToggle")!;
        _wordToggle = this.FindControl<ToggleButton>("WordToggle")!;
        _regexToggle = this.FindControl<ToggleButton>("RegexToggle")!;
        _countText = this.FindControl<TextBlock>("CountText")!;
        _replaceStack = this.FindControl<StackPanel>("ReplaceActions")!;

        this.FindControl<Button>("NextButton")!.Click += (_, _) => FindNext();
        this.FindControl<Button>("PrevButton")!.Click += (_, _) => FindPrevious();
        this.FindControl<Button>("CloseButton")!.Click += (_, _) => Hide();
        this.FindControl<Button>("ReplaceButton")!.Click += (_, _) => ReplaceCurrent();
        this.FindControl<Button>("ReplaceAllButton")!.Click += (_, _) => ReplaceAll();

        _findBox.KeyDown += OnFindKeyDown;
        _findBox.TextChanged += (_, _) => UpdateCount();
        foreach (var toggle in new[] { _caseToggle, _wordToggle, _regexToggle })
            toggle.IsCheckedChanged += (_, _) => UpdateCount();

        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Hide(); e.Handled = true; } };
    }

    public void Attach(TextEditor editor) => _editor = editor;

    public void ShowFind() => Show(showReplace: false);

    public void ShowReplace() => Show(showReplace: true);

    private void Show(bool showReplace)
    {
        _replaceBox.IsVisible = showReplace;
        _replaceStack.IsVisible = showReplace;
        IsVisible = true;

        if (_editor is { SelectionLength: > 0 } ed && !ed.SelectedText.Contains('\n'))
            _findBox.Text = ed.SelectedText;

        _findBox.Focus();
        _findBox.SelectAll();
        UpdateCount();
    }

    public void Hide()
    {
        IsVisible = false;
        _editor?.Focus();
    }

    private SearchQuery Query => new(
        _findBox.Text ?? string.Empty,
        _caseToggle.IsChecked == true,
        _wordToggle.IsChecked == true,
        _regexToggle.IsChecked == true);

    private void OnFindKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) FindPrevious();
        else FindNext();
        e.Handled = true;
    }

    private void FindNext()
    {
        if (_editor is null) return;
        var start = _editor.SelectionLength > 0
            ? _editor.SelectionStart + _editor.SelectionLength
            : _editor.CaretOffset;
        var match = SearchEngine.FindNext(_editor.Text, Query, start);
        if (match is { } m) SelectMatch(m);
    }

    private void FindPrevious()
    {
        if (_editor is null) return;
        var before = _editor.SelectionLength > 0 ? _editor.SelectionStart : _editor.CaretOffset;
        var match = SearchEngine.FindPrevious(_editor.Text, Query, before);
        if (match is { } m) SelectMatch(m);
    }

    private void SelectMatch(SearchMatch match)
    {
        if (_editor is null) return;
        _editor.CaretOffset = match.Offset + match.Length;
        _editor.Select(match.Offset, match.Length);
        _editor.TextArea.Caret.BringCaretToView();
    }

    private void ReplaceCurrent()
    {
        if (_editor is null) return;
        var query = Query;
        var regex = SearchEngine.BuildRegex(query);
        if (regex is not null && _editor.SelectionLength > 0)
        {
            var selected = _editor.SelectedText;
            var whole = regex.Match(selected);
            if (whole.Success && whole.Length == selected.Length)
            {
                var replacement = query.UseRegex ? whole.Result(_replaceBox.Text ?? string.Empty)
                    : _replaceBox.Text ?? string.Empty;
                _editor.Document.Replace(_editor.SelectionStart, _editor.SelectionLength, replacement);
            }
        }

        FindNext();
        UpdateCount();
    }

    private void ReplaceAll()
    {
        if (_editor is null) return;
        var (result, count) = SearchEngine.ReplaceAll(_editor.Text, Query, _replaceBox.Text ?? string.Empty);
        if (count > 0) _editor.Document.Replace(0, _editor.Document.TextLength, result);
        _countText.Text = $"{count} replaced";
    }

    private void UpdateCount()
    {
        if (_editor is null || string.IsNullOrEmpty(_findBox.Text))
        {
            _countText.Text = string.Empty;
            return;
        }

        var matches = SearchEngine.FindAll(_editor.Text, Query);
        _countText.Text = matches.Count == 0 ? "No results" : $"{matches.Count} match{(matches.Count == 1 ? "" : "es")}";
    }
}
