using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Jot.Platform;

/// <summary>
/// Registers a system-wide hotkey. The registration and its message pump live on a dedicated STA
/// thread so that the hotkey handler can read the File Explorer selection through COM without
/// marshalling. If the chord is already taken (for example by PowerToys Peek on Ctrl+Space),
/// <see cref="Registered"/> is false and the caller can surface that to the user.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int HotkeyId = 0xB01D;
    private const uint WmHotkey = 0x0312;
    private const uint WmQuit = 0x0012;
    private const uint ModNoRepeat = 0x4000;

    private Thread? _thread;
    private uint _threadId;
    private volatile bool _running;

    /// <summary>Raised on the hotkey thread (STA) each time the chord is pressed.</summary>
    public event Action? Pressed;

    public bool Registered { get; private set; }

    public string? Chord { get; private set; }

    public bool Start(string hotkey)
    {
        Chord = hotkey;
        if (!TryParse(hotkey, out var modifiers, out var virtualKey))
            return false;

        var ready = new TaskCompletionSource<bool>();
        _thread = new Thread(() => Run(modifiers, virtualKey, ready)) { IsBackground = true, Name = "Jot hotkey" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        Registered = ready.Task.GetAwaiter().GetResult();
        return Registered;
    }

    private void Run(uint modifiers, uint virtualKey, TaskCompletionSource<bool> ready)
    {
        _threadId = GetCurrentThreadId();
        var ok = RegisterHotKey(IntPtr.Zero, HotkeyId, modifiers | ModNoRepeat, virtualKey);
        ready.SetResult(ok);
        if (!ok) return;

        _running = true;
        while (_running && GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            if (message.message == WmHotkey)
                Pressed?.Invoke();
        }

        UnregisterHotKey(IntPtr.Zero, HotkeyId);
    }

    /// <summary>Parses a chord such as "Ctrl+Space" or "Ctrl+Alt+E" into Win32 modifiers and a key.</summary>
    public static bool TryParse(string hotkey, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(hotkey)) return false;

        foreach (var raw in hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= 0x0002; break;
                case "alt": modifiers |= 0x0001; break;
                case "shift": modifiers |= 0x0004; break;
                case "win" or "super" or "meta": modifiers |= 0x0008; break;
                default:
                    if (!TryParseKey(raw, out virtualKey)) return false;
                    break;
            }
        }

        return virtualKey != 0;
    }

    private static bool TryParseKey(string key, out uint virtualKey)
    {
        virtualKey = key.ToLowerInvariant() switch
        {
            "space" => 0x20,
            "tab" => 0x09,
            "enter" or "return" => 0x0D,
            "escape" or "esc" => 0x1B,
            "backspace" => 0x08,
            "insert" => 0x2D,
            "delete" or "del" => 0x2E,
            "home" => 0x24,
            "end" => 0x23,
            _ => ResolveCharOrFunction(key),
        };
        return virtualKey != 0;
    }

    private static uint ResolveCharOrFunction(string key)
    {
        if (key.Length >= 2 && (key[0] is 'f' or 'F') && int.TryParse(key[1..], out var n) && n is >= 1 and <= 24)
            return (uint)(0x70 + (n - 1)); // F1..F24

        if (key.Length == 1)
        {
            var c = char.ToUpperInvariant(key[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9') return c; // VK codes match ASCII here
        }

        return 0;
    }

    public void Dispose()
    {
        _running = false;
        if (_threadId != 0)
            PostThreadMessage(_threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Msg lpMsg, IntPtr hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
