using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Jot.Config;
using Jot.Platform;

namespace Jot;

public partial class App : Application
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private JotConfig _config = new();
    private MainWindow? _window;

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

            var startup = Program.Startup;
            if (!startup.IsAgent)
                ShowFile(startup.Path, startup.OpenPreview);
            // The agent stays hidden and warm; its window is created on the first request.
        }

        base.OnFrameworkInitializationCompleted();
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
                _desktop?.Shutdown();
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
