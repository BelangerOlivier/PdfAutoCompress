namespace PdfAutoCompress.Tray;

internal static class IconFactory
{
    private const string ResourceName = "PdfAutoCompress.Tray.app.ico";

    public static System.Drawing.Icon GetBaseIcon()
    {
        using Stream stream = GetResourceStream();
        return new System.Drawing.Icon(stream);
    }

    public static Eto.Drawing.Icon GetEtoIcon()
    {
        using Stream stream = GetResourceStream();
        return new Eto.Drawing.Icon(stream);
    }

    private static Stream GetResourceStream()
    {
        return typeof(IconFactory).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded icon '{ResourceName}' not found.");
    }
}
