using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Folding;
using Jot.Config;
using Jot.Diagnostics;
using Jot.Editor;
using Jot.Formatting;
using Jot.Markdown;
using Jot.Search;
using Jot.Theming;

namespace Jot;

public partial class MainWindow : Window
{
    private readonly TextEditor _editor;
    private readonly TextBlock _statusPath;
    private readonly TextBlock _statusInfo;
    private readonly ComboBox _languagePicker;
    private readonly TextBlock _statusDiagnostics;
    private readonly SearchReplacePanel _searchPanel;
    private readonly SquiggleRenderer _squiggles = new();
    private readonly DispatcherTimer _diagnosticsTimer;
    private readonly LanguageService _languages = new();
    private readonly EditingOptions _editingOptions = new();
    private readonly FoldingManager _foldingManager;
    private readonly DispatcherTimer _foldingTimer;
    private JotConfig _config;

    /// <summary>Raised when the settings editor saves a new configuration.</summary>
    public event Action<JotConfig>? SettingsSaved;
    private readonly Grid _editorGrid;
    private readonly Border _previewHost;
    private readonly GridSplitter _previewSplitter;
    private readonly DispatcherTimer _previewTimer;
    private readonly Border _statusBar;
    private MarkdownPreview? _preview;
    private bool _previewVisible;
    private JotTheme _theme = Themes.Get(Themes.DefaultId);

    private string? _path;
    private Encoding _encoding = new UTF8Encoding(false);
    private bool _hasBom;
    private LineEnding _lineEnding = LineEnding.Crlf;
    private bool _isDirty;
    private bool _suppressLanguageEvent;
    private bool _exiting;
    private bool _shown;

    /// <summary>Lets the application terminate even when the background agent is on.</summary>
    public void PrepareForShutdown() => _exiting = true;

    public MainWindow() : this(new JotConfig()) { }

