using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PdfAutoCompress.Core;

public static class GhostscriptProcess
{
    private static readonly string[] WindowsDefaultDirectories
        = [@"C:\Program Files\gs", @"C:\Program Files (x86)\gs"];

    private static readonly string[] GhostscriptExeFilenames
        = ["gswin64c.exe", "gswin32c.exe"];

    private const string BinFolder = "bin";
    private const string GhostscriptEnvVariable = "gs";
    private const string PathEnvVariable = "PATH";

    public const string CompressedMarker = "PAC_5f3c9a1e";

    /// <summary>
    /// Try to find the executable path for Ghostscript.
    /// </summary>
    public static string GetExecutablePath(string configuredPath)
    {
        if (IsConfiguredPathValid(configuredPath, out string path))
            return path;

        return IsRunningOnWindows()
            ? GetPathFromWindowsDirectories()
            : GetPathFromEnvVariable(GhostscriptEnvVariable);
    }

    /// <summary>
    /// Start the Ghostscript process to compress the source file.
    /// </summary>
    /// <param name="executablePath">The Ghostscript executable path</param>
    /// <param name="source">The source path of the file to be compressed</param>
    /// <param name="temp">The destination path of the compressed file</param>
    /// <param name="config">The user app configurations</param>
    public static async Task<(int exitCode, string stderr)> StartAsync(
        string executablePath,
        string source,
        string temp,
        AppConfig config
    )
    {
        var processInfo = GetProcessInfo(executablePath, source, temp, config);
        using var process = Process.Start(processInfo)
            ?? throw new InvalidOperationException("Failed to start Ghostscript");

        // Drain both streams concurrently to avoid deadlock
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        await Task.WhenAll(stderrTask, stdoutTask, process.WaitForExitAsync());

        return (process.ExitCode, await stderrTask);
    }

    internal static bool IsConfiguredPathValid(string configuredPath, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(configuredPath)) return false;

        path = Environment.ExpandEnvironmentVariables(configuredPath);
        return File.Exists(path);
    }

    internal static bool IsRunningOnWindows()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    internal static string GetPathFromWindowsDirectories()
    {
        foreach (string root in WindowsDefaultDirectories)
        {
            if (!Directory.Exists(root)) continue;

            foreach (string dir in Directory.GetDirectories(root))
            {
                foreach (string exe in GhostscriptExeFilenames)
                {
                    string candidate = Path.Combine(dir, BinFolder, exe);
                    if (File.Exists(candidate)) return candidate;
                }
            }
        }
        return GetPathFromEnvVariable(GhostscriptExeFilenames[0])
            ?? GetPathFromEnvVariable(GhostscriptExeFilenames[1])
            ?? string.Empty;
    }

    internal static string GetPathFromEnvVariable(string exe)
    {
        string? variablePath = Environment.GetEnvironmentVariable(PathEnvVariable);
        if (string.IsNullOrWhiteSpace(variablePath)) return string.Empty;

        foreach (string dir in variablePath.Split(Path.PathSeparator))
        {
            if (dir.Length == 0) continue;

            try
            {
                string candidate = Path.Combine(dir, exe);
                if (File.Exists(candidate)) return candidate;
            }
            catch { /* Ignore malformed path entries */ }
        }
        return string.Empty;
    }

    internal static ProcessStartInfo GetProcessInfo(
        string executablePath,
        string source,
        string temp,
        AppConfig config
    )
    {
        var processInfo = new ProcessStartInfo(executablePath)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        AddArgumentsToProcess(processInfo, source, temp, config);
        return processInfo;
    }

    internal static void AddArgumentsToProcess(
        ProcessStartInfo processInfo,
        string source,
        string temp,
        AppConfig config
    )
    {
        processInfo.ArgumentList.Add("-sDEVICE=pdfwrite");
        processInfo.ArgumentList.Add("-dCompatibilityLevel=1.4");
        processInfo.ArgumentList.Add($"-dPDFSETTINGS={config.PdfSettings}");
        processInfo.ArgumentList.Add("-dNOPAUSE");
        processInfo.ArgumentList.Add("-dQUIET");
        processInfo.ArgumentList.Add("-dBATCH");
        processInfo.ArgumentList.Add("-dSAFER");
        processInfo.ArgumentList.Add($"-sOutputFile={temp}");
        processInfo.ArgumentList.Add("-c");
        processInfo.ArgumentList.Add($"[ /Keywords ({CompressedMarker}) /DOCINFO pdfmark");
        processInfo.ArgumentList.Add("-f");
        processInfo.ArgumentList.Add(source);
    }
}