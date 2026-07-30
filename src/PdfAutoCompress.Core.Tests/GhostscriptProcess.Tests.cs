using System.Diagnostics;

namespace PdfAutoCompress.Core.Tests;

public class GhostscriptProcessTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsConfiguredPathValidReturnsFalseForBlank(string? configuredPath)
    {
        Assert.False(GhostscriptProcess.IsConfiguredPathValid(configuredPath!, out string path));
        Assert.Equal(string.Empty, path);
    }

    [Fact]
    public void IsConfiguredPathValidReturnsFalseWhenFileDoesNotExist()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"gs-{Guid.NewGuid():N}.exe");
        Assert.False(GhostscriptProcess.IsConfiguredPathValid(missing, out _));
    }

    [Fact]
    public void GetPathFromEnvVariableFindsExecutableInPathDirectory()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"pac-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string exeName = $"gs-{Guid.NewGuid():N}.exe";
        string exePath = Path.Combine(dir, exeName);
        File.WriteAllText(exePath, "");

        string originalPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        Environment.SetEnvironmentVariable("PATH", originalPath + Path.PathSeparator + dir);

        try
        {
            Assert.Equal(exePath, GhostscriptProcess.GetPathFromEnvVariable(exeName));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void GetPathFromEnvVariableReturnsEmptyWhenExecutableNotFound()
    {
        Assert.Equal(string.Empty, GhostscriptProcess.GetPathFromEnvVariable($"gs-{Guid.NewGuid():N}.exe"));
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

    [Fact]
    public void ReturnsProcessWithValidParams()
    {
        string file = Path.Combine(Path.GetTempPath(), $"gs-{Guid.NewGuid():N}.exe");
        File.WriteAllText(file, "");

        string executablePath = GhostscriptProcess.GetExecutablePath(file);
        string source = "report.pdf";
        string temp = source + ".gstmp";
        var config = new AppConfig { MinSizeBytes = 0, KeepOriginal = true };

        var process = GhostscriptProcess.GetProcessInfo(executablePath, source, temp, config);
        Assert.NotNull(process);
        Assert.Equal(executablePath, process.FileName);
        Assert.Contains(source, process.ArgumentList);
    }

    [Fact]
    public void GetProcessInfoConfiguresRedirectionAndNoWindow()
    {
        var config = new AppConfig();

        var process = GhostscriptProcess.GetProcessInfo("gs", "source.pdf", "source.pdf.gstmp", config);

        Assert.True(process.RedirectStandardError);
        Assert.True(process.RedirectStandardOutput);
        Assert.False(process.UseShellExecute);
        Assert.True(process.CreateNoWindow);
    }

    [Fact]
    public void AddArgumentsToProcessIncludesOutputFileAndPdfSettings()
    {
        var processInfo = new ProcessStartInfo("gs");
        var config = new AppConfig { PdfSettings = "/screen" };

        GhostscriptProcess.AddArgumentsToProcess(processInfo, "source.pdf", "source.pdf.gstmp", config);

        Assert.Contains("-sOutputFile=source.pdf.gstmp", processInfo.ArgumentList);
        Assert.Contains("-dPDFSETTINGS=/screen", processInfo.ArgumentList);
        Assert.Equal("source.pdf", processInfo.ArgumentList[^1]);
    }
}
