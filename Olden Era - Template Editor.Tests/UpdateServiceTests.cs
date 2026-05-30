using Olden_Era___Template_Editor.Services.Update;

namespace Olden_Era___Template_Editor.Tests;

public class UpdateServiceTests
{
    [Theory]
    [InlineData("v0.8.0", 0, 8, 0)]
    [InlineData("0.8.0", 0, 8, 0)]
    [InlineData("V1.2.3", 1, 2, 3)]
    [InlineData("v0.8.0.0", 0, 8, 0)]   // 4-part tag normalises to major.minor.build
    [InlineData("v2.0", 2, 0, 0)]       // missing build defaults to 0
    [InlineData("v0.8.0-beta.1", 0, 8, 0)] // pre-release suffix is stripped
    public void TryParseTag_ParsesCommonFormats(string tag, int major, int minor, int build)
    {
        Assert.True(UpdateService.TryParseTag(tag, out var version));
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("v")]
    [InlineData("latest")]
    [InlineData("vNext")]
    public void TryParseTag_RejectsNonNumericTags(string tag)
    {
        Assert.False(UpdateService.TryParseTag(tag, out _));
    }

    [Fact]
    public void ParsedTags_OrderByVersionSemantics()
    {
        Assert.True(UpdateService.TryParseTag("v0.8.0", out var newer));
        Assert.True(UpdateService.TryParseTag("v0.7.1", out var older));
        Assert.True(newer > older);

        // A patch bump is still newer than its base.
        Assert.True(UpdateService.TryParseTag("v0.8.1", out var patch));
        Assert.True(patch > newer);
    }
}
