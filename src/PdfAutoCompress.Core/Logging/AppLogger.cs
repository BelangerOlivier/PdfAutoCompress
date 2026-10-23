using Serilog;
using Serilog.Events;

namespace PdfAutoCompress.Core.Logging;

public static class AppLogger
{
    private const int RetainedDays = 7;

    public static InMemoryLogSink Sink { get; } = new();

    public static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PdfAutoCompress",
        "logs"
    );

    public static void Configure(string? directory = null)
    {
        string path = Path.Combine(directory ?? LogDirectory, "pdfautocompress-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedDays,
                shared: true,
                restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.Sink(Sink)
            .CreateLogger();
    }

    public static void Shutdown() => Log.CloseAndFlush();
}
