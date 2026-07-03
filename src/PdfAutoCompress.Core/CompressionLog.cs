namespace PdfAutoCompress.Core;

public sealed class CompressionLog
{
    /// <summary>Human-readable status/activity lines (also mirrored to <see cref="RecentLog"/>).</summary>
    public event Action<string>? Log;

    private readonly Lock _logLock = new();
    private readonly LinkedList<string> _recent = new();

    public IReadOnlyCollection<string> RecentLog
    {
        get { lock (_logLock) return [.. _recent]; }
    }

    public void Emit(string line)
    {
        string stamped = $"{DateTime.Now:HH:mm:ss}  {line}";
        lock (_logLock)
        {
            _recent.AddLast(stamped);
            while (_recent.Count > 200) _recent.RemoveFirst();
        }
        Log?.Invoke(stamped);
    }
}