using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace PdfAutoCompress.Tray;

/// <summary>
/// Builds the app/tray icon at runtime so nothing external has to be shipped.
/// The returned HICON must be freed with <see cref="Destroy"/> to avoid a GDI handle leak.
/// </summary>
internal static class IconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create(out IntPtr hicon)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.Clear(Color.Transparent);

            using var body = new SolidBrush(Color.FromArgb(0xC1, 0x1B, 0x17)); // PDF red
            using var path = RoundedRect(new Rectangle(3, 1, 26, 30), 5);
            g.FillPath(body, path);

            using var font = new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var text = new SolidBrush(Color.White);
            var fmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString("PDF", font, text, new RectangleF(3, 1, 26, 30), fmt);
        }

        hicon = bmp.GetHicon();
        return Icon.FromHandle(hicon);
    }

    public static void Destroy(IntPtr hicon)
    {
        if (hicon != IntPtr.Zero) DestroyIcon(hicon);
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
