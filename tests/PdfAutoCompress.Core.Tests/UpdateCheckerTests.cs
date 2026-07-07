using System.Text.Json;
using PdfAutoCompress.Core;

namespace PdfAutoCompress.Core.Tests;

public class UpdateCheckerTests
{
    private static JsonElement Root(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void FindsInstallerAssetUrlAndSize()
    {
        var root = Root($$"""
        {
          "assets": [
            { "name": "pdfautocompress-cli-win-x64.exe", "browser_download_url": "https://x/cli.exe", "size": 111 },
            { "name": "{{UpdateInstaller.InstallerAssetName}}", "browser_download_url": "https://x/setup.exe", "size": 2048 }
          ]
        }
        """);

        (string url, long size) = UpdateChecker.FindInstallerAsset(root);

        Assert.Equal("https://x/setup.exe", url);
        Assert.Equal(2048, size);
    }

    [Fact]
    public void MatchesInstallerAssetCaseInsensitively()
    {
        var root = Root("""
        { "assets": [ { "name": "PDFAUTOCOMPRESSSETUP.EXE", "browser_download_url": "https://x/s.exe", "size": 5 } ] }
        """);

        (string url, long size) = UpdateChecker.FindInstallerAsset(root);

        Assert.Equal("https://x/s.exe", url);
        Assert.Equal(5, size);
    }

    [Fact]
    public void ReturnsEmptyWhenNoInstallerAsset()
    {
        var root = Root("""
        { "assets": [ { "name": "notes.txt", "browser_download_url": "https://x/n.txt", "size": 3 } ] }
        """);

        (string url, long size) = UpdateChecker.FindInstallerAsset(root);

        Assert.Equal("", url);
        Assert.Equal(0, size);
    }

    [Fact]
    public void ReturnsEmptyWhenAssetsMissing()
    {
        (string url, long size) = UpdateChecker.FindInstallerAsset(Root("{}"));

        Assert.Equal("", url);
        Assert.Equal(0, size);
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("V2.0", "2.0")]
    [InlineData("v1.2.3-beta", "1.2.3")]
    [InlineData("1.2.3+meta", "1.2.3")]
    [InlineData("v1.2.3 (notes)", "1.2.3")]
    public void ParsesVersionTags(string tag, string expected)
    {
        Assert.True(UpdateChecker.TryParseVersion(tag, out Version v));
        Assert.Equal(Version.Parse(expected), v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("vNext")]
    public void RejectsNonVersionTags(string tag)
    {
        Assert.False(UpdateChecker.TryParseVersion(tag, out _));
    }
}
