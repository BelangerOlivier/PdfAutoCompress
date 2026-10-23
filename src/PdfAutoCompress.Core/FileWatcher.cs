using System.Collections.Concurrent;

using Serilog;

namespace PdfAutoCompress.Core;

public sealed class FileWatcher : IDisposable
{
    public List<string> WatchedFolders { get; private set; } = [];
    public event Action<CompressResult>? Compressed;

    private List<FileSystemWatcher>? _watchers;
    private AppConfig _config = new();
    private readonly ConcurrentDictionary<string, DateTime> _busy =
        new(StringComparer.OrdinalIgnoreCase);

    private void OnCreatedOrRenamed(object sender, FileSystemEventArgs e) =>
        _ = HandleAsync(e.FullPath);

    public string? Start(AppConfig config)
    {
        Stop();
        _config = config;
        _config.GhostscriptLocation = GhostscriptProcess.GetExecutablePath(_config.GhostscriptLocation);

        SetWatchedFolders();
        if (!WatchedFolders.TrueForAll(Directory.Exists))
        {
            string message = $"Watched folders do not exist: {string.Join(", ", WatchedFolders.Where(f => !Directory.Exists(f)))}";
            Log.Error(message);
            return message;
        }

        if (string.IsNullOrWhiteSpace(_config.GhostscriptLocation))
        {
            Log.Error("Ghostscript was not found");
            return $"Ghostscript was not found.\n\nInstall it from https://ghostscript.com/releases/gsdnld.html, or set its path in settings.";
        }

        StartWatcher();
        Log.Information("Watching {Folders} with Ghostscript at {Ghostscript}",
            string.Join(", ", WatchedFolders), _config.GhostscriptLocation);
        return null;
    }

    internal void SetWatchedFolders()
    {
        WatchedFolders = IsWatchedFoldersEmpty()
            ? [DownloadsFolder()]
            : _config.WatchedFolders;
    }

    internal bool IsWatchedFoldersEmpty() =>
        _config.WatchedFolders.Count == 0 || _config.WatchedFolders.TrueForAll(string.IsNullOrWhiteSpace);

    internal static string DownloadsFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    private void StartWatcher()
    {
        _watchers = [];
        foreach (var directory in WatchedFolders)
        {
            var watcher = new FileSystemWatcher(directory, "*.pdf")
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite
            };
            watcher.Created += OnCreatedOrRenamed;
            watcher.Renamed += OnCreatedOrRenamed;
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
    }

    private async Task HandleAsync(string path)
    {
        try
        {
            if (IsFileCompressed(path) || IsFileBeingProcessed(path))
                return;

            try
            {
                await CompressAsync(path);
            }
            finally
            {
                _busy[path] = DateTime.UtcNow;
                _ = ForgetLater(path);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to process {File}", path);
        }
    }

    internal static bool IsFileCompressed(string path)
    {
        string filename = Path.GetFileName(path);
        return filename.EndsWith(Constants.CompressedSuffix, StringComparison.OrdinalIgnoreCase);
    }

    internal bool IsFileBeingProcessed(string path) =>
        !_busy.TryAdd(path, DateTime.UtcNow);

    private async Task ForgetLater(string path)
    {
        await Task.Delay(TimeSpan.FromSeconds(30));
        _busy.TryRemove(path, out _);
    }

    private async Task CompressAsync(string source)
    {
        string name = Path.GetFileName(source);
        if (!await WaitUntilFileIsReadyAsync(source))
        {
            Log.Warning("Skipped {File}: file never became readable", name);
            return;
        }

        if (FileInspector.IsCompressed(source))
        {
            Log.Information("Skipped {File}: already compressed", name);
            return;
        }

        long originalSize = new FileInfo(source).Length;
        if (!GhostscriptCompressor.CanBeCompressed(originalSize, _config.MinSizeBytes))
        {
            Log.Information("Skipped {File}: {Size} is not above the minimum size of {Min}",
                name, FormatSize(originalSize), FormatSize(_config.MinSizeBytes));
            return;
        }

        Log.Information("Compressing {File} ({Size})", name, FormatSize(originalSize));

        string destination = GhostscriptCompressor.GetCompressedFileDestination(source, _config.KeepOriginal);
        _busy[destination] = DateTime.UtcNow;

        try
        {
            CompressResult? result = await GhostscriptCompressor.CompressFileAsync(
                _config.GhostscriptLocation, source, _config);
            if (result is { } r)
            {
                Log.Information("Compressed {File}: {Original} -> {New} ({Saved:F1}% saved)",
                    Path.GetFileName(r.File), FormatSize(r.OriginalSize), FormatSize(r.NewSize),
                    100 - (100.0 * r.NewSize / r.OriginalSize));
                Compressed?.Invoke(r);
            }
        }
        catch (GhostscriptException gex)
        {
            string detail = gex.Stderr.Length > 300 ? gex.Stderr[..300] : gex.Stderr;
            Log.Error(gex, "Ghostscript failed on {File}: {Detail}", name, detail);
        }
    }

    private static async Task<bool> WaitUntilFileIsReadyAsync(string path)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
            if (!File.Exists(path))
            {
                await Task.Delay(300);
                continue;
            }

            try
            {
                using var filestream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                if (filestream.Length > 0) return true;
            }
            catch (IOException) { /* still being written */ }
            catch (UnauthorizedAccessException) { /* is locked */ }
            await Task.Delay(500);
        }
        return false;
    }

    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
    }

    public void Stop()
    {
        if (_watchers is null || _watchers.Count == 0) return;

        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnCreatedOrRenamed;
            watcher.Renamed -= OnCreatedOrRenamed;
            watcher.Dispose();
        }
        _watchers.Clear();
        _busy.Clear();
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
