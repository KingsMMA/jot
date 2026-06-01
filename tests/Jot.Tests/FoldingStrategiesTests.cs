using AvaloniaEdit.Document;
using Jot.Editor;
using Xunit;

namespace Jot.Tests;

public class FoldingStrategiesTests
{
    private static TextDocument Doc(string text) => new(text);

    [Fact]
    public void BraceFoldings_FoldsMultiLineBlock()
    {
        var foldings = FoldingStrategies.BraceFoldings(Doc("{\n  \"a\": 1\n}"));
        Assert.Single(foldings);
        Assert.Equal(0, foldings[0].StartOffset);
    }

    [Fact]
    public void BraceFoldings_IgnoresSingleLineBlock()
    {
        Assert.Empty(FoldingStrategies.BraceFoldings(Doc("{ \"a\": 1 }")));
    }

    [Fact]
    public void BraceFoldings_IgnoresBracesInsideStrings()
    {
        // The brace inside the string must not start a fold; only the real pair folds.
        var foldings = FoldingStrategies.BraceFoldings(Doc("x = \"{ not real\"\n{\n}\n"));
        Assert.Single(foldings);
    }

    [Fact]
    public void BraceFoldings_IgnoresBracesInLineComments()
    {
        var foldings = FoldingStrategies.BraceFoldings(Doc("// {\n{\n}\n"));
        Assert.Single(foldings);
    }

    [Fact]
    public void BraceFoldings_HandlesNestedBlocks()
    {
        var foldings = FoldingStrategies.BraceFoldings(Doc("{\n  [\n    1\n  ]\n}"));
        Assert.Equal(2, foldings.Count);
    }

    [Fact]
    public void IndentFoldings_FoldsIndentedBlock()
    {
        var yaml = "parent:\n  child1: 1\n  child2: 2\nother: 3\n";
        var foldings = FoldingStrategies.IndentFoldings(Doc(yaml));
        Assert.NotEmpty(foldings);
    }

    [Fact]
    public void IndentFoldings_FlatDocument_HasNoFoldings()
    {
        Assert.Empty(FoldingStrategies.IndentFoldings(Doc("a: 1\nb: 2\nc: 3\n")));
    }
}
