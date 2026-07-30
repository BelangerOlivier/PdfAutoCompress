namespace PdfAutoCompress.Core.Tests;

public class GhostscriptCompressorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"pac-{Guid.NewGuid():N}");

    public GhostscriptCompressorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch { /* best effort */ }

        GC.SuppressFinalize(this);
    }

    private string MakePdf(string name, int bytes)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    private static GhostscriptCompressor.GhostscriptRunner FakeRunner(int outBytes, int exitCode = 0) =>
        (_, _, temp, _) =>
        {
            File.WriteAllBytes(temp, new byte[outBytes]);
            return Task.FromResult((exitCode, ""));
        };

    [Fact]
    public async Task ReplacesOriginalWhenSmaller()
    {
        string source = MakePdf("a.pdf", 1000);
        var config = new AppConfig { MinSizeBytes = 0 };

        CompressResult? result = await GhostscriptCompressor.CompressFileAsync(
            "gs", source, config, FakeRunner(400));

        Assert.NotNull(result);
        Assert.Equal(source, result.Value.File);
        Assert.Equal(1000, result.Value.OriginalSize);
        Assert.Equal(400, result.Value.NewSize);
        Assert.Equal(400, new FileInfo(source).Length);
    }

    [Fact]
    public async Task LeavesOriginalWhenNoGain()
    {
        string source = MakePdf("a.pdf", 1000);
        var config = new AppConfig { MinSizeBytes = 0 };

        CompressResult? result = await GhostscriptCompressor.CompressFileAsync(
            "gs", source, config, FakeRunner(1250));

        Assert.Null(result);
        Assert.Equal(1000, new FileInfo(source).Length);
        Assert.False(File.Exists(source + GhostscriptCompressor.TempFileExtension));
    }

    [Fact]
    public async Task ThrowsAndCleansUpWhenGhostscriptFails()
    {
        string source = MakePdf("a.pdf", 1000);
        var config = new AppConfig { MinSizeBytes = 0 };

        await Assert.ThrowsAsync<GhostscriptException>(() =>
            GhostscriptCompressor.CompressFileAsync(
                "gs", source, config, FakeRunner(1250, exitCode: 1)));

        Assert.Equal(1000, new FileInfo(source).Length);
        Assert.False(File.Exists(source + GhostscriptCompressor.TempFileExtension));
    }

    [Fact]
    public async Task CleansUpTempFileWhenRunnerThrows()
    {
        string source = MakePdf("a.pdf", 1000);
        var config = new AppConfig { MinSizeBytes = 0 };

        GhostscriptCompressor.GhostscriptRunner throwingRunner = (_, _, temp, _) =>
        {
            File.WriteAllBytes(temp, new byte[400]); // Ghostscript wrote partial output before crashing
            throw new InvalidOperationException("process could not be started");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GhostscriptCompressor.CompressFileAsync("gs", source, config, throwingRunner));

        Assert.Equal(1000, new FileInfo(source).Length);
        Assert.False(File.Exists(source + GhostscriptCompressor.TempFileExtension));
    }

    [Fact]
    public async Task SkipsFileBelowMinSize()
    {
        string source = MakePdf("a.pdf", 1000);
        var config = new AppConfig { MinSizeBytes = 1250 };

        CompressResult? result = await GhostscriptCompressor.CompressFileAsync(
            "gs", source, config, null);

        Assert.Null(result);
        Assert.Equal(1000, new FileInfo(source).Length);
    }

    [Fact]
    public async Task SkipsFileWhenMinSizeBytesIsNegative()
    {
        string source = MakePdf("a.pdf", 1000);
        var config = new AppConfig { MinSizeBytes = -10 };

        CompressResult? result = await GhostscriptCompressor.CompressFileAsync(
            "gs", source, config, null);

        Assert.Null(result);
        Assert.Equal(1000, new FileInfo(source).Length);
    }

    [Fact]
    public async Task ReturnsNullWhenSourceDoesNotExist()
    {
        string source = Path.Combine(_dir, "missing.pdf");
        var config = new AppConfig { MinSizeBytes = 0 };

        CompressResult? result = await GhostscriptCompressor.CompressFileAsync(
            "gs", source, config, null);

        Assert.Null(result);
    }

    [Fact]
    public async Task KeepOriginalWithSeparateCopy()
    {
        string source = MakePdf("a.pdf", 1000);
        var config = new AppConfig { MinSizeBytes = 0, KeepOriginal = true };

        CompressResult? result = await GhostscriptCompressor.CompressFileAsync(
            "gs", source, config, FakeRunner(400));

        string expectedCopy = Path.Combine(_dir, "a" + GhostscriptCompressor.CompressedSuffix);
        Assert.NotNull(result);
        Assert.Equal(expectedCopy, result.Value.File);
        Assert.Equal(1000, new FileInfo(source).Length);
        Assert.Equal(400, new FileInfo(expectedCopy).Length);
    }

    [Fact]
    public async Task KeepOriginalWithNoGainCreatesNoCopy()
    {
        string source = MakePdf("a.pdf", 1000);
        var config = new AppConfig { MinSizeBytes = 0, KeepOriginal = true };

        CompressResult? result = await GhostscriptCompressor.CompressFileAsync(
            "gs", source, config, FakeRunner(1250));

        string wouldBeCopy = Path.Combine(_dir, "a" + GhostscriptCompressor.CompressedSuffix);
        Assert.Null(result);
        Assert.Equal(1000, new FileInfo(source).Length);
        Assert.False(File.Exists(wouldBeCopy));
    }

    [Fact]
    public void ReturnsDestinationWhenOriginalIsNotKept()
    {
        string file = Path.Combine(Path.GetTempPath(), $"a.pdf");
        Assert.Equal(file, GhostscriptCompressor.GetCompressedFileDestination(file, false));
    }

    [Fact]
    public void ReturnsSourceWhenOriginalIsKept()
    {
        string fileWithoutExt = Path.Combine(Path.GetTempPath(), $"a");
        string originalFile = fileWithoutExt + ".pdf";
        string compressedFile = fileWithoutExt + GhostscriptCompressor.CompressedSuffix;
        Assert.Equal(compressedFile, GhostscriptCompressor.GetCompressedFileDestination(originalFile, true));

    }

    [Fact]
    public void ReturnsFileSizeWhenFileExists()
    {
        string source = MakePdf("a.pdf", 1000);
        Assert.Equal(1000, GhostscriptCompressor.GetFileSize(source));
    }

    [Fact]
    public void ReturnsZeroWhenFileDoesNotExist()
    {
        string file = $"{Guid.NewGuid():N}.pdf";
        Assert.Equal(0, GhostscriptCompressor.GetFileSize(file));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(100, 99)]
    public void ReturnsTrueWhenFileCanBeCompressed(long size, long minSizeBytes)
    {
        Assert.True(GhostscriptCompressor.CanBeCompressed(size, minSizeBytes));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 100)]
    [InlineData(-10, -10)]
    public void ReturnsFalseWhenFileCannotBeCompressed(long size, long minSizeBytes)
    {
        Assert.False(GhostscriptCompressor.CanBeCompressed(size, minSizeBytes));
    }

    [Fact]
    public void ReturnsTrueWhenCompressionIsSuccessful()
    {
        string source = MakePdf("a.pdf", 1000);
        Assert.True(GhostscriptCompressor.IsCompressionSuccessful(0, source));
    }

    [Theory]
    [InlineData(0, "INVALID-FILE.pdf")]
    [InlineData(1, "INVALID-FILE.pdf")]
    public void ReturnsFalseWhenCompressionIsNotSuccessful(int exitCode, string temp)
    {
        Assert.False(GhostscriptCompressor.IsCompressionSuccessful(exitCode, temp));
    }

    [Fact]
    public void ReturnsTrueWhenCompressionIsSmaller()
    {
        long originalSize = 100;
        long newSize = 10;
        Assert.True(GhostscriptCompressor.IsCompressionSmaller(originalSize, newSize));
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(100, 1000)]
    public void ReturnsFalseWhenCompressionIsNotSmaller(long originalSize, long newSize)
    {
        Assert.False(GhostscriptCompressor.IsCompressionSmaller(originalSize, newSize));
    }

    [Fact]
    public void IsFileMovedCorrectly()
    {
        string source = MakePdf("a.pdf", 1000);
        string destination = Path.Combine(_dir, $"b.pdf");
        GhostscriptCompressor.MoveFileToDestination(source, destination);
        Assert.True(File.Exists(destination));
    }
}
