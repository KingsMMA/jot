using System.Runtime.InteropServices;

namespace Jot.Platform;

/// <summary>
/// Reads the file currently selected in File Explorer, using the shell automation interface. This
/// is what the global hotkey opens. It must be called from an STA thread because it talks to COM.
/// Selection on the desktop itself is out of scope; an elevated Explorer cannot be read from a
/// non-elevated process.
/// </summary>
public static class ExplorerSelection
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    public static string? GetForegroundSelection()
    {
        try
        {
            var foreground = GetForegroundWindow();
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null) return null;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic windows = shell.Windows();
            int count = windows.Count;

            string? anySelection = null;
            for (var i = 0; i < count; i++)
            {
                dynamic? window = windows.Item(i);
                if (window is null) continue;

                string fullName;
                try { fullName = (string)window.FullName; }
                catch { continue; }
                if (!fullName.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase)) continue;

                IntPtr hwnd;
                try { hwnd = (IntPtr)(long)window.HWND; }
                catch { continue; }

                var selection = TryGetSelection(window);
                if (selection is null) continue;

                // Prefer the Explorer window that currently has focus.
                if (hwnd == foreground) return selection;
                anySelection ??= selection;
            }

            return anySelection;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetSelection(dynamic window)
    {
        try
        {
            dynamic document = window.Document;
            dynamic items = document.SelectedItems();
            if (items.Count <= 0) return null;
            dynamic item = items.Item(0);
            var path = (string)item.Path;
            return string.IsNullOrEmpty(path) ? null : path;
        }
        catch
        {
            return null;
        }
    }
}
