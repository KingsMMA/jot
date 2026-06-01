using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jot.Config;

/// <summary>
/// Loads and saves the system-wide configuration and the small amount of session state
/// (the last opened file and window size). Both live under <c>%APPDATA%\Jot</c>.
/// </summary>
public static class ConfigStore
{
    public static string Directory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Jot");

    public static string ConfigPath => Path.Combine(Directory, "config.json");
    public static string StatePath => Path.Combine(Directory, "state.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize(JotConfig config) => JsonSerializer.Serialize(config, Options);

    public static JotConfig Deserialize(string json) =>
        JsonSerializer.Deserialize<JotConfig>(json, Options) ?? new JotConfig();

    public static JotConfig LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return Deserialize(File.ReadAllText(ConfigPath));
        }
        catch
        {
            // A malformed config should never stop the editor from opening.
        }
        return new JotConfig();
    }

    /// <summary>Loads the config, writing a default file the first time so it is discoverable.</summary>
    public static JotConfig LoadOrCreateConfig()
    {
        if (File.Exists(ConfigPath)) return LoadConfig();
        var config = new JotConfig();
        SaveConfig(config);
        return config;
    }

    public static void SaveConfig(JotConfig config)
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(ConfigPath, Serialize(config));
    }

    public static JotState LoadState()
    {
        try
        {
            if (File.Exists(StatePath))
                return JsonSerializer.Deserialize<JotState>(File.ReadAllText(StatePath), Options) ?? new JotState();
        }
        catch
        {
            // Ignore unreadable state; it is non-essential.
        }
        return new JotState();
    }

    public static void SaveState(JotState state)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(state, Options));
        }
        catch
        {
            // State is a convenience; never surface a failure to write it.
        }
    }
}

/// <summary>Non-essential session state restored on the next launch.</summary>
public sealed class JotState
{
    public string? LastFile { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
}
