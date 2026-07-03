using PdfAutoCompress.Core;

namespace PdfAutoCompress.Core.Tests;

public class GhostscriptCheckerTests
{
    [Fact]
    public void ReturnsConfiguredPathWhenItExists()
    {
        string file = Path.Combine(Path.GetTempPath(), $"gs-{Guid.NewGuid():N}.exe");
        File.WriteAllText(file, "");
        try
        {
            Assert.Equal(file, GhostscriptChecker.ResolveGhostscript(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ReturnsEmptyWhenConfiguredPathMissing()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"nope-{Guid.NewGuid():N}.exe");
        Assert.Equal("", GhostscriptChecker.ResolveGhostscript(missing));
    }

    [Fact]
    public void ExpandsEnvironmentVariablesInConfiguredPath()
    {
        string file = Path.Combine(Path.GetTempPath(), $"gs-{Guid.NewGuid():N}.exe");
        File.WriteAllText(file, "");
        string var = $"GS_TEST_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(var, Path.GetDirectoryName(file));
        try
        {
            string configured = Path.Combine($"%{var}%", Path.GetFileName(file));
            Assert.Equal(file, GhostscriptChecker.ResolveGhostscript(configured));
        }
        finally
        {
            Environment.SetEnvironmentVariable(var, null);
            File.Delete(file);
        }
    }
}
