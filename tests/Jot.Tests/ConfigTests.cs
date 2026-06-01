using Jot.Config;
using Xunit;

namespace Jot.Tests;

public class ConfigTests
{
    [Fact]
    public void Defaults_MatchTheSpecifiedBehaviour()
    {
        var config = new JotConfig();
        Assert.Equal(4, config.IndentSize);
        Assert.True(config.InsertSpaces);
        Assert.Equal("same-line", config.BraceStyle);
        Assert.True(config.TrimTrailingWhitespace);
        Assert.True(config.InsertFinalNewline);
        Assert.Equal("Ctrl+Space", config.Hotkey);
        Assert.True(config.BackgroundAgent);
        Assert.True(config.AutoCloseBrackets);
    }

    [Fact]
    public void RoundTrip_PreservesDefaults()
    {
        var json = ConfigStore.Serialize(new JotConfig());
        var restored = ConfigStore.Deserialize(json);
        Assert.Equal(4, restored.IndentSize);
        Assert.Equal("same-line", restored.BraceStyle);
        Assert.Equal("Ctrl+Space", restored.Hotkey);
    }

    [Fact]
    public void Serialize_WritesHotkeyWithoutEscaping()
    {
        Assert.Contains("\"Ctrl+Space\"", ConfigStore.Serialize(new JotConfig()));
    }

    [Fact]
    public void Deserialize_MissingKeys_FallBackToDefaults()
    {
        var restored = ConfigStore.Deserialize("{ \"indentSize\": 2 }");
        Assert.Equal(2, restored.IndentSize);
        Assert.True(restored.InsertSpaces);          // default
        Assert.Equal("Ctrl+Space", restored.Hotkey); // default
    }

    [Fact]
    public void Deserialize_PreservesUnknownKeys_OnReSave()
    {
        var restored = ConfigStore.Deserialize("{ \"indentSize\": 8, \"futureSetting\": 42 }");
        var reSaved = ConfigStore.Serialize(restored);
        Assert.Contains("futureSetting", reSaved);
        Assert.Contains("42", reSaved);
    }

    [Fact]
    public void Deserialize_ToleratesCommentsAndTrailingCommas()
    {
        var restored = ConfigStore.Deserialize("{\n  // a comment\n  \"indentSize\": 3,\n}");
        Assert.Equal(3, restored.IndentSize);
    }
}
