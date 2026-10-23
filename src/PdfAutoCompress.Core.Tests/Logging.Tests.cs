using PdfAutoCompress.Core.Logging;

using Serilog;
using Serilog.Events;
using Serilog.Parsing;

namespace PdfAutoCompress.Core.Tests;

public class LoggingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"pac-{Guid.NewGuid():N}");

    public void Dispose()
    {
        Log.CloseAndFlush();
        Log.Logger = Serilog.Core.Logger.None;

        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }
        catch { /* best effort */ }

        GC.SuppressFinalize(this);
    }

    private static LogEvent MakeEvent(string message, LogEventLevel level = LogEventLevel.Information, Exception? ex = null) =>
        new(DateTimeOffset.Now, level, ex, new MessageTemplateParser().Parse(message), []);

    [Fact]
    public void SinkFormatsTimeLevelAndMessage()
    {
        var sink = new InMemoryLogSink();

        sink.Emit(MakeEvent("hello", LogEventLevel.Warning));

        string line = Assert.Single(sink.Snapshot());
        Assert.Matches(@"^\d{2}:\d{2}:\d{2} \[WRN\] hello$", line);
    }

    [Fact]
    public void SinkAppendsExceptionSummary()
    {
        var sink = new InMemoryLogSink();

        sink.Emit(MakeEvent("failed", LogEventLevel.Error, new InvalidOperationException("boom")));

        string line = Assert.Single(sink.Snapshot());
        Assert.EndsWith("failed (InvalidOperationException: boom)", line);
    }

    [Fact]
    public void SinkKeepsOnlyTheLastLines()
    {
        var sink = new InMemoryLogSink(capacity: 3);

        for (int i = 1; i <= 5; i++)
            sink.Emit(MakeEvent($"line{i}"));

        IReadOnlyList<string> lines = sink.Snapshot();
        Assert.Equal(3, lines.Count);
        Assert.EndsWith("line3", lines[0]);
        Assert.EndsWith("line5", lines[2]);
    }

    [Fact]
    public void SinkRaisesEmitted()
    {
        var sink = new InMemoryLogSink();
        string? received = null;
        sink.Emitted += line => received = line;

        sink.Emit(MakeEvent("hello"));

        Assert.EndsWith("hello", received);
    }

    [Fact]
    public void ConfigureWritesADatedLogFile()
    {
        AppLogger.Configure(_dir);

        Log.Information("written to file");
        Log.CloseAndFlush();

        string file = Assert.Single(Directory.GetFiles(_dir));
        Assert.Equal($"pdfautocompress-{DateTime.Now:yyyyMMdd}.log", Path.GetFileName(file));
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        Assert.Contains("written to file", reader.ReadToEnd());
    }

    [Fact]
    public void ConfigureKeepsOnlySevenDailyFiles()
    {
        Directory.CreateDirectory(_dir);
        for (int i = 1; i <= 9; i++)
            File.WriteAllText(Path.Combine(_dir, $"pdfautocompress-{DateTime.Now.AddDays(-i):yyyyMMdd}.log"), "old");

        AppLogger.Configure(_dir);
        Log.Information("trigger retention");
        Log.CloseAndFlush();

        Assert.Equal(7, Directory.GetFiles(_dir).Length);
    }
}
