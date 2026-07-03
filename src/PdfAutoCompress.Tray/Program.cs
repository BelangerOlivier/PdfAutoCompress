using System.Windows.Forms;

namespace PdfAutoCompress.Tray;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Single instance: a second launch (e.g. from the Run key) just exits.
        using var mutex = new Mutex(initiallyOwned: true, "PdfAutoCompress.SingleInstance", out bool isNew);
        if (!isNew)
            return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext(args));

        GC.KeepAlive(mutex);
    }
}
