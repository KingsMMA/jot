using Jot.Diagnostics;
using Xunit;

namespace Jot.Tests;

public class DiagnosticsTests
{
    [Fact]
    public void Json_Valid_HasNoProblems()
    {
        Assert.Empty(DiagnosticsAnalyzer.Analyze("json", "{ \"a\": 1, \"b\": [1, 2] }"));
    }

    [Fact]
    public void Json_Invalid_ReportsOneProblem()
    {
        var problems = DiagnosticsAnalyzer.Analyze("json", "{ \"a\": }");
        Assert.Single(problems);
        Assert.NotEmpty(problems[0].Message);
    }

    [Fact]
    public void Json_ProblemOffset_IsWithinTheText()
    {
        var text = "{\n  \"a\": 1\n  \"b\": 2\n}"; // missing comma after line 2
        var problems = DiagnosticsAnalyzer.Analyze("json", text);
        Assert.Single(problems);
        Assert.InRange(problems[0].Offset, 0, text.Length - 1);
        Assert.True(problems[0].Length >= 1);
    }

    [Fact]
    public void Json_CommentsAndTrailingCommas_AreAllowed()
    {
        Assert.Empty(DiagnosticsAnalyzer.Analyze("json", "{\n  // ok\n  \"a\": 1,\n}"));
    }

    [Fact]
    public void Yaml_Valid_HasNoProblems()
    {
        Assert.Empty(DiagnosticsAnalyzer.Analyze("yaml", "name: jot\nlist:\n  - a\n  - b\n"));
    }

    [Fact]
    public void Yaml_Invalid_ReportsAProblem()
    {
        var problems = DiagnosticsAnalyzer.Analyze("yaml", "foo: [1, 2, 3");
        Assert.NotEmpty(problems);
        Assert.NotEmpty(problems[0].Message);
    }

    [Fact]
    public void UnsupportedLanguage_HasNoProblems()
    {
        Assert.Empty(DiagnosticsAnalyzer.Analyze("python", "def f(: pass"));
    }

    [Fact]
    public void Empty_HasNoProblems()
    {
        Assert.Empty(DiagnosticsAnalyzer.Analyze("json", ""));
    }

    [Fact]
    public void Json_UnterminatedAtEnd_ReportsProblem()
    {
        var text = "{ \"a\": 1";
        var problems = DiagnosticsAnalyzer.Analyze("json", text);
        Assert.Single(problems);
        Assert.InRange(problems[0].Offset, 0, text.Length);
    }

    [Fact]
    public void OffsetFromLineByteColumn_MapsMultiByteToCharOffset()
    {
        // "é" is two UTF-8 bytes; a byte column of 2 should map to the character after it.
        Assert.Equal(1, DiagnosticsAnalyzer.OffsetFromLineByteColumn("éx", 0, 2));
        // Pure ASCII behaves like a plain column.
        Assert.Equal(3, DiagnosticsAnalyzer.OffsetFromLineByteColumn("abcd", 0, 3));
    }

    [Theory]
    [InlineData("abc\ndef\nghi", 0, 0, 0)]
    [InlineData("abc\ndef\nghi", 1, 0, 4)]
    [InlineData("abc\ndef\nghi", 2, 2, 10)]
    public void OffsetFromLineColumn_IsCorrect(string text, int line, int column, int expected)
    {
        Assert.Equal(expected, DiagnosticsAnalyzer.OffsetFromLineColumn(text, line, column));
    }
}
