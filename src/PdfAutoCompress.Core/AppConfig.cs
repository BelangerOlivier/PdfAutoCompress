using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfAutoCompress.Core;

/// <summary>
/// User settings, loaded from and saved to appsettings.json next to the executable.
/// Shared by every front-end (tray app, Windows service, CLI).
/// </summary>
public sealed class AppConfig
{
    public string WatchFolder { get; set; } = "";
    public string GhostscriptPath { get; set; } = "";
    public string PdfSettings { get; set; } = "/ebook";
    public long MinSizeBytes { get; set; } = 1024 * 1024;
    public bool KeepOriginal { get; set; }

    /// <summary>Show a tray balloon when a PDF is compressed (tray app only).</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Check GitHub for a newer release on startup.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>GitHub repository as "owner/name" (e.g. "belangerolivier/PdfAutoCompress").</summary>
    public string UpdateRepo { get; set; } = "";

    /// <summary>When started at login, wait this long before watching (lazy start).</summary>
    public int StartupDelaySeconds { get; set; } = 20;

    [JsonIgnore]
    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

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
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json, s_read) ?? new AppConfig();
            }
        }
        catch
        {
            // Fall through to defaults on any parse/IO error.
        }
        return new AppConfig();
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(this, s_write);
        File.WriteAllText(ConfigPath, json);
    }

    public AppConfig Clone() => (AppConfig)MemberwiseClone();
}
