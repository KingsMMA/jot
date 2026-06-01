using System.Text.RegularExpressions;

namespace Jot.Search;

/// <summary>A search request: the pattern plus the toggles that shape how it is matched.</summary>
public readonly record struct SearchQuery(string Pattern, bool MatchCase, bool WholeWord, bool UseRegex);

/// <summary>A located match as an offset and length into the searched text.</summary>
public readonly record struct SearchMatch(int Offset, int Length);

/// <summary>
/// Pure find-and-replace logic, independent of the editor control so it can be unit tested.
/// Literal searches are escaped to a regex; regex searches are used verbatim. An invalid regex
/// yields no matches rather than throwing, so a half-typed pattern never crashes the editor.
/// </summary>
public static class SearchEngine
{
    public static Regex? BuildRegex(SearchQuery query)
    {
        if (string.IsNullOrEmpty(query.Pattern)) return null;

        var options = RegexOptions.Multiline | RegexOptions.CultureInvariant;
        if (!query.MatchCase) options |= RegexOptions.IgnoreCase;

        var pattern = query.UseRegex ? query.Pattern : Regex.Escape(query.Pattern);
        if (query.WholeWord) pattern = $@"\b(?:{pattern})\b";

        try { return new Regex(pattern, options); }
        catch (ArgumentException) { return null; }
    }

    public static SearchMatch? FindNext(string text, SearchQuery query, int startOffset, bool wrap = true)
    {
        var regex = BuildRegex(query);
        if (regex is null) return null;

        if (startOffset < 0) startOffset = 0;
        if (startOffset <= text.Length)
        {
            var match = regex.Match(text, startOffset);
            if (match.Success) return new SearchMatch(match.Index, match.Length);
        }

        if (wrap)
        {
            var match = regex.Match(text, 0);
            if (match.Success && match.Index < startOffset) return new SearchMatch(match.Index, match.Length);
        }

        return null;
    }

    public static SearchMatch? FindPrevious(string text, SearchQuery query, int beforeOffset, bool wrap = true)
    {
        var regex = BuildRegex(query);
        if (regex is null) return null;

        SearchMatch? last = null;
        foreach (Match m in regex.Matches(text))
        {
            if (m.Index < beforeOffset) last = new SearchMatch(m.Index, m.Length);
            else break;
        }

        if (last is null && wrap)
        {
            Match? lastOverall = null;
            foreach (Match m in regex.Matches(text)) lastOverall = m;
            if (lastOverall is not null) last = new SearchMatch(lastOverall.Index, lastOverall.Length);
        }

        return last;
    }

    public static List<SearchMatch> FindAll(string text, SearchQuery query)
    {
        var results = new List<SearchMatch>();
        var regex = BuildRegex(query);
        if (regex is null) return results;
        foreach (Match m in regex.Matches(text)) results.Add(new SearchMatch(m.Index, m.Length));
        return results;
    }

    public static (string result, int count) ReplaceAll(string text, SearchQuery query, string replacement)
    {
        var regex = BuildRegex(query);
        if (regex is null) return (text, 0);

        var count = 0;
        var result = regex.Replace(text, match =>
        {
            count++;
            // In regex mode honour substitutions like $1; in literal mode insert the text as-is.
            return query.UseRegex ? match.Result(replacement) : replacement;
        });
        return (result, count);
    }
}
