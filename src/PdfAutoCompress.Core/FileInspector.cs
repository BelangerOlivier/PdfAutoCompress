using UglyToad.PdfPig;

namespace PdfAutoCompress.Core;

public readonly record struct PdfInfo(int PageCount, bool IsCompressed);

public static class FileInspector
{
    public static PdfInfo Inspect(string path)
    {
        try
        {
            using var doc = PdfDocument.Open(path);
            string? keywords = doc.Information.Keywords;
            bool isCompressed = keywords is not null && keywords.Contains(GhostscriptProcess.CompressedMarker);
            return new PdfInfo(doc.NumberOfPages, isCompressed);
        }
        catch
        {
            return new PdfInfo(0, false);
        }
    }

    public static bool IsCompressed(string path) => Inspect(path).IsCompressed;

    public static int GetPageCount(string path) => Inspect(path).PageCount;

    public static bool IsSamePageCount(string source, string output)
    {
        int sourcePageCount = GetPageCount(source);
        int outputPageCount = GetPageCount(output);
        return sourcePageCount > 0 && sourcePageCount == outputPageCount;
    }
}
