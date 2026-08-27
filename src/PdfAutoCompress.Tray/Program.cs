using System;
using System.Threading;

using Eto.Drawing;
using Eto.Forms;

using PdfAutoCompress.Core;

namespace PdfAutoCompress.Tray;

internal static class Program
{
    private static Application app;
    private static TrayIndicator tray;
    private static SettingsForm window;
    private static FileWatcher fileWatcher;

    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: false, "PdfAutoCompress.SingleInstance");

        try
        {
            app = new Application();
            app.Initialized += (s, e) =>
            {
                var settingsItem = new ButtonMenuItem { Text = "Settings" };
                settingsItem.Click += (_, __) => ShowWindow();

                var quitItem = new ButtonMenuItem { Text = "Quit" };
                quitItem.Click += (_, __) => app.Quit();

                tray = new TrayIndicator
                {
                    Title = "PDF Auto Compress",
                    Image = Icon.FromResource("PdfAutoCompress.Tray.Resources.app.ico"),
                    Menu = new ContextMenu { Items = { settingsItem, quitItem } }
                };
                tray.Activated += (_, __) => ShowWindow();
                tray.Show();

                fileWatcher = new FileWatcher();
                StartWatcher();
            };

            app.Run();
        }
        finally
        {
            fileWatcher?.Dispose();
            mutex.ReleaseMutex();
        }
    }

    public static void RestartWatcher() => StartWatcher();

    static void StartWatcher()
    {
        string error = fileWatcher.Start(AppConfig.Load());
        if (error is not null)
            MessageBox.Show(error, "PDF Auto Compress", MessageBoxButtons.OK, MessageBoxType.Error);
    }

    static void ShowWindow()
    {
        if (window == null)
        {
            window = new SettingsForm();
            window.Closed += (_, __) => window = null;
        }
        window.Show();
        window.BringToFront();
    }
}
