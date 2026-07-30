namespace PdfAutoCompress.Core;

/// <summary>
/// User settings. Saved to a per-user file under %APPDATA% so the ap can run
/// from a read-only location.
/// </summary>
public class AppConfig
{
    /// <summary>When true, the original PDF is kept alongside the compressed copy.</summary>
    public bool KeepOriginal { get; set; }

    /// <summary>Minimum file size in bytes to consider for compression. 0 means no minimum.</summary>
    public long MinSizeBytes { get; set; } = 1024 * 1024;

    /// <summary>Determines the PDF compression settings. /ebook is the recommended compression.</summary>
    public string PdfSettings { get; set; } = "/ebook";
}