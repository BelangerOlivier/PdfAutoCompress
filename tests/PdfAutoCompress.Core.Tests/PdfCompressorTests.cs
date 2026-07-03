using PdfAutoCompress.Core;

namespace PdfAutoCompress.Core.Tests;

public class PdfCompressorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"pac-{Guid.NewGuid():N}");

    public PdfCompressorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string MakePdf(string name, int bytes)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    /// <summary>A fake Ghostscript that writes <paramref name="outBytes"/> to the temp file.</summary>
    private static PdfCompressor.GhostscriptRunner FakeRunner(int outBytes, int exit = 0) =>
        (_, _, tmp, _) =>
        {
            File.WriteAllBytes(tmp, new byte[outBytes]);
            return Task.FromResult((exit, ""));
        };

    [Fact]
    public async Task ReplacesOriginalWhenSmaller()
    {
        string src = MakePdf("a.pdf", 1000);
        var config = new AppConfig { MinSizeBytes = 0 };

        CompressResult? result = await PdfCompressor.CompressFileAsync(src, config, "gs", FakeRunner(400));

        Assert.NotNull(result);
        Assert.Equal(src, result!.Value.File);
        Assert.Equal(1000, result.Value.OriginalBytes);
        Assert.Equal(400, result.Value.NewBytes);
        Assert.Equal(60.0, result.Value.SavedPercent, precision: 1);
        Assert.Equal(400, new FileInfo(src).Length);
    }

    [Fact]
    public async Task LeavesOriginalWhenNoGain()
    {
        string src = MakePdf("a.pdf", 1000);
        var config = new AppConfig { MinSizeBytes = 0 };

        CompressResult? result = await PdfCompressor.CompressFileAsync(src, config, "gs", FakeRunner(1200));

        Assert.Null(result);
        Assert.Equal(1000, new FileInfo(src).Length);
        Assert.False(File.Exists(src + ".gstmp"));
    }

    [Fact]
    public async Task ThrowsAndCleansUpWhenGhostscriptFails()
    {
        string src = MakePdf("a.pdf", 1000);
        var config = new AppConfig { MinSizeBytes = 0 };

        await Assert.ThrowsAsync<GhostscriptException>(() =>
            PdfCompressor.CompressFileAsync(src, config, "gs", FakeRunner(400, exit: 1)));

        Assert.Equal(1000, new FileInfo(src).Length);
        Assert.False(File.Exists(src + ".gstmp"));
    }

    [Fact]
    public async Task SkipsFileBelowMinSize()
    {
        string src = MakePdf("a.pdf", 500);
        var config = new AppConfig { MinSizeBytes = 1000 };
        bool ranGhostscript = false;

        CompressResult? result = await PdfCompressor.CompressFileAsync(src, config, "gs",
            (_, _, tmp, _) => { ranGhostscript = true; File.WriteAllBytes(tmp, new byte[1]); return Task.FromResult((0, "")); });

        Assert.Null(result);
        Assert.False(ranGhostscript);
        Assert.Equal(500, new FileInfo(src).Length);
    }

    [Fact]
    public async Task KeepOriginalWritesSeparateCopy()
    {
        string src = MakePdf("report.pdf", 1000);
        var config = new AppConfig { MinSizeBytes = 0, KeepOriginal = true };

        CompressResult? result = await PdfCompressor.CompressFileAsync(src, config, "gs", FakeRunner(400));

        string expectedCopy = Path.Combine(_dir, "report" + PdfCompressor.CompressedSuffix);
        Assert.NotNull(result);
        Assert.Equal(expectedCopy, result!.Value.File);
        Assert.Equal(1000, new FileInfo(src).Length);   // original untouched
        Assert.Equal(400, new FileInfo(expectedCopy).Length);
    }
}
