namespace PdfAutoCompress.Core.Tests;

public class GhostscriptProcessTests
{
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
}
