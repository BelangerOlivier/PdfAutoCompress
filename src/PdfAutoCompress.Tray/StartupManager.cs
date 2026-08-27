#nullable enable

using System;

using Microsoft.Win32;

namespace PdfAutoCompress.Tray;

internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PdfAutoCompress";

    public static bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is not null;
    }

    public static void SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return;

        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            string? exePath = Environment.ProcessPath;
            if (exePath is not null) key.SetValue(ValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
