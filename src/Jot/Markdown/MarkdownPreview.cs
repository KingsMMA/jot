using System.Drawing;
using System.IO;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Web.WebView2.Core;

namespace Jot.Markdown;

/// <summary>
/// A live Markdown preview rendered with GitHub-style HTML inside an embedded WebView2 control.
/// The page shell loads once; each edit swaps the article body via script so the scroll position
/// is kept and there is no reload flicker. The WebView2 runtime ships with Windows 11, and the
/// installer ensures it, so this is the high-fidelity default.
/// </summary>
public sealed class MarkdownPreview : NativeControlHost
{
    private CoreWebView2Controller? _controller;
    private bool _shellReady;
    private string _pendingBody = string.Empty;

    public void Update(string markdown)
    {
        _pendingBody = MarkdownRenderer.ToBodyHtml(markdown);
        if (_shellReady) ApplyBody();
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle = base.CreateNativeControlCore(parent);
        _ = InitialiseAsync(handle.Handle);
        return handle;
    }

    private async Task InitialiseAsync(IntPtr hwnd)
    {
        try
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "Jot", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: dataDir);
            _controller = await environment.CreateCoreWebView2ControllerAsync(hwnd);

            var core = _controller.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;

            UpdateBounds();
            core.NavigationCompleted += (_, _) =>
            {
                _shellReady = true;
                ApplyBody();
            };
            core.NavigateToString(MarkdownRenderer.Shell());
        }
        catch
        {
            // If WebView2 is unavailable the preview simply stays blank; editing is unaffected.
        }
    }

    private void ApplyBody()
    {
        if (_controller is null) return;
        var script = "document.getElementById('content').innerHTML = " + JsonSerializer.Serialize(_pendingBody);
        _ = _controller.CoreWebView2.ExecuteScriptAsync(script);
    }

    private void UpdateBounds()
    {
        if (_controller is null) return;
        var scale = (this.GetVisualRoot() as TopLevel)?.RenderScaling ?? 1.0;
        _controller.Bounds = new Rectangle(0, 0,
            Math.Max(0, (int)(Bounds.Width * scale)),
            Math.Max(0, (int)(Bounds.Height * scale)));
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Dispatcher.UIThread.Post(UpdateBounds);
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _controller?.Close();
        _controller = null;
        base.DestroyNativeControlCore(control);
    }
}
