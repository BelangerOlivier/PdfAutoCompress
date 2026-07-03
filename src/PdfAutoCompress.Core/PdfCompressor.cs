namespace PdfAutoCompress.Core;

/// <summary>
/// Compresses a single PDF with Ghostscript, in place or to a "-compressed.pdf" copy.
/// UI- and watcher-agnostic: used by <see cref="PdfWatcher"/>, one-shot front-ends
/// (the Explorer context menu, the "Compress a file now" tray item) and tests.
/// </summary>
public static class PdfCompressor
{
    public const string CompressedSuffix = "-compressed.pdf";

    public delegate Task<(int exit, string stderr)> GhostscriptRunner(
        string ghostscriptExe, string src, string tmp, AppConfig config);

    public static async Task<CompressResult?> CompressFileAsync(
        string src, AppConfig config, string ghostscriptExe, GhostscriptRunner? runner = null)
    {
        runner ??= GhostscriptCompress.RunGhostscriptAsync;

        if (!File.Exists(src))
            return null;

        long origSize = new FileInfo(src).Length;
        if (config.MinSizeBytes > 0 && origSize < config.MinSizeBytes)
            return null;

        string tmp = src + ".gstmp"; // not *.pdf, so it can't retrigger a watcher
        string dest = config.KeepOriginal
            ? Path.Combine(Path.GetDirectoryName(src)!,
                           Path.GetFileNameWithoutExtension(src) + CompressedSuffix)
            : src;

        try
        {
            (int exit, string stderr) = await runner(ghostscriptExe, src, tmp, config);
            if (exit != 0 || !File.Exists(tmp))
                throw new GhostscriptException(exit, stderr);

            long newSize = new FileInfo(tmp).Length;
            if (newSize >= origSize)
                return null; // no gain — leave the original untouched

            File.Move(tmp, dest, overwrite: true);
            double savedPct = 100.0 * (1 - (double)newSize / origSize);
            return new CompressResult(dest, origSize, newSize, savedPct);
        }
        finally
        {
            if (File.Exists(tmp))
            {
                try { File.Delete(tmp); } catch { /* best effort */ }
            }
        }
    }
}

/// <summary>Thrown when Ghostscript exits non-zero or produces no output file.</summary>
public sealed class GhostscriptException(int exitCode, string stderr)
    : Exception($"Ghostscript failed (exit {exitCode}).")
{
    public int ExitCode { get; } = exitCode;
    public string Stderr { get; } = stderr;
}
