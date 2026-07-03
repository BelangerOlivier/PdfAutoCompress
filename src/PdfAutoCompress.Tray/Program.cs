namespace PdfAutoCompress.Tray;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Silent install/uninstall (also used by the "Install on this PC" button under the hood).
        if (args.Contains("--install", StringComparer.OrdinalIgnoreCase))
        {
            Installer.Install();
            return;
        }
        if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
        {
            Installer.Uninstall();
            return;
        }

        // Single instance: a second launch (e.g. from the Run key) just exits.
        using var mutex = new Mutex(initiallyOwned: true, "PdfAutoCompress.SingleInstance", out bool isNew);
        if (!isNew)
            return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext(args));

        GC.KeepAlive(mutex);
    }
}
