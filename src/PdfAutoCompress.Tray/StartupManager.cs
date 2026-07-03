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

    private static string ExePath => Environment.ProcessPath ?? Path.Combine(
        AppContext.BaseDirectory, "PdfAutoCompress.exe");

    public static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{ExePath}\" {StartupArg}");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
