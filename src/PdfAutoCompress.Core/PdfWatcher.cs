using System.Collections.Concurrent;
using System.Diagnostics;

namespace PdfAutoCompress.Core;

public readonly record struct CompressResult(
    string File, long OriginalBytes, long NewBytes, double SavedPercent);

/// <summary>
/// The engine: watches a folder and compresses newly-arrived PDFs in place with Ghostscript.
/// UI-agnostic and cross-platform — every front-end (tray, service, CLI) drives this class.
/// Restartable: call <see cref="Stop"/> then <see cref="Start"/> after settings change.
/// </summary>
public sealed class PdfWatcher : IDisposable
{
    private const string CompressedSuffix = "-compressed.pdf";

    private readonly ConcurrentDictionary<string, DateTime> _busy =
        new(StringComparer.OrdinalIgnoreCase);

    private FileSystemWatcher? _watcher;
    private AppConfig _config = new();

    /// <summary>Human-readable status/activity lines (also mirrored to <see cref="RecentLog"/>).</summary>
    public event Action<string>? Log;

    /// <summary>Raised after a PDF is successfully shrunk.</summary>
    public event Action<CompressResult>? Compressed;

    public string ResolvedGhostscript { get; private set; } = "";
    public string WatchFolder { get; private set; } = "";
    public bool IsRunning => _watcher is { EnableRaisingEvents: true };

    private readonly object _logLock = new();
    private readonly LinkedList<string> _recent = new();
    public IReadOnlyCollection<string> RecentLog
    {
        get { lock (_logLock) return _recent.ToArray(); }
    }

    /// <summary>Starts watching. Returns null on success, or an error message.</summary>
    public string? Start(AppConfig config)
    {
        Stop();
        _config = config;

        WatchFolder = string.IsNullOrWhiteSpace(config.WatchFolder)
            ? DefaultDownloadsFolder()
            : Environment.ExpandEnvironmentVariables(config.WatchFolder);

        if (!Directory.Exists(WatchFolder))
            return $"Watch folder does not exist:\n{WatchFolder}";

        ResolvedGhostscript = GhostscriptChecker.ResolveGhostscript(config.GhostscriptPath);
        if (ResolvedGhostscript.Length == 0)
            return "Ghostscript was not found.\n\nInstall it from " +
                   "https://ghostscript.com/releases/gsdnld.html, or set its path in settings.";

        _watcher = new FileSystemWatcher(WatchFolder, "*.pdf")
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
        };
        _watcher.Created += OnCreatedOrRenamed;
        _watcher.Renamed += OnCreatedOrRenamed;
        _watcher.Error += (_, e) => Emit($"Watcher error: {e.GetException().Message}");
        _watcher.EnableRaisingEvents = true;

        Emit($"Watching {WatchFolder} (Ghostscript: {ResolvedGhostscript})");
        return null;
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
            string name = Path.GetFileName(path);
            if (name.EndsWith(CompressedSuffix, StringComparison.OrdinalIgnoreCase))
                return;

            // Skip files we are already handling / just wrote (short cooldown).
            if (_busy.TryGetValue(path, out var since) &&
                DateTime.UtcNow - since < TimeSpan.FromSeconds(30))
                return;
            if (!_busy.TryAdd(path, DateTime.UtcNow))
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
            Emit($"Error handling {Path.GetFileName(path)}: {ex.Message}");
        }
    }

    private async Task ForgetLater(string path)
    {
        await Task.Delay(TimeSpan.FromSeconds(30));
        _busy.TryRemove(path, out _);
    }

    private async Task ProcessAsync(string src)
    {
        if (!await WaitUntilReadyAsync(src))
            return;

        long origSize = new FileInfo(src).Length;
        if (_config.MinSizeBytes > 0 && origSize < _config.MinSizeBytes)
            return;

        Emit($"Compressing {Path.GetFileName(src)} ({Format(origSize)})…");

        string tmp = src + ".gstmp"; // not *.pdf, so it can't retrigger the watcher
        string dest = _config.KeepOriginal
            ? Path.Combine(Path.GetDirectoryName(src)!,
                           Path.GetFileNameWithoutExtension(src) + CompressedSuffix)
            : src;

        try
        {
            (int exit, string stderr) = await RunGhostscriptAsync(src, tmp);
            if (exit != 0 || !File.Exists(tmp))
            {
                string detail = stderr.Length > 300 ? stderr[..300] : stderr;
                Emit($"  Ghostscript failed (exit {exit}). {detail}");
                return;
            }

            long newSize = new FileInfo(tmp).Length;
            if (newSize < origSize)
            {
                _busy[dest] = DateTime.UtcNow; // ignore the resulting file event
                File.Move(tmp, dest, overwrite: true);
                double savedPct = 100.0 * (1 - (double)newSize / origSize);
                Emit($"  Compressed {Path.GetFileName(dest)}: " +
                     $"{Format(origSize)} → {Format(newSize)} ({savedPct:F1}% saved)");
                Compressed?.Invoke(new CompressResult(dest, origSize, newSize, savedPct));
            }
            else
            {
                Emit($"  Skipped {Path.GetFileName(src)}: no size gain.");
            }
        }
        finally
        {
            if (File.Exists(tmp))
            {
                try { File.Delete(tmp); } catch { /* best effort */ }
            }
        }
    }

    private async Task<(int exit, string stderr)> RunGhostscriptAsync(string src, string tmp)
    {
        var psi = new ProcessStartInfo(ResolvedGhostscript)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-sDEVICE=pdfwrite");
        psi.ArgumentList.Add("-dCompatibilityLevel=1.4");
        psi.ArgumentList.Add($"-dPDFSETTINGS={_config.PdfSettings}");
        psi.ArgumentList.Add("-dNOPAUSE");
        psi.ArgumentList.Add("-dQUIET");
        psi.ArgumentList.Add("-dBATCH");
        psi.ArgumentList.Add("-dSAFER");
        psi.ArgumentList.Add($"-sOutputFile={tmp}");
        psi.ArgumentList.Add(src);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start Ghostscript.");
        // Drain stderr before waiting to avoid deadlock on large output.
        string stderr = await proc.StandardError.ReadToEndAsync();
        _ = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return (proc.ExitCode, stderr);
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
            catch (UnauthorizedAccessException) { }
            await Task.Delay(500);
        }
        return false;
    }

    private void Emit(string line)
    {
        string stamped = $"{DateTime.Now:HH:mm:ss}  {line}";
        lock (_logLock)
        {
            _recent.AddLast(stamped);
            while (_recent.Count > 200) _recent.RemoveFirst();
        }
        Log?.Invoke(stamped);
    }

    private static string Format(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double v = bytes;
        int u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return u == 0 ? $"{bytes} B" : $"{v:0.#} {units[u]}";
    }

    public static string DefaultDownloadsFolder()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Downloads");
    }
}
