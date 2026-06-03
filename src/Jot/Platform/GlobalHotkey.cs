using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jot.Platform;

/// <summary>
/// Watches for a configured chord (such as "Ctrl+Space") and raises <see cref="Pressed"/> only while
/// File Explorer is the foreground window, so the chord opens the selected file there but is left
/// untouched everywhere else.
///
/// This uses a low-level keyboard hook rather than <c>RegisterHotKey</c> on purpose. A registered
/// hotkey is global: it fires (and swallows the keys) in every application, which meant the chord was
/// captured inside games, browsers, and so on. The hook lets us inspect the foreground window first
/// and only consume the keys when they are ours; any other time they pass straight through to the
/// active application. The hook and its message pump live on a dedicated STA thread so the
/// <see cref="Pressed"/> handler can read the Explorer selection through COM without marshalling.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmSysKeyUp = 0x0105;
    private const uint WmQuit = 0x0012;
    private const uint WmTrigger = 0x8001; // WM_APP + 1: posted from the hook to run Pressed on the loop.

    // Virtual-key codes for the modifier keys we test the live keyboard state against.
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12; // Alt
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    // GetAncestor flags.
    private const uint GaRoot = 2;
    private const uint GaRootOwner = 3;

    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hook;
    private uint _modifiers;
    private uint _virtualKey;
    private bool _chordHeld;
    private volatile bool _running;

    // Held so the delegate passed to the OS is never garbage collected while the hook is installed.
    private LowLevelKeyboardProc? _proc;

    /// <summary>Raised on the hotkey thread (STA) when the chord is pressed while Explorer is focused.</summary>
    public event Action? Pressed;

    public bool Registered { get; private set; }

    public string? Chord { get; private set; }

    public bool Start(string hotkey)
    {
        Chord = hotkey;
        if (!TryParse(hotkey, out _modifiers, out _virtualKey))
            return false;

        var ready = new TaskCompletionSource<bool>();
        _thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "Jot hotkey" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        Registered = ready.Task.GetAwaiter().GetResult();
        return Registered;
    }

    private void Run(TaskCompletionSource<bool> ready)
    {
        _threadId = GetCurrentThreadId();
        _proc = HookCallback;
        _hook = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(null), 0);
        ready.SetResult(_hook != IntPtr.Zero);
        if (_hook == IntPtr.Zero) return;

        _running = true;
        while (_running && GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            if (message.message == WmTrigger)
                Pressed?.Invoke();
        }

        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var message = (uint)wParam;
                var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                var isDown = message is WmKeyDown or WmSysKeyDown;
                var isUp = message is WmKeyUp or WmSysKeyUp;

                if (data.vkCode == _virtualKey)
                {
                    if (isUp)
                    {
                        _chordHeld = false;
                    }
                    else if (isDown && ModifiersMatch(_modifiers, Held(VkControl), Held(VkMenu), Held(VkShift), Held(VkLWin) || Held(VkRWin)) && ForegroundIsExplorer())
                    {
                        // Fire once per press (ignore auto-repeat) but keep swallowing the key while it
                        // is held so it never leaks a stray space into the focused Explorer item.
                        if (!_chordHeld)
                        {
                            _chordHeld = true;
                            PostThreadMessage(_threadId, WmTrigger, IntPtr.Zero, IntPtr.Zero);
                        }
                        return 1; // consume: the chord is ours here.
                    }
                }
            }
        }
        catch
        {
            // A hook callback must never throw into native code; fall through and pass the key on.
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    /// <summary>True when the live modifier state exactly matches the chord's required modifiers.</summary>
    public static bool ModifiersMatch(uint modifiers, bool ctrl, bool alt, bool shift, bool win)
    {
        var wantCtrl = (modifiers & 0x0002) != 0;
        var wantAlt = (modifiers & 0x0001) != 0;
        var wantShift = (modifiers & 0x0004) != 0;
        var wantWin = (modifiers & 0x0008) != 0;
        return ctrl == wantCtrl && alt == wantAlt && shift == wantShift && win == wantWin;
    }

    /// <summary>True for the top-level window classes used by File Explorer browser windows.</summary>
    public static bool IsExplorerClass(string? className) =>
        className is "CabinetWClass" or "ExploreWClass";

    /// <summary>
    /// True when <paramref name="hwnd"/> belongs to a File Explorer browser window. Checks the window
    /// itself and its top-level ancestors for the classic Explorer classes, and also recognises the
    /// Windows 11 XAML island that hosts the file list (which can hold the keyboard focus on its own).
    /// The taskbar and desktop, although also owned by explorer.exe, do not match.
    /// </summary>
    public static bool IsExplorerWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;

        if (IsExplorerClass(ClassOf(hwnd))
            || IsExplorerClass(ClassOf(GetAncestor(hwnd, GaRoot)))
            || IsExplorerClass(ClassOf(GetAncestor(hwnd, GaRootOwner))))
            return true;

        return ClassOf(hwnd) == "XamlExplorerHostIslandWindow" && IsExplorerProcess(hwnd);
    }

    private static string ClassOf(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return string.Empty;
        var buffer = new StringBuilder(64);
        return GetClassName(hwnd, buffer, buffer.Capacity) > 0 ? buffer.ToString() : string.Empty;
    }

    private static bool IsExplorerProcess(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var pid);
        try { return string.Equals(Process.GetProcessById((int)pid).ProcessName, "explorer", StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    private static bool Held(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static bool ForegroundIsExplorer() => IsExplorerWindow(GetForegroundWindow());

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

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Msg lpMsg, IntPtr hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
