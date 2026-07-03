using System.Diagnostics;

namespace PdfAutoCompress.Core;

public static class GhostscriptCompress
{
    public static async Task<(int exit, string stderr)> RunGhostscriptAsync(
        string ghostscriptExePath,
        string src,
        string tmp,
        AppConfig config
    )
    {
        var psi = new ProcessStartInfo(ghostscriptExePath)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-sDEVICE=pdfwrite");
        psi.ArgumentList.Add("-dCompatibilityLevel=1.4");
        psi.ArgumentList.Add($"-dPDFSETTINGS={config.PdfSettings}");
        psi.ArgumentList.Add("-dNOPAUSE");
        psi.ArgumentList.Add("-dQUIET");
        psi.ArgumentList.Add("-dBATCH");
        psi.ArgumentList.Add("-dSAFER");
        psi.ArgumentList.Add($"-sOutputFile={tmp}");
        psi.ArgumentList.Add(src);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start Ghostscript.");
        // Drain stderr before waiting to avoid deadlock on large output.
        string stderr = await proc.StandardError.ReadToEndAsync();
        _ = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return (proc.ExitCode, stderr);
    }
}
