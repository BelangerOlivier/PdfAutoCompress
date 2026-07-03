using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PdfAutoCompress.Tray;

/// <summary>
/// Installs the app into a stable per-user folder so it doesn't have to live in Downloads.
/// Target: %LOCALAPPDATA%\Programs\PdfAutoCompress (no admin needed). Also creates a Start
/// Menu shortcut and points autostart at the installed copy.
/// </summary>
internal static class Installer
{
    public const string AppName = "PDF Auto-Compress";

    public const string RelaunchArg = "--relaunch";

    public static string InstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "PdfAutoCompress");

    public static string InstalledExe => Path.Combine(InstallDir, "PdfAutoCompress.exe");

    private static string CurrentExe => Environment.ProcessPath ?? InstalledExe;

    private static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");

    public static bool IsInstalled =>
        string.Equals(Path.GetFullPath(CurrentExe), Path.GetFullPath(InstalledExe),
                      StringComparison.OrdinalIgnoreCase)
        || File.Exists(InstalledExe);

    public static bool RunningFromInstall =>
        string.Equals(Path.GetFullPath(CurrentExe), Path.GetFullPath(InstalledExe),
                      StringComparison.OrdinalIgnoreCase);

    public static void Install()
    {
        // Create the directory and copy the exe.
        Directory.CreateDirectory(InstallDir);
        File.Copy(CurrentExe, InstalledExe, overwrite: true);

        // Create a Start Menu shortcut and enable autostart for the installed copy.
        TryCreateShortcut();
        try { StartupManager.SetEnabled(true, InstalledExe); } catch { /* non-fatal */ }

        // Start the installed copy now.
        Process.Start(new ProcessStartInfo(InstalledExe, RelaunchArg) { UseShellExecute = true });
    }

    public static void Uninstall()
    {
        // Remove the autostart entry
        try { StartupManager.SetEnabled(false); } catch { /* ignore */ }

        // Delete the start menu shortcut
        try { if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath); } catch { /* ignore */ }

        // Delete the install folder after we exit (the exe is locked while running).
        RunAfterExit($"rmdir /s /q \"{InstallDir}\"");
    }

    private static void RunAfterExit(string command)
    {
        // `ping -n 3` waits ~2s reliably giving this process time to exit and release the exe/mutex.
        var psi = new ProcessStartInfo("cmd.exe", $"/c ping -n 3 127.0.0.1 >nul & {command}")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        Process.Start(psi);
    }

    private static void TryCreateShortcut()
    {
        try
        {
            Type? t = Type.GetTypeFromProgID("WScript.Shell");
            if (t is null) return;
            dynamic shell = Activator.CreateInstance(t)!;
            dynamic sc = shell.CreateShortcut(ShortcutPath);
            sc.TargetPath = InstalledExe;
            sc.WorkingDirectory = InstallDir;
            sc.Description = AppName;
            sc.Save();
            Marshal.FinalReleaseComObject(sc);
            Marshal.FinalReleaseComObject(shell);
        }
        catch
        {
            // Shortcut is a nicety; ignore if the Windows Script Host isn't available.
        }
    }
}
