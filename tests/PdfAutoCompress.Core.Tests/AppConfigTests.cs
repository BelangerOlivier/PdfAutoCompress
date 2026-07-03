using PdfAutoCompress.Core;

namespace PdfAutoCompress.Core.Tests;

public class AppConfigTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pac-cfg-{Guid.NewGuid():N}", "appsettings.json");

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_path)!, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void RoundTripsThroughAFile()
    {
        var config = new AppConfig
        {
            WatchFolder = @"C:\Docs",
            GhostscriptPath = @"C:\gs\gswin64c.exe",
            PdfSettings = "/printer",
            MinSizeBytes = 5 * 1024 * 1024,
            KeepOriginal = true,
            ShowNotifications = false,
            CheckForUpdates = false,
            StartupDelaySeconds = 42,
            SetupPromptShown = true,
        };

        config.SaveTo(_path);
        AppConfig? loaded = AppConfig.LoadFrom(_path);

        Assert.NotNull(loaded);
        Assert.Equal(config.WatchFolder, loaded!.WatchFolder);
        Assert.Equal(config.GhostscriptPath, loaded.GhostscriptPath);
        Assert.Equal(config.PdfSettings, loaded.PdfSettings);
        Assert.Equal(config.MinSizeBytes, loaded.MinSizeBytes);
        Assert.Equal(config.KeepOriginal, loaded.KeepOriginal);
        Assert.Equal(config.ShowNotifications, loaded.ShowNotifications);
        Assert.Equal(config.CheckForUpdates, loaded.CheckForUpdates);
        Assert.Equal(config.StartupDelaySeconds, loaded.StartupDelaySeconds);
        Assert.Equal(config.SetupPromptShown, loaded.SetupPromptShown);
    }

    [Fact]
    public void DefaultsAreSensible()
    {
        var config = new AppConfig();
        Assert.Equal("/ebook", config.PdfSettings);
        Assert.Equal(1024 * 1024, config.MinSizeBytes);
        Assert.True(config.ShowNotifications);
        Assert.False(config.KeepOriginal);
    }

    [Fact]
    public void ToleratesCommentsAndTrailingCommas()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, """
            {
              // user-edited by hand
              "PdfSettings": "/screen",
              "KeepOriginal": true,
            }
            """);

        AppConfig? loaded = AppConfig.LoadFrom(_path);

        Assert.NotNull(loaded);
        Assert.Equal("/screen", loaded!.PdfSettings);
        Assert.True(loaded.KeepOriginal);
    }

    [Fact]
    public void ReturnsNullForMissingFile()
    {
        Assert.Null(AppConfig.LoadFrom(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json")));
    }
}
