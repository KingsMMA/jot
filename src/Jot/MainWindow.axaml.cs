using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Folding;
using Jot.Config;
using Jot.Editor;
using Jot.Formatting;
using Jot.Markdown;
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
    private readonly JotConfig _config;
    private readonly Grid _editorGrid;
    private readonly Border _previewHost;
    private readonly GridSplitter _previewSplitter;
    private readonly DispatcherTimer _previewTimer;
    private MarkdownPreview? _preview;
    private bool _previewVisible;

    private string? _path;
    private Encoding _encoding = new UTF8Encoding(false);
    private bool _hasBom;
    private LineEnding _lineEnding = LineEnding.Crlf;
    private bool _isDirty;
    private bool _suppressLanguageEvent;

    public MainWindow() : this(new JotConfig()) { }

    public MainWindow(JotConfig config, string? path = null, bool openPreview = false)
    {
        _config = config;
        InitializeComponent();
        _editor = this.FindControl<TextEditor>("Editor")!;
        _statusPath = this.FindControl<TextBlock>("StatusPath")!;
        _statusInfo = this.FindControl<TextBlock>("StatusInfo")!;
        _languagePicker = this.FindControl<ComboBox>("LanguagePicker")!;
        _searchPanel = this.FindControl<SearchReplacePanel>("SearchPanel")!;
        _editorGrid = this.FindControl<Grid>("EditorGrid")!;
        _previewHost = this.FindControl<Border>("PreviewHost")!;
        _previewSplitter = this.FindControl<GridSplitter>("PreviewSplitter")!;

        _editor.Options.AllowScrollBelowDocument = true;
        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;
        ApplyConfig();

        _languages.Install(_editor);
        _languagePicker.ItemsSource = _languages.AvailableLanguages();
        _languagePicker.SelectionChanged += OnLanguagePicked;

        _ = new SmartEditing(_editor, _editingOptions);
        _searchPanel.Attach(_editor);
        _foldingManager = FoldingManager.Install(_editor.TextArea);

        // Recompute foldings shortly after edits settle, to keep typing responsive.
        _foldingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _foldingTimer.Tick += (_, _) => { _foldingTimer.Stop(); UpdateFoldings(); };

        // Refresh the Markdown preview a moment after typing pauses.
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _previewTimer.Tick += (_, _) => { _previewTimer.Stop(); RefreshPreview(); };

        _editor.TextChanged += (_, _) =>
        {
            _isDirty = true;
            UpdateTitle();
            _foldingTimer.Stop();
            _foldingTimer.Start();
            if (_previewVisible)
            {
                _previewTimer.Stop();
                _previewTimer.Start();
            }
        };
        _editor.TextArea.Caret.PositionChanged += (_, _) => UpdateStatusInfo();

        if (!string.IsNullOrEmpty(path))
            OpenFile(path);
        else
            ApplyDocument(FileDocument.Empty());

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        // With the agent on, closing the window only hides it so the process stays warm.
        Closing += (_, e) =>
        {
            if (_config.BackgroundAgent)
            {
                e.Cancel = true;
                Hide();
            }
        };
        Opened += (_, _) =>
        {
            // Give the editor focus once shown so typing and shortcuts work immediately.
            _editor.TextArea.Focus();
            if (openPreview) ToggleMarkdownPreview();
        };
    }

    private void OnLanguagePicked(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressLanguageEvent) return;
        if (_languagePicker.SelectedItem is LanguageChoice choice)
        {
            _languages.ApplyById(choice.Id);
            UpdateIndentUnit();
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

    /// <summary>Loads a path into the existing window, or an empty buffer when none is given.</summary>
    public void LoadPath(string? path)
    {
        if (!string.IsNullOrEmpty(path)) OpenFile(path);
        else ApplyDocument(FileDocument.Empty());
    }

    /// <summary>Opens the Markdown preview if it is not already showing.</summary>
    public void EnsurePreview()
    {
        if (!_previewVisible) ToggleMarkdownPreview();
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
        UpdateIndentUnit();
        UpdateFoldings();
        UpdateTitle();
        UpdateStatusInfo();

        if (!string.IsNullOrEmpty(doc.Path))
        {
            var state = ConfigStore.LoadState();
            state.LastFile = doc.Path;
            ConfigStore.SaveState(state);
        }
    }

    private void ApplyConfig()
    {
        _editor.FontFamily = new FontFamily($"{_config.FontFamily},Consolas,Menlo,monospace");
        _editor.FontSize = _config.FontSize;
        _editor.WordWrap = _config.WordWrap;
        _editor.Options.IndentationSize = _config.IndentSize;
        _editor.Options.ConvertTabsToSpaces = _config.InsertSpaces;
        _editingOptions.AutoCloseBrackets = _config.AutoCloseBrackets;
        UpdateIndentUnit();
    }

    private void UpdateIndentUnit()
    {
        _editingOptions.IndentUnit = _config.IndentUnitFor(_languages.CurrentLanguageId);
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
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            if (_searchPanel.IsVisible) _searchPanel.Hide();
            else HideOrClose();
            e.Handled = true;
            return;
        }

        switch (KeyMap.Resolve(e.Key, e.KeyModifiers))
        {
            case EditorCommand.Save:
                Save();
                break;
            case EditorCommand.Find:
                _searchPanel.ShowFind();
                break;
            case EditorCommand.Replace:
                _searchPanel.ShowReplace();
                break;
            case EditorCommand.Format:
                FormatDocument();
                break;
            case EditorCommand.ToggleMarkdownPreview:
                ToggleMarkdownPreview();
                break;
            case EditorCommand.OpenConfig:
                OpenConfig();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void ToggleMarkdownPreview()
    {
        if (_previewVisible)
        {
            _previewVisible = false;
            _editorGrid.ColumnDefinitions[2].Width = new GridLength(0);
            _previewSplitter.IsVisible = false;
            return;
        }

        _preview ??= CreatePreview();
        _previewVisible = true;
        _editorGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        _editorGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
        _previewSplitter.IsVisible = true;
        RefreshPreview();
    }

    private MarkdownPreview CreatePreview()
    {
        var preview = new MarkdownPreview();
        _previewHost.Child = preview;
        return preview;
    }

    private void RefreshPreview() => _preview?.Update(_editor.Text);

    /// <summary>
    /// Escape closes the window. With the background agent on, the window is only hidden so the next
    /// open is instant; otherwise the process exits.
    /// </summary>
    private void HideOrClose()
    {
        if (_config.BackgroundAgent) Hide();
        else Close();
    }

    private void FormatDocument()
    {
        var caret = _editor.CaretOffset;
        var result = DocumentFormatter.Format(_languages.CurrentLanguageId, _editor.Text, _config);
        if (result.Changed)
        {
            _editor.Document.Replace(0, _editor.Document.TextLength, result.Text);
            _editor.CaretOffset = Math.Min(caret, _editor.Document.TextLength);
        }
        _statusInfo.Text = result.Message;
    }

    private void OpenConfig()
    {
        ConfigStore.LoadOrCreateConfig();
        OpenFile(ConfigStore.ConfigPath);
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
            var options = new SaveOptions(_config.TrimTrailingWhitespace, _config.InsertFinalNewline);
            FileDocument.Save(_path, _editor.Text, _encoding, _hasBom, _lineEnding, options);
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
