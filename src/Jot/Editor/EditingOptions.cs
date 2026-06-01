namespace Jot.Editor;

/// <summary>
/// Editing behaviour that mirrors the user's configuration. A single mutable instance is shared
/// with the editor helpers so a configuration change takes effect without rebuilding them.
/// </summary>
public sealed class EditingOptions
{
    /// <summary>The string inserted for one level of indentation (spaces or a tab).</summary>
    public string IndentUnit { get; set; } = "    ";

    public bool AutoCloseBrackets { get; set; } = true;
}
