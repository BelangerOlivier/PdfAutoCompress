using Microsoft.Win32;

namespace PdfAutoCompress.Tray;

/// <summary>
/// Adds/removes an HKCU "Run" entry so the app starts at login (no admin needed).
/// The command includes a --startup flag so the app can delay work at boot (lazy start).
/// </summary>
internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PdfAutoCompress";

    public const string StartupArg = "--startup";

    private static string CurrentExe => Environment.ProcessPath ?? Path.Combine(
        AppContext.BaseDirectory, "PdfAutoCompress.exe");

    public static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    /// <summary>Enable/disable autostart. When enabling, points at <paramref name="exePath"/>
    /// (defaults to the running executable).</summary>
    public static void SetEnabled(bool enabled, string? exePath = null)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{exePath ?? CurrentExe}\" {StartupArg}");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
