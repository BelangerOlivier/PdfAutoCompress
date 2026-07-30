namespace PdfAutoCompress.Core.Tests;

public class GhostscriptProcessTests
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"pac-{Guid.NewGuid():N}");

    public GhostscriptProcessTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch { /* best effort */ }
    }

    [Fact]
    public void ReturnsConfiguredPathWhenItExists()
    {
        string file = Path.Combine(Path.GetTempPath(), $"gs-{Guid.NewGuid():N}.exe");
        File.WriteAllText(file, "");

        try
        {
            Assert.Equal(file, GhostscriptProcess.GetExecutablePath(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ReturnsEnvPathWhenItExists()
    {
        string file = Path.Combine(Path.GetTempPath(), $"gs-{Guid.NewGuid():N}.exe");
        File.WriteAllText(file, "");

        string var = $"GS_TEST_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(var, Path.GetDirectoryName(file));

        try
        {
            string configured = Path.Combine($"%{var}%", Path.GetFileName(file));
            Assert.Equal(file, GhostscriptProcess.GetExecutablePath(configured));
        }
        finally
        {
            Environment.SetEnvironmentVariable(var, null);
            File.Delete(file);
        }
    }

    // This test should run only if Ghostscript is installed on the machine
    /*
    [Theory]
    [InlineData("")]
    [InlineData("INVALID")]
    [InlineData("INVALID.exe")]
    public void ReturnsDefaultPathWhenConfiguredPathIsInvalid(string configuredPath)
    {
        string invalidPath = Path.Combine(Path.GetTempPath(), configuredPath);
        string path = GhostscriptProcess.GetExecutablePath(invalidPath);
        Assert.NotNull(path);
        Assert.NotEqual("", path);
    }
    */

    [Theory]
    [InlineData("", "file.pdf", "file.tmp")]
    [InlineData("gs.exe", "", "file.tmp")]
    [InlineData("gs.exe", "file.pdf", "")]
    public void ReturnsArgumentExceptionWithInvalidProcessParams(
        string executablePath, string source, string temp
    )
    {
        var config = new AppConfig();

        if (string.IsNullOrWhiteSpace(executablePath))
            Assert.Throws<ArgumentException>(() => GhostscriptProcess.GetProcess(executablePath, source, temp, config));
        else if (string.IsNullOrWhiteSpace(source))
            Assert.Throws<ArgumentException>(() => GhostscriptProcess.GetProcess(executablePath, source, temp, config));
        else if (string.IsNullOrWhiteSpace(temp))
            Assert.Throws<ArgumentException>(() => GhostscriptProcess.GetProcess(executablePath, source, temp, config));
    }

    [Fact]
    public void ReturnsProcessWithValidParams()
    {
        string file = Path.Combine(Path.GetTempPath(), $"gs-{Guid.NewGuid():N}.exe");
        File.WriteAllText(file, "");

        string executablePath = GhostscriptProcess.GetExecutablePath(file);
        string source = "report.pdf";
        string temp = source + ".gstmp";
        var config = new AppConfig { MinSizeBytes = 0, KeepOriginal = true };

        var process = GhostscriptProcess.GetProcess(executablePath, source, temp, config);
        Assert.NotNull(process);
        Assert.Equal(executablePath, process.FileName);
        Assert.Contains(source, process.ArgumentList);
    }
}
