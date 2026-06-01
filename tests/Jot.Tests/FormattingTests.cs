using Jot.Config;
using Jot.Formatting;
using Xunit;

namespace Jot.Tests;

public class FormattingTests
{
    [Fact]
    public void Json_PrettyPrints_WithFourSpaceIndent()
    {
        var formatted = JsonFormatter.Format("{\"a\":1,\"b\":[1,2]}", "    ");
        Assert.Equal("{\n    \"a\": 1,\n    \"b\": [\n        1,\n        2\n    ]\n}", formatted);
    }

    [Fact]
    public void Json_EmptyContainers_StayCompact()
    {
        Assert.Equal("{\n    \"a\": {},\n    \"b\": []\n}", JsonFormatter.Format("{\"a\":{},\"b\":[]}", "    "));
    }

    [Fact]
    public void Json_PreservesNumberRepresentation()
    {
        Assert.Equal("[\n  1.50,\n  2e3\n]", JsonFormatter.Format("[1.50,2e3]", "  "));
    }

    [Fact]
    public void Json_ToleratesCommentsAndTrailingCommas()
    {
        var formatted = JsonFormatter.Format("{\n // comment\n \"a\": 1, \n}", "  ");
        Assert.Equal("{\n  \"a\": 1\n}", formatted);
    }

    [Fact]
    public void DocumentFormatter_InvalidJson_LeavesTextUnchanged()
    {
        var config = new JotConfig();
        var result = DocumentFormatter.Format("json", "{not valid", config);
        Assert.False(result.Changed);
        Assert.Equal("{not valid", result.Text);
        Assert.Contains("Invalid JSON", result.Message);
    }

    [Fact]
    public void Xml_Reindents()
    {
        var config = new JotConfig();
        var result = DocumentFormatter.Format("xml", "<root><child>x</child></root>", config);
        Assert.True(result.Changed);
        Assert.Contains("<root>\n    <child>x</child>\n</root>", result.Text);
    }

    [Fact]
    public void Whitespace_TrimsTrailing_AndEnsuresFinalNewline()
    {
        var config = new JotConfig { TrimTrailingWhitespace = true, InsertFinalNewline = true };
        var result = WhitespaceNormaliser.Normalise("a   \nb\t", config, "    ");
        Assert.Equal("a\nb\n", result);
    }

    [Fact]
    public void Whitespace_ConvertsLeadingTabsToSpaces()
    {
        var config = new JotConfig { InsertSpaces = true, IndentSize = 4, InsertFinalNewline = false };
        var result = WhitespaceNormaliser.Normalise("\t\tx", config, "    ");
        Assert.Equal("        x", result);
    }

    [Fact]
    public void IndentUnitFor_RespectsLanguageOverride()
    {
        var config = new JotConfig { IndentSize = 4, InsertSpaces = true };
        config.LanguageOverrides["go"] = new LanguageOverride { InsertSpaces = false };
        Assert.Equal("    ", config.IndentUnitFor("json"));
        Assert.Equal("\t", config.IndentUnitFor("go"));
    }
}
