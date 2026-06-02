using System.IO;

namespace Jot.Platform;

/// <summary>
/// The parsed command line. Jot runs as a single instance: a normal launch either becomes that
/// instance or hands its request to the running one over a pipe; <c>--agent</c> starts the warm,
/// hidden background instance that owns the global hotkey.
/// </summary>
public sealed class StartupOptions
{
    public string? Path { get; init; }
    public bool IsAgent { get; init; }
    public bool OpenPreview { get; init; }

    /// <summary>True for <c>--quit</c>: ask the running instance to exit, then do nothing else.</summary>
    public bool IsQuit { get; init; }

    /// <summary>True for <c>--settings</c>: open the settings editor.</summary>
    public bool OpenSettings { get; init; }

    public static StartupOptions Parse(string[] args)
    {
        string? path = null;
        var isAgent = false;
        var openPreview = false;
        var isQuit = false;
        var openSettings = false;

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--agent":
                    isAgent = true;
                    break;
                case "--preview":
                    openPreview = true;
                    break;
                case "--quit":
                    isQuit = true;
                    break;
                case "--settings":
                    openSettings = true;
                    break;
                default:
                    if (!arg.StartsWith('-') && path is null)
                        path = ResolvePath(arg);
                    break;
            }
        }

        return new StartupOptions
        {
            Path = path,
            IsAgent = isAgent,
            OpenPreview = openPreview,
            IsQuit = isQuit,
            OpenSettings = openSettings,
        };
    }

    private static string? ResolvePath(string arg)
    {
        try { return System.IO.Path.GetFullPath(arg); }
        catch { return arg; }
    }

    /// <summary>The message a secondary launch sends to the running instance.</summary>
    public string ToMessage() => Path is null
        ? IpcMessages.Show
        : $"{IpcMessages.Open}\t{Path}{(OpenPreview ? "\t" + IpcMessages.PreviewFlag : string.Empty)}";
}

public static class IpcMessages
{
    public const string Open = "OPEN";
    public const string Show = "SHOW";
    public const string Quit = "QUIT";
    public const string PreviewFlag = "PREVIEW";
}
