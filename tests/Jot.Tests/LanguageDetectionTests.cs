using Jot.Editor;
using Xunit;

namespace Jot.Tests;

public class LanguageDetectionTests
{
    private readonly LanguageService _svc = new();

    [Theory]
    [InlineData("config.json", "json")]
    [InlineData("notes.md", "markdown")]
    [InlineData("data.yaml", "yaml")]
    [InlineData("data.yml", "yaml")]
    [InlineData("script.py", "python")]
    [InlineData("app.ts", "typescript")]
    [InlineData("app.js", "javascript")]
    [InlineData("Program.cs", "csharp")]
    [InlineData("main.go", "go")]
    [InlineData("style.css", "css")]
    [InlineData("page.html", "html")]
    public void Detect_ByExtension(string fileName, string expectedId)
    {
        Assert.Equal(expectedId, _svc.Detect(fileName, ""));
    }

    [Theory]
    [InlineData("Dockerfile", "dockerfile")]
    [InlineData("Makefile", "makefile")]
    [InlineData("CMakeLists.txt", "cmake")]
    public void Detect_BySpecialName(string fileName, string expectedId)
    {
        Assert.Equal(expectedId, _svc.Detect(fileName, ""));
    }

    [Theory]
    [InlineData("#!/usr/bin/env python\nprint('hi')", "python")]
    [InlineData("#!/bin/bash\necho hi", "shellscript")]
    [InlineData("#!/usr/bin/node\nconsole.log(1)", "javascript")]
    public void Detect_ByShebang_WhenNoExtension(string content, string expectedId)
    {
        Assert.Equal(expectedId, _svc.Detect(null, content));
    }

    [Theory]
    [InlineData("<?xml version=\"1.0\"?><root/>", "xml")]
    [InlineData("{\n  \"a\": 1\n}", "json")]
    [InlineData("[1, 2, 3]", "json")]
    [InlineData("---\nkey: value\n", "yaml")]
    public void Detect_ByContent_WhenUnknownExtension(string content, string expectedId)
    {
        Assert.Equal(expectedId, _svc.Detect("mystery.unknownext", content));
    }

    [Fact]
    public void Detect_UnknownContent_FallsBackToPlainText()
    {
        Assert.Equal(LanguageService.PlainText, _svc.Detect("notes.unknownext", "just some prose"));
    }

    [Fact]
    public void Extension_TakesPrecedenceOverContent()
    {
        // A .json extension wins even if the body looks like a shebang script.
        Assert.Equal("json", _svc.Detect("data.json", "#!/bin/bash"));
    }
}
