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
        Assert.True(config.MarkdownPreviewByDefault);
    }

    [Fact]
    public void MarkdownPreviewByDefault_RoundTrips()
    {
        var json = ConfigStore.Serialize(new JotConfig { MarkdownPreviewByDefault = false });
        Assert.False(ConfigStore.Deserialize(json).MarkdownPreviewByDefault);
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
    public void Clone_PreservesAdvancedMaps_TheWaySettingsEditorRelies()
    {
        // The settings editor clones the config via Serialize/Deserialize and only edits scalar
        // fields, so the maps (which have no control in the form) must survive a round-trip intact.
        var original = new JotConfig
        {
            Theme = "rose",
            LanguageOverrides = { ["go"] = new LanguageOverride { InsertSpaces = false } },
            ExternalFormatters = { ["python"] = "black -" },
        };

        var clone = ConfigStore.Deserialize(ConfigStore.Serialize(original));

        Assert.True(clone.LanguageOverrides.ContainsKey("go"));
        Assert.False(clone.LanguageOverrides["go"].InsertSpaces);
        Assert.Equal("black -", clone.ExternalFormatters["python"]);
        Assert.Equal("rose", clone.Theme);
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
