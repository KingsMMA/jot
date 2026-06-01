using Jot.Platform;
using Xunit;

namespace Jot.Tests;

public class StartupOptionsTests
{
    [Fact]
    public void Parse_FilePath_ResolvesToFullPathAndIsNotAgent()
    {
        var options = StartupOptions.Parse(["notes.md"]);
        Assert.False(options.IsAgent);
        Assert.False(options.OpenPreview);
        Assert.NotNull(options.Path);
        Assert.EndsWith("notes.md", options.Path);
        Assert.True(System.IO.Path.IsPathFullyQualified(options.Path!));
    }

    [Fact]
    public void Parse_Agent_SetsAgentWithNoPath()
    {
        var options = StartupOptions.Parse(["--agent"]);
        Assert.True(options.IsAgent);
        Assert.Null(options.Path);
    }

    [Fact]
    public void Parse_PreviewFlag_WithPath()
    {
        var options = StartupOptions.Parse(["doc.md", "--preview"]);
        Assert.True(options.OpenPreview);
        Assert.EndsWith("doc.md", options.Path);
    }

    [Fact]
    public void Parse_NoArgs_HasNoPath()
    {
        var options = StartupOptions.Parse([]);
        Assert.Null(options.Path);
        Assert.False(options.IsAgent);
    }

    [Fact]
    public void ToMessage_WithPath_IsOpenMessage()
    {
        var options = new StartupOptions { Path = @"C:\x\y.txt" };
        Assert.Equal("OPEN\tC:\\x\\y.txt", options.ToMessage());
    }

    [Fact]
    public void ToMessage_WithPreview_AppendsFlag()
    {
        var options = new StartupOptions { Path = @"C:\x\y.md", OpenPreview = true };
        Assert.Equal("OPEN\tC:\\x\\y.md\tPREVIEW", options.ToMessage());
    }

    [Fact]
    public void ToMessage_NoPath_IsShowMessage()
    {
        Assert.Equal("SHOW", new StartupOptions().ToMessage());
    }
}
