using Serilog.Core;
using Serilog.Events;

namespace PdfAutoCompress.Core.Logging;

public sealed class InMemoryLogSink(int capacity = 500) : ILogEventSink
{
    private readonly Queue<string> _lines = new();
    private readonly object _lock = new();

    public event Action<string>? Emitted;

    public void Emit(LogEvent logEvent)
    {
        string line = Format(logEvent);

        lock (_lock)
        {
            _lines.Enqueue(line);
            while (_lines.Count > capacity)
                _lines.Dequeue();
        }

        Emitted?.Invoke(line);
    }

    public IReadOnlyList<string> Snapshot()
    {
        lock (_lock)
            return [.. _lines];
    }

    internal static string Format(LogEvent logEvent)
    {
        string level = logEvent.Level switch
        {
            LogEventLevel.Verbose => "VRB",
            LogEventLevel.Debug => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning => "WRN",
            LogEventLevel.Error => "ERR",
            _ => "FTL"
        };

        string line = $"{logEvent.Timestamp:HH:mm:ss} [{level}] {logEvent.RenderMessage()}";
        if (logEvent.Exception is { } ex)
            line += $" ({ex.GetType().Name}: {ex.Message})";
        return line;
    }
}
