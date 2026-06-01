using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Jot;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args ?? [];
            var path = args.FirstOrDefault(a => !a.StartsWith('-'));
            var openPreview = args.Contains("--preview");
            desktop.MainWindow = new MainWindow(path, openPreview);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
