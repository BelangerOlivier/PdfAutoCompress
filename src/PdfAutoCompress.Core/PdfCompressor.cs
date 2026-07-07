using UglyToad.PdfPig;

namespace PdfAutoCompress.Core;

public static class PdfCompressor
{
    public const string CompressedSuffix = "-compressed.pdf";
    public const string Marker = "PAC_5f3c9a1e";

    public static bool IsAlreadyCompressed(string path)
    {
        try
        {
            using var doc = PdfDocument.Open(path);
            string? kw = doc.Information.Keywords;
            return kw is not null && kw.Contains(Marker);
        }
        catch
        {
            return false;
        }
    }

    public static string DestinationFor(string src, AppConfig config) =>
        config.KeepOriginal
            ? Path.Combine(Path.GetDirectoryName(src)!,
                           Path.GetFileNameWithoutExtension(src) + CompressedSuffix)
            : src;

    public delegate Task<(int exit, string stderr)> GhostscriptRunner(
        string ghostscriptExe, string src, string tmp, AppConfig config);

    public static async Task<CompressResult?> CompressFileAsync(
        string src, AppConfig config, string ghostscriptExe, GhostscriptRunner? runner = null)
    {
        runner ??= Core.GhostscriptRunner.RunGhostscriptAsync;

        if (!File.Exists(src))
            return null;

        long origSize = new FileInfo(src).Length;
        if (config.MinSizeBytes > 0 && origSize < config.MinSizeBytes)
            return null;

        string tmp = src + ".gstmp"; // not *.pdf, so it can't retrigger a watcher
        string dest = DestinationFor(src, config);

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

public sealed class GhostscriptException(int exitCode, string stderr)
    : Exception($"Ghostscript failed (exit {exitCode}).")
{
    public int ExitCode { get; } = exitCode;
    public string Stderr { get; } = stderr;
}
