namespace PdfAutoCompress.Core;

public sealed class GhostscriptException(int exitCode, string stderr)
    : Exception($"Ghostscript failed with exit code {exitCode}.")
{
    public int ExitCode { get; } = exitCode;
    public string Stderr { get; } = stderr;
}

public sealed class PageCountMismatchException(int expected, int actual)
    : Exception($"Page count changed during compression. Expected {expected}, got {actual}.")
{
    public int Expected { get; } = expected;
    public int Actual { get; } = actual;
}