namespace PdfAutoCompress.Tray;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Single instance. A second launch (e.g. a stray double-click while the autostart copy is
        // already running) exits immediately. The mutex name is also what the Inno Setup installer
        // watches (AppMutex) so it can close the running app before an upgrade.
        using var mutex = new Mutex(initiallyOwned: false, "PdfAutoCompress.SingleInstance");

        bool acquired;
        try { acquired = mutex.WaitOne(TimeSpan.Zero); }
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
