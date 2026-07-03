using System.Runtime.InteropServices;

namespace PdfAutoCompress.Core;

public static class GhostscriptChecker
{
    /// <summary>
    /// Try to resolve the Ghostscript executable path. If the configured path is valid, it is returned.
    /// Otherwise, attempts to find Ghostscript in common installation locations or on the system PATH.
    /// </summary>
    public static string ResolveGhostscript(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string p = Environment.ExpandEnvironmentVariables(configured);
            return File.Exists(p) ? p : "";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (string root in new[] { @"C:\Program Files\gs", @"C:\Program Files (x86)\gs" })
            {
                if (!Directory.Exists(root)) continue;

                foreach (string dir in Directory.GetDirectories(root))
                {
                    foreach (string exe in new[] { "gswin64c.exe", "gswin32c.exe" })
                    {
                        string candidate = Path.Combine(dir, "bin", exe);
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            return WhichOnPath("gswin64c.exe") ?? WhichOnPath("gswin32c.exe") ?? "";
        }

        return WhichOnPath("gs") ?? "";
    }

    private static string? WhichOnPath(string exe)
    {
        string? pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar)) return null;

        foreach (string dir in pathVar.Split(Path.PathSeparator))
        {
            if (dir.Length == 0) continue;

            try
            {
                string candidate = Path.Combine(dir, exe);
                if (File.Exists(candidate)) return candidate;
            }
            catch { /* ignore malformed PATH entries */ }
        }

        return null;
    }
}