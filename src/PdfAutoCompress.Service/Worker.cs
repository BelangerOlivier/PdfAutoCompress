using PdfAutoCompress.Core;

namespace PdfAutoCompress.Service;

/// <summary>
/// Hosts the <see cref="PdfWatcher"/> engine as a long-running background service.
/// Runs as a Windows Service when installed, or as a plain console app otherwise.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _log;
    private readonly PdfWatcher _watcher = new();

    public Worker(ILogger<Worker> log)
    {
        _log = log;
        _watcher.Logger.Log += m => _log.LogInformation("{Message}", m);
        _watcher.Compressed += r => _log.LogInformation("Compressed {File} ({Percent:F1}% saved)", r.File, r.SavedPercent);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        AppConfig config = AppConfig.Load();
        string? error = _watcher.Start(config);
        if (error != null)
            _log.LogError("Not watching: {Error}", error);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        finally
        {
            _watcher.Stop();
        }
    }

    public override void Dispose()
    {
        _watcher.Dispose();
        base.Dispose();
    }
}
