using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Jot.Config;
using Jot.Platform;
using Jot.Theming;

namespace Jot.Settings;

/// <summary>
/// A popup editor for the system-wide configuration. It edits a clone of the current configuration,
/// so the advanced map settings that have no control here (language overrides, external formatters,
/// and any unknown keys) are preserved untouched when saving.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly JotConfig _working;
    private readonly Action _onEditRaw;
    private readonly List<JotTheme> _themes = Themes.All.ToList();

    // Parameterless constructor for the XAML designer.
    public SettingsWindow() : this(new JotConfig(), Themes.Get(Themes.DefaultId), () => { }) { }

    public SettingsWindow(JotConfig current, JotTheme theme, Action onEditRaw)
    {
        // Clone so the maps and unknown keys survive even though the form only shows scalar settings.
        _working = ConfigStore.Deserialize(ConfigStore.Serialize(current));
        _onEditRaw = onEditRaw;

        InitializeComponent();
        ApplyTheme(theme);
        LoadFromConfig();

        this.FindControl<Button>("SaveButton")!.Click += (_, _) => Save();
        this.FindControl<Button>("CancelButton")!.Click += (_, _) => Close(null);
        this.FindControl<Button>("EditRawButton")!.Click += (_, _) => { _onEditRaw(); Close(null); };
    }

    private void LoadFromConfig()
    {
        this.FindControl<NumericUpDown>("IndentSizeBox")!.Value = _working.IndentSize;
        this.FindControl<CheckBox>("InsertSpacesToggle")!.IsChecked = _working.InsertSpaces;
        this.FindControl<CheckBox>("AutoCloseToggle")!.IsChecked = _working.AutoCloseBrackets;
        this.FindControl<CheckBox>("WordWrapToggle")!.IsChecked = _working.WordWrap;
        this.FindControl<TextBox>("FontFamilyBox")!.Text = _working.FontFamily;
        this.FindControl<NumericUpDown>("FontSizeBox")!.Value = (decimal)_working.FontSize;
        this.FindControl<ComboBox>("BraceStyleBox")!.SelectedIndex =
            _working.BraceStyle == "next-line" ? 1 : 0;
        this.FindControl<CheckBox>("TrimToggle")!.IsChecked = _working.TrimTrailingWhitespace;
        this.FindControl<CheckBox>("FinalNewlineToggle")!.IsChecked = _working.InsertFinalNewline;
        this.FindControl<CheckBox>("BackgroundAgentToggle")!.IsChecked = _working.BackgroundAgent;
        this.FindControl<CheckBox>("MarkdownPreviewToggle")!.IsChecked = _working.MarkdownPreviewByDefault;
        this.FindControl<TextBox>("HotkeyBox")!.Text = _working.Hotkey;

        var themeBox = this.FindControl<ComboBox>("ThemeBox")!;
        themeBox.ItemsSource = _themes.Select(t => t.Name).ToList();
        var index = _themes.FindIndex(t => string.Equals(t.Id, _working.Theme, StringComparison.OrdinalIgnoreCase));
        themeBox.SelectedIndex = index < 0 ? 0 : index;
    }

    private void Save()
    {
        var hotkey = (this.FindControl<TextBox>("HotkeyBox")!.Text ?? string.Empty).Trim();
        if (!GlobalHotkey.TryParse(hotkey, out _, out _))
        {
            var warning = this.FindControl<TextBlock>("HotkeyWarning")!;
            warning.Text = $"\"{hotkey}\" is not a valid shortcut. Use something like Ctrl+Space or Ctrl+Alt+E.";
            warning.IsVisible = true;
            return;
        }

        _working.IndentSize = (int)(this.FindControl<NumericUpDown>("IndentSizeBox")!.Value ?? 4);
        _working.InsertSpaces = this.FindControl<CheckBox>("InsertSpacesToggle")!.IsChecked ?? true;
        _working.AutoCloseBrackets = this.FindControl<CheckBox>("AutoCloseToggle")!.IsChecked ?? true;
        _working.WordWrap = this.FindControl<CheckBox>("WordWrapToggle")!.IsChecked ?? false;
        _working.FontFamily = string.IsNullOrWhiteSpace(this.FindControl<TextBox>("FontFamilyBox")!.Text)
            ? "Cascadia Code"
            : this.FindControl<TextBox>("FontFamilyBox")!.Text!.Trim();
        _working.FontSize = (double)(this.FindControl<NumericUpDown>("FontSizeBox")!.Value ?? 13);
        _working.BraceStyle = this.FindControl<ComboBox>("BraceStyleBox")!.SelectedIndex == 1
            ? "next-line"
            : "same-line";
        _working.TrimTrailingWhitespace = this.FindControl<CheckBox>("TrimToggle")!.IsChecked ?? true;
        _working.InsertFinalNewline = this.FindControl<CheckBox>("FinalNewlineToggle")!.IsChecked ?? true;
        _working.BackgroundAgent = this.FindControl<CheckBox>("BackgroundAgentToggle")!.IsChecked ?? true;
        _working.MarkdownPreviewByDefault = this.FindControl<CheckBox>("MarkdownPreviewToggle")!.IsChecked ?? true;
        _working.Hotkey = hotkey;

        var themeIndex = this.FindControl<ComboBox>("ThemeBox")!.SelectedIndex;
        if (themeIndex >= 0 && themeIndex < _themes.Count)
            _working.Theme = _themes[themeIndex].Id;

        Close(_working);
    }

    private void ApplyTheme(JotTheme theme)
    {
        RequestedThemeVariant = theme.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        Background = new SolidColorBrush(Color.Parse(theme.EffectiveTextBackground()));
        var bar = this.FindControl<Border>("ButtonBar");
        if (bar is not null) bar.Background = new SolidColorBrush(Color.Parse(theme.Panel));
    }
}
