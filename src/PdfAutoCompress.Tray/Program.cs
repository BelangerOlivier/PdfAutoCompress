using System;

using Eto.Drawing;
using Eto.Forms;

namespace PdfAutoCompress.Tray;

internal static class Program
{
    private static Application app;
    private static TrayIndicator tray;
    private static MainForm window;

    [STAThread]
    static void Main()
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
        };

        app.Run();
    }

    static void ShowWindow()
    {
        if (window == null)
        {
            window = new MainForm();
            window.Closed += (_, __) => window = null;
        }
        window.Show();
        window.BringToFront();
    }
}
