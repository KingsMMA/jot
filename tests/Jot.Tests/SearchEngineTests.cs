using Jot.Search;
using Xunit;

namespace Jot.Tests;

public class SearchEngineTests
{
    private static SearchQuery Literal(string pattern, bool matchCase = false, bool wholeWord = false) =>
        new(pattern, matchCase, wholeWord, UseRegex: false);

    [Fact]
    public void FindNext_Literal_CaseInsensitiveByDefault()
    {
        var m = SearchEngine.FindNext("Hello hello", Literal("hello"), 0);
        Assert.Equal(new SearchMatch(0, 5), m);
    }

    [Fact]
    public void FindNext_MatchCase_SkipsWrongCase()
    {
        var m = SearchEngine.FindNext("Hello hello", Literal("hello", matchCase: true), 0);
        Assert.Equal(new SearchMatch(6, 5), m);
    }

    [Fact]
    public void FindNext_WrapsAround()
    {
        var m = SearchEngine.FindNext("abc abc", Literal("abc"), 5, wrap: true);
        Assert.Equal(new SearchMatch(0, 3), m);
    }

    [Fact]
    public void FindNext_NoWrap_ReturnsNullPastLastMatch()
    {
        var m = SearchEngine.FindNext("abc abc", Literal("abc"), 5, wrap: false);
        Assert.Null(m);
    }

    [Fact]
    public void FindPrevious_ReturnsMatchBeforeOffset()
    {
        var m = SearchEngine.FindPrevious("abc abc abc", Literal("abc"), beforeOffset: 8);
        Assert.Equal(new SearchMatch(4, 3), m);
    }

    [Fact]
    public void WholeWord_DoesNotMatchSubstring()
    {
        Assert.Null(SearchEngine.FindNext("category", Literal("cat", wholeWord: true), 0));
        Assert.Equal(new SearchMatch(0, 3), SearchEngine.FindNext("cat nap", Literal("cat", wholeWord: true), 0));
    }

    [Fact]
    public void Regex_FindsPattern()
    {
        var q = new SearchQuery(@"\d+", MatchCase: false, WholeWord: false, UseRegex: true);
        Assert.Equal(new SearchMatch(3, 2), SearchEngine.FindNext("abc42def", q, 0));
    }

    [Fact]
    public void Regex_Invalid_YieldsNoMatch()
    {
        var q = new SearchQuery("(unclosed", MatchCase: false, WholeWord: false, UseRegex: true);
        Assert.Null(SearchEngine.BuildRegex(q));
        Assert.Null(SearchEngine.FindNext("text", q, 0));
    }

    [Fact]
    public void ReplaceAll_Literal_TreatsDollarLiterally()
    {
        var (result, count) = SearchEngine.ReplaceAll("a a a", Literal("a"), "$1");
        Assert.Equal("$1 $1 $1", result);
        Assert.Equal(3, count);
    }

    [Fact]
    public void ReplaceAll_Regex_ExpandsGroups()
    {
        var q = new SearchQuery(@"(\w+)@(\w+)", MatchCase: false, WholeWord: false, UseRegex: true);
        var (result, count) = SearchEngine.ReplaceAll("send to bob@acme now", q, "$2.$1");
        Assert.Equal("send to acme.bob now", result);
        Assert.Equal(1, count);
    }

    [Fact]
    public void FindAll_CountsMatches()
    {
        Assert.Equal(3, SearchEngine.FindAll("a.a.a", Literal("a")).Count);
    }
}
