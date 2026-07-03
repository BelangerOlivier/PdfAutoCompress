namespace PdfAutoCompress.Tray;

internal static class IconFactory
{
    private const string ResourceName = "PdfAutoCompress.Tray.app.ico";

    public static Icon Create()
    {
        using Stream stream = typeof(IconFactory).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded icon '{ResourceName}' not found.");
        return new Icon(stream);
    }

    public static Icon Small(Icon icon) => new(icon, SystemInformation.SmallIconSize);
}
