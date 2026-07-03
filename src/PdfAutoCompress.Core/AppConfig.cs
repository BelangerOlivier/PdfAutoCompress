using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfAutoCompress.Core;

/// <summary>
/// User settings. Saved to a per-user file under %APPDATA% so the app can run from a
/// read-only location (e.g. Program Files) and doesn't litter the folder it launched from.
/// Shared by every front-end (tray app, Windows service, CLI).
/// </summary>
public sealed class AppConfig
{
    public const string UpdateRepo = "BelangerOlivier/PdfAutoCompress";

    /// <summary>Determines the folder to watch for PDF files.</summary>
    public string WatchFolder { get; set; } = "";

    /// <summary>Determines the path to the Ghostscript executable.</summary>
    public string GhostscriptPath { get; set; } = "";

    /// <summary>Determines the PDF compression settings. /ebook is the recommended setting.</summary>
    public string PdfSettings { get; set; } = "/ebook";

    /// <summary>Minimum file size (in bytes) to consider for compression. 0 means no minimum.</summary>
    public long MinSizeBytes { get; set; } = 1024 * 1024;

    /// <summary>When true, the original PDF is kept alongside the compressed copy.</summary>
    public bool KeepOriginal { get; set; }

    /// <summary>Show a tray balloon when a PDF is compressed (tray app only).</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Check GitHub for a newer release on startup.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>When started at login, wait this long before watching (lazy start).</summary>
    public int StartupDelaySeconds { get; set; } = 20;

    public static string DefaultDownloadsFolder()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Downloads");
    }

    /// <summary>Per-user settings file: %APPDATA%\PdfAutoCompress\appsettings.json.</summary>
    [JsonIgnore]
    public static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PdfAutoCompress", "appsettings.json");

    /// <summary>Optional settings file shipped next to the executable (used as defaults).</summary>
    [JsonIgnore]
    private static string BundledPath => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    private static readonly JsonSerializerOptions s_read = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions s_write = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static AppConfig Load()
    {
        // Prefer the per-user file; fall back to a file bundled next to the exe; else defaults.
        foreach (string path in new[] { ConfigPath, BundledPath })
        {
            AppConfig? loaded = LoadFrom(path);
            if (loaded != null)
                return loaded;
        }
        return new AppConfig();
    }

    /// <summary>Reads a config from a specific file, or null if it's missing/unreadable.</summary>
    internal static AppConfig? LoadFrom(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppConfig>(json, s_read) ?? new AppConfig();
            }
        }
        catch
        {
            // Treat parse/IO errors as "not available" so callers can fall back.
        }
        return null;
    }

    public void Save() => SaveTo(ConfigPath);

    /// <summary>Writes this config to a specific file, creating the directory if needed.</summary>
    internal void SaveTo(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string json = JsonSerializer.Serialize(this, s_write);
        File.WriteAllText(path, json);
    }

    public AppConfig Clone() => (AppConfig)MemberwiseClone();
}
