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

        // Single instance. A normal second launch (e.g. from the Run key) exits immediately;
        // a post-install relaunch waits briefly for the copy that installed to exit and
        // release the mutex, so the installed instance reliably takes over.
        bool relaunch = args.Contains(Installer.RelaunchArg, StringComparer.OrdinalIgnoreCase);
        using var mutex = new Mutex(initiallyOwned: false, "PdfAutoCompress.SingleInstance");

        bool acquired;
        try { acquired = mutex.WaitOne(relaunch ? TimeSpan.FromSeconds(15) : TimeSpan.Zero); }
        catch (AbandonedMutexException) { acquired = true; } // previous owner exited abruptly
        if (!acquired)
            return;

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext(args));
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }
}
