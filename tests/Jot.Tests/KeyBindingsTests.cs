using Avalonia.Input;
using Jot.Editor;
using Xunit;

namespace Jot.Tests;

public class KeyBindingsTests
{
    [Theory]
    [InlineData(Key.S, KeyModifiers.Control, EditorCommand.Save)]
    [InlineData(Key.F, KeyModifiers.Control, EditorCommand.Find)]
    [InlineData(Key.H, KeyModifiers.Control, EditorCommand.Replace)]
    [InlineData(Key.F, KeyModifiers.Control | KeyModifiers.Shift, EditorCommand.Format)]
    [InlineData(Key.F, KeyModifiers.Alt | KeyModifiers.Shift, EditorCommand.Format)]
    [InlineData(Key.V, KeyModifiers.Control | KeyModifiers.Shift, EditorCommand.ToggleMarkdownPreview)]
    [InlineData(Key.OemComma, KeyModifiers.Control, EditorCommand.OpenConfig)]
    public void Resolve_MapsShortcuts(Key key, KeyModifiers modifiers, EditorCommand expected)
    {
        Assert.Equal(expected, KeyMap.Resolve(key, modifiers));
    }

    [Theory]
    [InlineData(Key.F, KeyModifiers.None)]
    [InlineData(Key.A, KeyModifiers.Control)]
    [InlineData(Key.S, KeyModifiers.Control | KeyModifiers.Shift)]
    [InlineData(Key.S, KeyModifiers.None)]
    public void Resolve_UnmappedKeys_ReturnNone(Key key, KeyModifiers modifiers)
    {
        Assert.Equal(EditorCommand.None, KeyMap.Resolve(key, modifiers));
    }
}
