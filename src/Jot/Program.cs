using Avalonia;
using Jot.Platform;

namespace Jot;

internal static class Program
{
    /// <summary>The command line for this launch, read by the app once it starts.</summary>
    public static StartupOptions Startup { get; private set; } = new();

    [STAThread]
    public static int Main(string[] args)
    {
        Startup = StartupOptions.Parse(args);

        // "--quit" just asks the running instance to exit.
        if (Startup.IsQuit)
        {
            SingleInstance.TrySend(IpcMessages.Quit, timeoutMs: 2000);
            return 0;
        }

        // If an instance is already running, hand our request to it and exit.
        if (!SingleInstance.TryBecomePrimary())
        {
            SingleInstance.AllowForegroundForRunningInstance();
            SingleInstance.TrySend(Startup.ToMessage(), timeoutMs: 4000);
            return 0;
        }

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            SingleInstance.Release();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
