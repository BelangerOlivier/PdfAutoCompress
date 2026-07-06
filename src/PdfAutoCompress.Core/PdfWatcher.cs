using System.Collections.Concurrent;

namespace PdfAutoCompress.Core;

public readonly record struct CompressResult(
    string File, long OriginalBytes, long NewBytes, double SavedPercent);

/// <summary>
/// The engine: watches a folder and compresses newly-arrived PDFs in place with Ghostscript.
/// UI-agnostic and cross-platform, every front-end (tray, service, CLI) drives this class.
/// Restartable: call <see cref="Stop"/> then <see cref="Start"/> after settings change.
/// </summary>
public sealed class PdfWatcher : IDisposable
{
    private const string CompressedSuffix = PdfCompressor.CompressedSuffix;

    private readonly ConcurrentDictionary<string, DateTime> _busy =
        new(StringComparer.OrdinalIgnoreCase);

    private FileSystemWatcher? _watcher;
    private AppConfig _config = new();

    /// <summary>Raised after a PDF is successfully shrunk.</summary>
    public event Action<CompressResult>? Compressed;

    public string ResolvedGhostscript { get; private set; } = "";
    public string WatchFolder { get; private set; } = "";
    public bool IsRunning => _watcher is { EnableRaisingEvents: true };
    public CompressionLog Logger { get; set; } = new();

    /// <summary>Starts watching. Returns null on success, or an error message.</summary>
    public string? Start(AppConfig config)
    {
        Stop();
        _config = config;

        WatchFolder = string.IsNullOrWhiteSpace(config.WatchFolder)
            ? AppConfig.DefaultDownloadsFolder()
            : Environment.ExpandEnvironmentVariables(config.WatchFolder);

        if (!Directory.Exists(WatchFolder))
            return $"Watch folder does not exist:\n{WatchFolder}";

        ResolvedGhostscript = GhostscriptChecker.ResolveGhostscript(config.GhostscriptPath);
        if (ResolvedGhostscript.Length == 0)
            return "Ghostscript was not found.\n\nInstall it from https://ghostscript.com/releases/gsdnld.html, or set its path in settings.";

        StartWatcher();
        Logger.Emit($"Watching {WatchFolder} (Ghostscript: {ResolvedGhostscript})");
        return null;
    }

    private void StartWatcher()
    {
        _watcher = new FileSystemWatcher(WatchFolder, "*.pdf")
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
        };
        _watcher.Created += OnCreatedOrRenamed;
        _watcher.Renamed += OnCreatedOrRenamed;
        _watcher.Error += (_, e) => Logger.Emit($"Watcher error: {e.GetException().Message}");
        _watcher.EnableRaisingEvents = true;
    }

    public void Stop()
    {
        if (_watcher is null) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnCreatedOrRenamed;
        _watcher.Renamed -= OnCreatedOrRenamed;
        _watcher.Dispose();
        _watcher = null;
        _busy.Clear();
    }

    public void Dispose() => Stop();

    private void OnCreatedOrRenamed(object sender, FileSystemEventArgs e) =>
        _ = HandleAsync(e.FullPath);

    private async Task HandleAsync(string path)
    {
        try
        {
            if (IsFileCompressed(path) || IsFileHandled(path))
                return;

            try
            {
                await ProcessAsync(path);
            }
            finally
            {
                _busy[path] = DateTime.UtcNow;
                _ = ForgetLater(path);
            }
        }
        catch (Exception ex)
        {
            Logger.Emit($"Error handling {Path.GetFileName(path)}: {ex.Message}");
        }
    }

    internal static bool IsFileCompressed(string path)
    {
        string filename = Path.GetFileName(path);
        return filename.EndsWith(CompressedSuffix, StringComparison.OrdinalIgnoreCase);
    }

    internal bool IsFileHandled(string path) => !_busy.TryAdd(path, DateTime.UtcNow);

    private async Task ForgetLater(string path)
    {
        await Task.Delay(TimeSpan.FromSeconds(30));
        _busy.TryRemove(path, out _);
    }

    private async Task ProcessAsync(string src)
    {
        if (!await WaitUntilReadyAsync(src))
            return;

        if (PdfCompressor.IsAlreadyCompressed(src))
        {
            Logger.Emit($"Skipped {Path.GetFileName(src)}: already compressed.");
            return;
        }

        long origSize = new FileInfo(src).Length;
        if (_config.MinSizeBytes > 0 && origSize < _config.MinSizeBytes)
            return;

        Logger.Emit($"Compressing {Path.GetFileName(src)} ({Format(origSize)})…");

        // The compressor writes to <src>.gstmp then moves it onto the destination; pre-mark
        // the destination as busy so we ignore the file event that move produces.
        string dest = PdfCompressor.DestinationFor(src, _config);
        _busy[dest] = DateTime.UtcNow;

        try
        {
            CompressResult? result = await PdfCompressor.CompressFileAsync(
                src, _config, ResolvedGhostscript);
            if (result is { } r)
            {
                Logger.Emit($"Compressed {Path.GetFileName(r.File)}: " +
                     $"{Format(r.OriginalBytes)} → {Format(r.NewBytes)} ({r.SavedPercent:F1}% saved)");
                Compressed?.Invoke(r);
            }
            else
            {
                Logger.Emit($"Skipped {Path.GetFileName(src)}: no size gain.");
            }
        }
        catch (GhostscriptException gx)
        {
            string detail = gx.Stderr.Length > 300 ? gx.Stderr[..300] : gx.Stderr;
            Logger.Emit($"Ghostscript failed (exit {gx.ExitCode}). {detail}");
        }
    }

    private static async Task<bool> WaitUntilReadyAsync(string path)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(5);
        while (DateTime.UtcNow < deadline)
        {
            if (!File.Exists(path))
            {
                await Task.Delay(300);
                continue;
            }
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                if (fs.Length > 0)
                    return true;
            }
            catch (IOException) { /* still being written */ }
            catch (UnauthorizedAccessException) { /* is locked */ }
            await Task.Delay(500);
        }
        return false;
    }

    internal static string Format(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double v = bytes;
        int u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return u == 0 ? $"{bytes} B" : $"{v:0.#} {units[u]}";
    }
}
