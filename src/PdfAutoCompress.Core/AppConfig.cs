using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfAutoCompress.Core;

/// <summary>
/// User settings. Saved to a per-user file under %APPDATA% so the ap can from a read-only location.
/// </summary>
public class AppConfig
{
    /// <summary>The folders to watch for PDF files.</summary>
    public List<string> WatchedFolders { get; set; } = [];

    /// <summary>The path to the Ghostscript executable.</summary>
    public string GhostscriptLocation { get; set; } = "";

    /// <summary>When true, the original PDF is kept alongside the compressed copy.</summary>
    public bool KeepOriginal { get; set; }

    /// <summary>Minimum file size in bytes to consider for compression. 0 means no minimum.</summary>
    public long MinSizeBytes { get; set; } = 1024 * 1024;

    /// <summary>Determines the PDF compression settings. /ebook is the recommended compression.</summary>
    public string PdfSettings { get; set; } = "/ebook";

    /// <summary>Per-user settings file: %APPDATA%\PdfAutoCompress\appsettings.json</summary>
    [JsonIgnore]
    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PdfAutoCompress",
        "appsettings.json"
    );

    /// <summary>Default settings file shipped next to the executable.</summary>
    [JsonIgnore]
    private static string DefaultConfigPath => Path.Combine(
        AppContext.BaseDirectory,
        "appsettings.json"
    );

    private static readonly JsonSerializerOptions s_read = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions s_write = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static AppConfig Load()
    {
        var availablePaths = new[] { ConfigPath, DefaultConfigPath };
        foreach (string path in availablePaths)
        {
            AppConfig? loaded = LoadFrom(path);
            if (loaded is not null) return loaded;
        }
        return new AppConfig();
    }

    /// <summary>Reads a config from a specific file, or null if it's missing/unreadable</summary>
    internal static AppConfig? LoadFrom(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                string configAsJson = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppConfig>(configAsJson, s_read) ?? new AppConfig();
            }
        }
        catch
        {
            // Ignore JSON deserialization errors
        }
        return null;
    }

    public void Save() => SaveTo(ConfigPath);

    /// <summary>Writes the configuration to a file, creating a directory if needed.</summary>
    internal void SaveTo(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string configAsJson = JsonSerializer.Serialize(this, s_write);
        File.WriteAllText(path, configAsJson);
    }
}