using PdfAutoCompress.Core;

namespace PdfAutoCompress.Core.Tests;

public class UpdateCheckerTests
{
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
