using Avalonia;

namespace Jot;

internal static class Program
{
    // Avalonia configuration; do not remove or rename without updating the build action.
    [STAThread]
    public static int Main(string[] args)
    {
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
