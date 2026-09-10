using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;

namespace Olden_Era___Template_Editor.Tests;

public class TemplatePreviewLayoutTests
{
    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 2)]
    [InlineData(2, 4)]
    [InlineData(4, 0)]
    [InlineData(4, 2)]
    [InlineData(4, 4)]
    [InlineData(8, 0)]
    [InlineData(8, 2)]
    [InlineData(8, 4)]
    public void LanesPreview_KeepsWholeNodesInsideFrameAndSeparated(int players, int depth)
    {
        var template = TemplateGenerator.Generate(new GeneratorSettings
        {
            PlayerCount = players,
            Topology = MapTopology.Lanes,
            ZoneCfg = new ZoneConfiguration
            {
                Advanced = new AdvancedSettings
                {
                    Enabled = true,
                    // Spread the fixture across tiers to stay below the per-tier cap of 30.
                    NeutralLowNoCastleCount = players * depth / 2,
                    NeutralMediumNoCastleCount = players * depth / 2,
                    NeutralHighCastleCount = 1,
                },
            },
        });
        var positions = TemplatePreviewPngWriter.ComputeLayout(template, MapTopology.Lanes);
        double radius = TemplatePreviewPngWriter.GetLastZoneRadius();
        Assert.Equal(players * (depth + 1) + 1, positions.Count);
        foreach (var point in positions.Values)
        {
            Assert.InRange(point.X - radius, 19.999, 680.001);
            Assert.InRange(point.X + radius, 19.999, 680.001);
            Assert.InRange(point.Y - radius, 19.999, 680.001);
            Assert.InRange(point.Y + radius, 19.999, 680.001);
        }
        var points = positions.Values.ToArray();
        for (int i = 0; i < points.Length; i++)
            for (int j = i + 1; j < points.Length; j++)
                Assert.True((points[i] - points[j]).Length >= 2 * radius + 7.999,
                    $"Nodes {i} and {j} overlap or lack the preview gap");
    }
}
