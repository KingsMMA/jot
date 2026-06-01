using Jot.Markdown;
using Xunit;

namespace Jot.Tests;

public class MarkdownRendererTests
{
    [Fact]
    public void Headings_RenderAsHeadingTags()
    {
        Assert.Contains("<h1", MarkdownRenderer.ToBodyHtml("# Title"));
    }

    [Fact]
    public void Bold_RendersAsStrong()
    {
        Assert.Contains("<strong>bold</strong>", MarkdownRenderer.ToBodyHtml("**bold**"));
    }

    [Fact]
    public void Tables_AreSupported()
    {
        var md = "| a | b |\n| - | - |\n| 1 | 2 |";
        Assert.Contains("<table>", MarkdownRenderer.ToBodyHtml(md));
    }

    [Fact]
    public void TaskLists_RenderCheckboxes()
    {
        Assert.Contains("type=\"checkbox\"", MarkdownRenderer.ToBodyHtml("- [x] done"));
    }

    [Fact]
    public void FencedCode_RendersPreCode()
    {
        var html = MarkdownRenderer.ToBodyHtml("```\ncode\n```");
        Assert.Contains("<pre>", html);
        Assert.Contains("<code", html);
    }

    [Fact]
    public void Shell_ContainsContentElementAndStyle()
    {
        var shell = MarkdownRenderer.Shell(Jot.Theming.Themes.Get("dark").Markdown());
        Assert.Contains("id=\"content\"", shell);
        Assert.Contains("markdown-body", shell);
        Assert.Contains("background: #0d1117", shell);
    }

    [Fact]
    public void ToBodyHtml_NullInput_DoesNotThrow()
    {
        Assert.Equal(string.Empty, MarkdownRenderer.ToBodyHtml(null!).Trim());
    }
}
