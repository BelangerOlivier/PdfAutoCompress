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

    public string WatchFolder { get; set; } = "";
    public string GhostscriptPath { get; set; } = "";
    public string PdfSettings { get; set; } = "/ebook";
    public long MinSizeBytes { get; set; } = 1024 * 1024;
    public bool KeepOriginal { get; set; }

    /// <summary>Show a tray balloon when a PDF is compressed (tray app only).</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Check GitHub for a newer release on startup.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>When started at login, wait this long before watching (lazy start).</summary>
    public int StartupDelaySeconds { get; set; } = 20;

    /// <summary>True once the tray app has offered to install itself (so we ask only once).</summary>
    public bool SetupPromptShown { get; set; }

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
                // Try the next candidate on any parse/IO error.
            }
        }
        return new AppConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        string json = JsonSerializer.Serialize(this, s_write);
        File.WriteAllText(ConfigPath, json);
    }

    public AppConfig Clone() => (AppConfig)MemberwiseClone();
}
