using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Folding;
using Jot.Editor;
using Jot.Search;

namespace Jot;

public partial class MainWindow : Window
{
    private readonly TextEditor _editor;
    private readonly TextBlock _statusPath;
    private readonly TextBlock _statusInfo;
    private readonly ComboBox _languagePicker;
    private readonly SearchReplacePanel _searchPanel;
    private readonly LanguageService _languages = new();
    private readonly EditingOptions _editingOptions = new();
    private readonly FoldingManager _foldingManager;
    private readonly DispatcherTimer _foldingTimer;

    private string? _path;
    private Encoding _encoding = new UTF8Encoding(false);
    private bool _hasBom;
    private LineEnding _lineEnding = LineEnding.Crlf;
    private bool _isDirty;
    private bool _suppressLanguageEvent;

    public MainWindow() : this(null) { }

    public MainWindow(string? path)
    {
        InitializeComponent();
        _editor = this.FindControl<TextEditor>("Editor")!;
        _statusPath = this.FindControl<TextBlock>("StatusPath")!;
        _statusInfo = this.FindControl<TextBlock>("StatusInfo")!;
        _languagePicker = this.FindControl<ComboBox>("LanguagePicker")!;
        _searchPanel = this.FindControl<SearchReplacePanel>("SearchPanel")!;

        _editor.Options.IndentationSize = 4;
        _editor.Options.ConvertTabsToSpaces = true;
        _editor.Options.AllowScrollBelowDocument = true;
        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;

        _languages.Install(_editor);
        _languagePicker.ItemsSource = _languages.AvailableLanguages();
        _languagePicker.SelectionChanged += OnLanguagePicked;

        _ = new SmartEditing(_editor, _editingOptions);
        _searchPanel.Attach(_editor);
        _foldingManager = FoldingManager.Install(_editor.TextArea);

        // Recompute foldings shortly after edits settle, to keep typing responsive.
        _foldingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _foldingTimer.Tick += (_, _) => { _foldingTimer.Stop(); UpdateFoldings(); };

        _editor.TextChanged += (_, _) =>
        {
            _isDirty = true;
            UpdateTitle();
            _foldingTimer.Stop();
            _foldingTimer.Start();
        };
        _editor.TextArea.Caret.PositionChanged += (_, _) => UpdateStatusInfo();

        if (!string.IsNullOrEmpty(path))
            OpenFile(path);
        else
            ApplyDocument(FileDocument.Empty());

        KeyDown += OnKeyDown;
    }

    private void OnLanguagePicked(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressLanguageEvent) return;
        if (_languagePicker.SelectedItem is LanguageChoice choice)
        {
            _languages.ApplyById(choice.Id);
            UpdateFoldings();
        }
    }

    private void SyncLanguagePicker()
    {
        _suppressLanguageEvent = true;
        if (_languagePicker.ItemsSource is IEnumerable<LanguageChoice> items)
        {
            foreach (var item in items)
            {
                if (item.Id == _languages.CurrentLanguageId)
                {
                    _languagePicker.SelectedItem = item;
                    break;
                }
            }
        }
        _suppressLanguageEvent = false;
    }

    public void OpenFile(string path)
    {
        try
        {
            ApplyDocument(FileDocument.Load(path));
        }
        catch (Exception ex)
        {
            _statusInfo.Text = $"Could not open: {ex.Message}";
        }
    }

    private void ApplyDocument(FileDocument doc)
    {
        _path = doc.Path;
        _encoding = doc.Encoding;
        _hasBom = doc.HasBom;
        _lineEnding = doc.LineEnding;
        _editor.Text = doc.Text;
        _isDirty = false;
        _languages.DetectAndApply(doc.Path, doc.Text);
        SyncLanguagePicker();
        UpdateFoldings();
        UpdateTitle();
        UpdateStatusInfo();
    }

    private void UpdateFoldings()
    {
        try
        {
            var foldings = FoldingStrategies.Create(_languages.CurrentLanguageId, _editor.Document);
            _foldingManager.UpdateFoldings(foldings, -1);
        }
        catch
        {
            // Folding is best-effort; never let it interrupt editing.
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        switch (e.Key)
        {
            case Key.S when ctrl:
                Save();
                e.Handled = true;
                break;
            case Key.F when ctrl:
                _searchPanel.ShowFind();
                e.Handled = true;
                break;
            case Key.H when ctrl:
                _searchPanel.ShowReplace();
                e.Handled = true;
                break;
        }
    }

    private void Save()
    {
        if (string.IsNullOrEmpty(_path))
        {
            _statusInfo.Text = "No file path (open a file first)";
            return;
        }

        try
        {
            FileDocument.Save(_path, _editor.Text, _encoding, _hasBom, _lineEnding, SaveOptions.Default);
            _isDirty = false;
            UpdateTitle();
            _statusInfo.Text = "Saved";
        }
        catch (Exception ex)
        {
            _statusInfo.Text = $"Save failed: {ex.Message}";
        }
    }

    private void UpdateTitle()
    {
        var name = string.IsNullOrEmpty(_path) ? "untitled" : Path.GetFileName(_path);
        Title = (_isDirty ? "● " : string.Empty) + name + " — Jot";
        _statusPath.Text = _path ?? "untitled";
    }

    private void UpdateStatusInfo()
    {
        var loc = _editor.TextArea.Caret.Location;
        var eol = _lineEnding switch
        {
            LineEnding.Lf => "LF",
            LineEnding.Cr => "CR",
            _ => "CRLF",
        };
        _statusInfo.Text = $"Ln {loc.Line}, Col {loc.Column}    {eol}";
    }
}
