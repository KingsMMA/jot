using System.IO;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using Jot.Editor;

namespace Jot;

public partial class MainWindow : Window
{
    private readonly TextEditor _editor;
    private readonly TextBlock _statusPath;
    private readonly TextBlock _statusInfo;

    private string? _path;
    private Encoding _encoding = new UTF8Encoding(false);
    private bool _hasBom;
    private LineEnding _lineEnding = LineEnding.Crlf;
    private bool _isDirty;

    public MainWindow() : this(null) { }

    public MainWindow(string? path)
    {
        InitializeComponent();
        _editor = this.FindControl<TextEditor>("Editor")!;
        _statusPath = this.FindControl<TextBlock>("StatusPath")!;
        _statusInfo = this.FindControl<TextBlock>("StatusInfo")!;

        _editor.TextChanged += (_, _) => { _isDirty = true; UpdateTitle(); };
        _editor.TextArea.Caret.PositionChanged += (_, _) => UpdateStatusInfo();

        if (!string.IsNullOrEmpty(path))
            OpenFile(path);
        else
            ApplyDocument(FileDocument.Empty());

        KeyDown += OnKeyDown;
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
        UpdateTitle();
        UpdateStatusInfo();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (ctrl && e.Key == Key.S)
        {
            Save();
            e.Handled = true;
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
