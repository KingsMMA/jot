using Markdig;

namespace Jot.Markdown;

/// <summary>
/// Renders Markdown to HTML using a GitHub-flavoured pipeline (tables, task lists, auto-links,
/// strikethrough, and the like) and wraps it in a dark, GitHub-style page. The page exposes a
/// <c>#content</c> element so the body can be swapped on each edit without reloading.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UsePipeTables()
        .UseGridTables()
        .UseTaskLists()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .UseFootnotes()
        .Build();

    public static string ToBodyHtml(string markdown) =>
        Markdig.Markdown.ToHtml(markdown ?? string.Empty, Pipeline);

    /// <summary>The full page shell with an empty content element, loaded once into the web view.</summary>
    public static string Shell() =>
        $"<!doctype html><html><head><meta charset=\"utf-8\">" +
        $"<meta name=\"color-scheme\" content=\"dark\"><style>{Css}</style></head>" +
        $"<body><article id=\"content\" class=\"markdown-body\"></article></body></html>";

    public const string Css = """
        :root { color-scheme: dark; }
        body {
            margin: 0;
            background: #0d1117;
            color: #e6edf3;
            font-family: -apple-system, "Segoe UI", Helvetica, Arial, sans-serif;
            font-size: 16px;
            line-height: 1.6;
        }
        .markdown-body {
            box-sizing: border-box;
            max-width: 900px;
            margin: 0 auto;
            padding: 32px 40px 64px;
            word-wrap: break-word;
        }
        .markdown-body > *:first-child { margin-top: 0; }
        h1, h2, h3, h4, h5, h6 { margin: 24px 0 16px; font-weight: 600; line-height: 1.25; }
        h1 { font-size: 2em; padding-bottom: .3em; border-bottom: 1px solid #21262d; }
        h2 { font-size: 1.5em; padding-bottom: .3em; border-bottom: 1px solid #21262d; }
        h3 { font-size: 1.25em; }
        h4 { font-size: 1em; }
        h5 { font-size: .875em; }
        h6 { font-size: .85em; color: #8b949e; }
        p, blockquote, ul, ol, dl, table, pre { margin: 0 0 16px; }
        a { color: #4493f8; text-decoration: none; }
        a:hover { text-decoration: underline; }
        code {
            font-family: "Cascadia Code", "SF Mono", Consolas, monospace;
            font-size: 85%;
            padding: .2em .4em;
            margin: 0;
            background: rgba(110,118,129,0.4);
            border-radius: 6px;
        }
        pre {
            padding: 16px;
            overflow: auto;
            font-size: 85%;
            line-height: 1.45;
            background: #161b22;
            border-radius: 6px;
        }
        pre code { padding: 0; margin: 0; background: transparent; font-size: 100%; }
        blockquote {
            padding: 0 1em;
            color: #8b949e;
            border-left: .25em solid #30363d;
        }
        table { border-collapse: collapse; display: block; width: max-content; max-width: 100%; overflow: auto; }
        table th, table td { padding: 6px 13px; border: 1px solid #30363d; }
        table tr { background: #0d1117; border-top: 1px solid #21262d; }
        table tr:nth-child(2n) { background: #161b22; }
        img { max-width: 100%; background: #0d1117; }
        hr { height: .25em; padding: 0; margin: 24px 0; background: #30363d; border: 0; }
        ul, ol { padding-left: 2em; }
        li + li { margin-top: .25em; }
        .task-list-item { list-style-type: none; }
        .task-list-item-checkbox { margin: 0 .35em .25em -1.4em; vertical-align: middle; }
        kbd {
            padding: 3px 5px;
            font-size: 11px;
            line-height: 10px;
            color: #e6edf3;
            background: #161b22;
            border: 1px solid #30363d;
            border-radius: 6px;
            box-shadow: inset 0 -1px 0 #30363d;
        }
        """;
}
