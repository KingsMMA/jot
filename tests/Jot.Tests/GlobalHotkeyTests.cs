using Jot.Platform;
using Xunit;

namespace Jot.Tests;

public class GlobalHotkeyTests
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    [Fact]
    public void Parse_CtrlSpace()
    {
        Assert.True(GlobalHotkey.TryParse("Ctrl+Space", out var mods, out var vk));
        Assert.Equal(ModControl, mods);
        Assert.Equal(0x20u, vk);
    }

    [Fact]
    public void Parse_MultipleModifiers()
    {
        Assert.True(GlobalHotkey.TryParse("Ctrl+Alt+E", out var mods, out var vk));
        Assert.Equal(ModControl | ModAlt, mods);
        Assert.Equal((uint)'E', vk);
    }

    [Fact]
    public void Parse_WinShiftFunctionKey()
    {
        Assert.True(GlobalHotkey.TryParse("Win+Shift+F2", out var mods, out var vk));
        Assert.Equal(ModWin | ModShift, mods);
        Assert.Equal(0x71u, vk); // F2
    }

    [Fact]
    public void Parse_Digit()
    {
        Assert.True(GlobalHotkey.TryParse("Ctrl+1", out _, out var vk));
        Assert.Equal((uint)'1', vk);
    }

    [Theory]
    [InlineData("Ctrl+")]
    [InlineData("")]
    [InlineData("Ctrl+Alt")]
    public void Parse_Invalid_ReturnsFalse(string hotkey)
    {
        Assert.False(GlobalHotkey.TryParse(hotkey, out _, out _));
    }
}
