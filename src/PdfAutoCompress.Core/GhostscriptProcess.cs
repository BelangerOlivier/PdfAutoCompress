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

    public static string GetExecutablePath(string configuredPath)
    {
        if (IsConfiguredPathValid(configuredPath, out string path))
            return path;

        return IsRunningOnWindows()
            ? GetPathFromWindowsDirectories()
            : GetPathFromEnvVariable(GhostscriptEnvVariable);
    }

    private static bool IsConfiguredPathValid(string configuredPath, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(configuredPath)) return false;

        path = Environment.ExpandEnvironmentVariables(configuredPath);
        return File.Exists(path);
    }

    private static bool IsRunningOnWindows()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static string GetPathFromWindowsDirectories()
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

    private static string GetPathFromEnvVariable(string exe)
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

    public static ProcessStartInfo GetProcess(
        string executablePath,
        string source,
        string temp,
        AppConfig config
    )
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new ArgumentException("The executablePath is empty");
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("The source is empty");
        if (string.IsNullOrWhiteSpace(temp))
            throw new ArgumentException("The temp is empty");

        var process = GetProcessInfo(executablePath);
        AddArgumentsToProcess(process, source, temp, config);
        return process;
    }

    private static ProcessStartInfo GetProcessInfo(string executablePath)
    {
        return new ProcessStartInfo(executablePath)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }

    private static void AddArgumentsToProcess(
        ProcessStartInfo process,
        string source,
        string temp,
        AppConfig config
    )
    {
        process.ArgumentList.Add("-sDEVICE=pdfwrite");
        process.ArgumentList.Add("-dCompatibilityLevel=1.4");
        process.ArgumentList.Add($"-dPDFSETTINGS={config.PdfSettings}");
        process.ArgumentList.Add("-dNOPAUSE");
        process.ArgumentList.Add("-dQUIET");
        process.ArgumentList.Add("-dBATCH");
        process.ArgumentList.Add("-dSAFER");
        process.ArgumentList.Add($"-sOutputFile={temp}");
        process.ArgumentList.Add("-c");
        process.ArgumentList.Add($"[ /Keywords ({CompressedMarker}) /DOCINFO pdfmark");
        process.ArgumentList.Add("-f");
        process.ArgumentList.Add(source);
    }
}