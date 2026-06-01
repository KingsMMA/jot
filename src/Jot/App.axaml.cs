using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Jot.Config;
using Jot.Platform;

namespace Jot;

public partial class App : Application
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private JotConfig _config = new();
    private MainWindow? _window;
    private GlobalHotkey? _hotkey;
    private TrayIcon? _tray;
    private NativeMenuItem? _hotkeyStatus;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            _config = ConfigStore.LoadOrCreateConfig();

            // With the background agent on, the process outlives its window so the next open is instant.
            desktop.ShutdownMode = _config.BackgroundAgent
                ? ShutdownMode.OnExplicitShutdown
                : ShutdownMode.OnLastWindowClose;

            SingleInstance.StartServer(message => Dispatcher.UIThread.Post(() => HandleMessage(message)));
            SetupTray();
            SetupHotkey();

            var startup = Program.Startup;
            if (!startup.IsAgent)
                ShowFile(startup.Path, startup.OpenPreview);
            // The agent stays hidden and warm; its window is created on the first request.
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void DebugLog(string message)
    {
        if (Environment.GetEnvironmentVariable("JOT_DEBUG") != "1") return;
        try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "jot-agent.log"), message + Environment.NewLine); }
        catch { /* ignore */ }
    }

    private void SetupHotkey()
    {
        _hotkey = new GlobalHotkey();
        _hotkey.Pressed += OnHotkeyPressed;
        var registered = _hotkey.Start(_config.Hotkey);
        DebugLog($"hotkey '{_config.Hotkey}' registered={registered}");

        if (_hotkeyStatus is not null)
            _hotkeyStatus.Header = registered
                ? $"Hotkey: {_config.Hotkey}"
                : $"Hotkey {_config.Hotkey} unavailable (in use)";
    }

    private void OnHotkeyPressed()
    {
        // Runs on the STA hotkey thread, so the shell selection can be read here directly. Guard the
        // whole body so a COM hiccup can never tear down the hotkey thread for the rest of the session.
        try
        {
            var selection = ExplorerSelection.GetForegroundSelection();
            DebugLog($"hotkey pressed; selection={selection ?? "<none>"}");
            Dispatcher.UIThread.Post(() => ShowFile(selection, openPreview: false));
        }
        catch (Exception ex)
        {
            DebugLog($"hotkey handler error: {ex.Message}");
        }
    }

    private void SetupTray()
    {
        var icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Jot/Assets/jot.ico")));
        _tray = new TrayIcon { Icon = icon, ToolTipText = "Jot" };
        _tray.Clicked += (_, _) => ShowFile(null, openPreview: false);

        var openLast = new NativeMenuItem("Open last file");
        openLast.Click += (_, _) => ShowFile(null, openPreview: false);

        var editConfig = new NativeMenuItem("Edit configuration");
        editConfig.Click += (_, _) => ShowFile(ConfigStore.ConfigPath, openPreview: false);

        _hotkeyStatus = new NativeMenuItem($"Hotkey: {_config.Hotkey}") { IsEnabled = false };

        var exit = new NativeMenuItem("Exit Jot");
        exit.Click += (_, _) => Quit();

        var menu = new NativeMenu();
        menu.Items.Add(openLast);
        menu.Items.Add(editConfig);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(_hotkeyStatus);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exit);
        _tray.Menu = menu;

        TrayIcon.SetIcons(this, new TrayIcons { _tray });
    }

    private void Quit()
    {
        _hotkey?.Dispose();
        if (_tray is not null) _tray.IsVisible = false;
        _window?.PrepareForShutdown();
        _desktop?.Shutdown();
    }

    private void HandleMessage(string message)
    {
        var parts = message.Split('\t');
        switch (parts[0])
        {
            case IpcMessages.Open:
                ShowFile(parts.Length > 1 ? parts[1] : null, parts.Contains(IpcMessages.PreviewFlag));
                break;
            case IpcMessages.Show:
                ShowFile(null, openPreview: false);
                break;
            case IpcMessages.Quit:
                Quit();
                break;
        }
    }

    /// <summary>Shows the requested file, reusing the single window and warming it on first use.</summary>
    public void ShowFile(string? path, bool openPreview)
    {
        var effectivePath = path ?? LastFileIfExists();

        if (_window is null)
        {
            _window = new MainWindow(_config, effectivePath, openPreview);
            _window.Show();
        }
        else
        {
            _window.LoadPath(effectivePath);
            if (openPreview) _window.EnsurePreview();
            BringToFront(_window);
        }
    }

    private static string? LastFileIfExists()
    {
        var last = ConfigStore.LoadState().LastFile;
        return !string.IsNullOrEmpty(last) && File.Exists(last) ? last : null;
    }

    private static void BringToFront(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Show();
        window.Activate();
        // A brief topmost toggle nudges the window to the foreground reliably.
        window.Topmost = true;
        window.Topmost = false;
    }
}
