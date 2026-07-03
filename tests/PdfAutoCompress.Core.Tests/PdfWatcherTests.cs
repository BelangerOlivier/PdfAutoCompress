using PdfAutoCompress.Core;

namespace PdfAutoCompress.Core.Tests;

public class PdfWatcherTests
{
    [Theory]
    [InlineData("report-compressed.pdf", true)]
    [InlineData("REPORT-COMPRESSED.PDF", true)]
    [InlineData("report.pdf", false)]
    [InlineData("compressed.pdf", false)]
    public void RecognizesAlreadyCompressedFiles(string name, bool expected)
    {
        Assert.Equal(expected, PdfWatcher.IsFileCompressed(Path.Combine("C:", "x", name)));
    }

    [Fact]
    public void DedupesTheSamePathWithinTheWindow()
    {
        using var watcher = new PdfWatcher();
        string path = Path.Combine("C:", "downloads", "a.pdf");

        Assert.False(watcher.IsFileHandled(path)); // first sighting → process it
        Assert.True(watcher.IsFileHandled(path));  // seen again → skip
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1073741824, "1 GB")]
    public void FormatsByteSizes(long bytes, string expected)
    {
        Assert.Equal(expected, PdfWatcher.Format(bytes));
    }
}
