using Avalonia.Input;

namespace Jot.Editor;

/// <summary>The editor-level actions that a keyboard shortcut can trigger.</summary>
public enum EditorCommand
{
    None,
    Save,
    Find,
    Replace,
    Format,
    ToggleMarkdownPreview,
    OpenConfig,
}

/// <summary>
/// Maps a key press to an editor command. Kept as a pure function, separate from the window, so the
/// full shortcut scheme can be unit tested without driving the UI.
/// </summary>
public static class KeyMap
{
    public static EditorCommand Resolve(Key key, KeyModifiers modifiers)
    {
        var ctrl = modifiers.HasFlag(KeyModifiers.Control);
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        var alt = modifiers.HasFlag(KeyModifiers.Alt);

        return (key, ctrl, shift, alt) switch
        {
            (Key.S, true, false, false) => EditorCommand.Save,
            (Key.F, true, true, false) => EditorCommand.Format,
            (Key.F, false, true, true) => EditorCommand.Format,
            (Key.F, true, false, false) => EditorCommand.Find,
            (Key.H, true, false, false) => EditorCommand.Replace,
            (Key.V, true, true, false) => EditorCommand.ToggleMarkdownPreview,
            (Key.OemComma, true, false, false) => EditorCommand.OpenConfig,
            _ => EditorCommand.None,
        };
    }
}
