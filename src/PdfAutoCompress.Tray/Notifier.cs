using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace PdfAutoCompress.Tray;

internal sealed class Notifier : IDisposable
{
    private readonly NotifyIcon _icon;

    private const string Title = "PDF Auto Compress";

    public Notifier(Icon icon)
    {
        _icon = new NotifyIcon { Icon = icon, Visible = false };
        _icon.BalloonTipClosed += (_, __) => _icon.Visible = false;
        _icon.BalloonTipClicked += (_, __) => _icon.Visible = false;
    }

    public void ShowSuccessfulCompression(
        string filename,
        string originalSize,
        string newSize,
        double savedPercent
    )
    {
        StringBuilder sb = new();
        sb.AppendLine($"{originalSize} -> {newSize} ({savedPercent:F1}% saved)");
        sb.AppendLine(filename);
        Show(sb.ToString());
    }

    private void Show(string message)
    {
        _icon.Visible = true;
        _icon.ShowBalloonTip(5000, Title, message, ToolTipIcon.Info);
    }

    public void Dispose() => _icon.Dispose();
}
