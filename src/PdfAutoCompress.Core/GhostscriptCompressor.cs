using Serilog;

namespace PdfAutoCompress.Core;

public readonly record struct CompressResult(
    string File, long OriginalSize, long NewSize);

public static class GhostscriptCompressor
{
    public delegate Task<(int exit, string stderr)> GhostscriptRunner(
        string ghostscriptExe, string source, string temp, AppConfig config);

    public delegate bool PageCountValidator(string source, string output);

    public static async Task<CompressResult?> CompressFileAsync(
        string ghostscriptExe,
        string source,
        AppConfig config,
        GhostscriptRunner? runner = null,
        PageCountValidator? validator = null
    )
    {
        runner ??= GhostscriptProcess.StartAsync;
        validator ??= FileInspector.IsSamePageCount;

        if (!File.Exists(source))
            return null;

        long originalSize = GetFileSize(source);
        if (!CanBeCompressed(originalSize, config.MinSizeBytes))
            return null;

        string temp = source + Constants.TempFileExtension; // Not *.pdf, so it can't retrigger a watcher
        string destination = GetCompressedFileDestination(source, config.KeepOriginal);

        try
        {
            (int exitCode, string stderr) = await runner(ghostscriptExe, source, temp, config);
            if (!IsCompressionSuccessful(exitCode, temp))
                throw new GhostscriptException(exitCode, stderr);

            long newSize = GetFileSize(temp);
            if (!IsCompressionSmaller(originalSize, newSize))
            {
                Log.Information("No gain for {File}: {New} bytes is not smaller than {Original} bytes, original left untouched",
                    Path.GetFileName(source), newSize, originalSize);
                return null;
            }

            if (!validator(source, temp))
                throw new PageCountMismatchException(FileInspector.GetPageCount(source), FileInspector.GetPageCount(temp));

            MoveFileToDestination(temp, destination);
            return new CompressResult(destination, originalSize, newSize);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); }
                catch { /* Ignore error */ }
            }
        }
    }

    public static string GetCompressedFileDestination(string source, bool keepOriginal)
    {
        return keepOriginal
            ? Path.Combine(
                Path.GetDirectoryName(source)!,
                Path.GetFileNameWithoutExtension(source) + Constants.CompressedSuffix)
            : source;
    }

    internal static long GetFileSize(string source)
    {
        return File.Exists(source)
            ? new FileInfo(source).Length
            : 0;
    }

    internal static bool CanBeCompressed(long size, long minSizeBytes)
        => minSizeBytes >= 0 && size > minSizeBytes;

    internal static bool IsCompressionSuccessful(int exitCode, string temp)
        => exitCode == 0 && File.Exists(temp);

    internal static bool IsCompressionSmaller(long originalSize, long newSize)
        => newSize < originalSize;

    internal static void MoveFileToDestination(string temp, string destination)
        => File.Move(temp, destination, overwrite: true);
}