    public MainWindow(JotConfig config, string? path = null, bool openPreview = false)
    {
        _config = config;
        InitializeComponent();
        _editor = this.FindControl<TextEditor>("Editor")!;
        _statusPath = this.FindControl<TextBlock>("StatusPath")!;
        _statusInfo = this.FindControl<TextBlock>("StatusInfo")!;
        _languagePicker = this.FindControl<ComboBox>("LanguagePicker")!;
        _statusDiagnostics = this.FindControl<TextBlock>("StatusDiagnostics")!;
        _searchPanel = this.FindControl<SearchReplacePanel>("SearchPanel")!;
        _editorGrid = this.FindControl<Grid>("EditorGrid")!;
        _previewHost = this.FindControl<Border>("PreviewHost")!;
        _previewSplitter = this.FindControl<GridSplitter>("PreviewSplitter")!;
        _statusBar = this.FindControl<Border>("StatusBar")!;

        _editor.Options.AllowScrollBelowDocument = true;
        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;
        ApplyConfig();

        _languages.Install(_editor);
        _languagePicker.ItemsSource = _languages.AvailableLanguages();
        _languagePicker.SelectionChanged += OnLanguagePicked;
        ApplyTheme(Themes.Get(_config.Theme));

        _ = new SmartEditing(_editor, _editingOptions);
        _searchPanel.Attach(_editor);
        _foldingManager = FoldingManager.Install(_editor.TextArea);
        _editor.TextArea.TextView.BackgroundRenderers.Add(_squiggles);
        _statusDiagnostics.PointerPressed += (_, _) => GoToFirstProblem();

        _diagnosticsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _diagnosticsTimer.Tick += (_, _) => { _diagnosticsTimer.Stop(); UpdateDiagnostics(); };

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
            _diagnosticsTimer.Stop();
            _diagnosticsTimer.Start();
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
        // With the agent on, closing the window only hides it so the process stays warm — unless
        // we are genuinely shutting down (tray Exit), in which case the close must go through.
        Closing += (_, e) =>
        {
            if (_config.BackgroundAgent && !_exiting)
            {
                e.Cancel = true;
                Hide();
            }
        };
        Opened += (_, _) =>
        {
            _shown = true;
            // Give the editor focus once shown so typing and shortcuts work immediately.
            _editor.TextArea.Focus();
            ApplyDefaultPreviewState();
            if (openPreview) EnsurePreview();
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
            UpdateDiagnostics();
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

    /// <summary>
    /// Applies the configured default: when enabled, the preview is open for Markdown files and closed
    /// for everything else. A no-op when the option is off, leaving the preview under manual control.
    /// </summary>
    private void ApplyDefaultPreviewState()
    {
        if (!_config.MarkdownPreviewByDefault) return;

        var isMarkdown = _languages.CurrentLanguageId == "markdown";
        if (isMarkdown && !_previewVisible) ToggleMarkdownPreview();
        else if (!isMarkdown && _previewVisible) ToggleMarkdownPreview();
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
        UpdateDiagnostics();
        UpdateTitle();
        UpdateStatusInfo();

        // Once the window is up, follow the file type for the preview. Doing this before the window is
        // shown would create the web view too early, so the first document is handled in Opened.
        if (_shown) ApplyDefaultPreviewState();

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

    private void UpdateDiagnostics()
    {
        var length = _editor.Document.TextLength;
        // Clamp each diagnostic into the current document. An error at the very end of the file (an
        // unterminated value, say) reports an offset equal to the length, so pull it back onto the
        // last character rather than dropping it.
        var problems = DiagnosticsAnalyzer.Analyze(_languages.CurrentLanguageId, _editor.Text)
            .Where(_ => length > 0)
            .Select(d =>
            {
                var offset = Math.Min(d.Offset, length - 1);
                return d with { Offset = offset, Length = Math.Min(d.Length, length - offset) };
            })
            .ToList();

        _squiggles.Diagnostics = problems;
        _editor.TextArea.TextView.InvalidateVisual();

        if (problems.Count == 0)
        {
            _statusDiagnostics.Text = string.Empty;
        }
        else
        {
            var first = problems[0];
            var line = _editor.Document.GetLineByOffset(first.Offset).LineNumber;
            _statusDiagnostics.Text = problems.Count == 1 ? $"⚠ 1 problem" : $"⚠ {problems.Count} problems";
            ToolTip.SetTip(_statusDiagnostics, $"Line {line}: {first.Message}");
        }
    }

    private void GoToFirstProblem()
    {
        if (_squiggles.Diagnostics.Count == 0) return;
        var first = _squiggles.Diagnostics[0];
        _editor.CaretOffset = Math.Min(first.Offset, _editor.Document.TextLength);
        _editor.TextArea.Caret.BringCaretToView();
        _editor.Focus();
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
            case EditorCommand.OpenSettings:
                OpenSettings();
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
        var preview = new MarkdownPreview(_theme.Markdown());
        _previewHost.Child = preview;
        return preview;
    }

    private void RefreshPreview() => _preview?.Update(_editor.Text);

    /// <summary>Switches the theme by id and persists the choice; used by the tray theme menu.</summary>
    public void ApplyThemeById(string themeId)
    {
        ApplyTheme(Themes.Get(themeId));
    }

    /// <summary>Applies a theme to the window, editor, syntax colours, and Markdown preview.</summary>
    public void ApplyTheme(JotTheme theme)
    {
        _theme = theme;
        RequestedThemeVariant = theme.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;

        // Window backdrop: a background image, the system acrylic material, or a solid colour.
        if (theme.BackgroundImage is not null && TryLoadImageBrush(theme.BackgroundImage) is { } image)
        {
            TransparencyLevelHint = [WindowTransparencyLevel.None];
            Background = image;
        }
        else if (theme.Acrylic)
        {
            TransparencyLevelHint = [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur];
            Background = Brushes.Transparent;
        }
        else
        {
            TransparencyLevelHint = [WindowTransparencyLevel.None];
            Background = Brush(theme.Background);
        }

        // Editor surface and text.
        _editor.Background = Brush(theme.SurfaceArgb());
        _editor.Foreground = Brush(theme.Foreground);
        _editor.LineNumbersForeground = Brush(theme.LineNumber);
        _editor.TextArea.SelectionBrush = Brush(Contrast.WithAlpha(theme.Selection, 0.5));

        // Status bar and chrome.
        _statusBar.Background = Brush(theme.PanelArgb());
        _statusPath.Foreground = Brush(theme.Muted);
        _statusInfo.Foreground = Brush(theme.Muted);
        _previewSplitter.Background = Brush(theme.Border);
        _previewHost.Background = Brush(theme.EffectiveTextBackground());

        _languages.ApplyTheme(theme.TextMateTheme);
        _preview?.SetPalette(theme.Markdown());
    }

    private static SolidColorBrush Brush(string hex) => new(Color.Parse(hex));

    private static ImageBrush? TryLoadImageBrush(string assetPath)
    {
        // A missing or unreadable backdrop must never stop the window from opening; the caller then
        // falls back to the theme's solid background.
        try
        {
            var bitmap = new Bitmap(AssetLoader.Open(new Uri($"avares://Jot/Assets/{assetPath}")));
            return new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
        }
        catch
        {
            return null;
        }
    }

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

    /// <summary>Opens the settings editor; on save, notifies listeners with the new configuration.</summary>
    public async void OpenSettings()
    {
        Activate();
        var dialog = new Settings.SettingsWindow(_config, _theme, onEditRaw: OpenConfig);
        var result = await dialog.ShowDialog<JotConfig?>(this);
        if (result is not null)
            SettingsSaved?.Invoke(result);
    }

    /// <summary>Adopts a new configuration and applies everything that can change without a restart.</summary>
    public void ApplyNewConfig(JotConfig config)
    {
        _config = config;
        ApplyConfig();
        ApplyTheme(Themes.Get(_config.Theme));
        UpdateDiagnostics();
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
