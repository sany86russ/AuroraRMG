using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

/// <summary>
/// The sketch importer works on raw BGRA pixels, so these tests paint tiny synthetic "sketches" in
/// memory and check what the analyser reads back out of them.
/// </summary>
public class MapSketchImporterTests
{
    [Fact]
    public void BoundaryAdjacency_MatchesExhaustivePixelSearch()
    {
        var rng = new Random(1701);
        for (int run = 0; run < 60; run++)
        {
            const int width = 25, height = 20;
            var labels = new int[width * height];
            for (int box = 1; box <= 7; box++)
            {
                int x0 = rng.Next(width), y0 = rng.Next(height);
                int x1 = Math.Min(width, x0 + rng.Next(2, 15)), y1 = Math.Min(height, y0 + rng.Next(2, 15));
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) labels[y * width + x] = box;
            }
            int radius = rng.Next(1, 8); var kept = new HashSet<int> { 1, 2, 3, 5, 6, 7 };
            var expected = new HashSet<(int, int)>();
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int a = labels[y * width + x]; if (!kept.Contains(a)) continue;
                for (int ny = Math.Max(0, y - radius); ny <= Math.Min(height - 1, y + radius); ny++)
                for (int nx = Math.Max(0, x - radius); nx <= Math.Min(width - 1, x + radius); nx++)
                {
                    int b = labels[ny * width + nx]; if (b != a && kept.Contains(b)) expected.Add((Math.Min(a,b), Math.Max(a,b)));
                }
            }
            Assert.True(expected.SetEquals(MapSketchImporter.FindAdjacency(labels, width, height, kept, radius)), $"run {run}");
        }
    }
    [Fact]
    public void Analyse_ExcessGreenStartsBecomeDistinctNeutralZones()
    {
        byte[] pixels = Canvas();
        for (int i = 0; i < 12; i++) Rect(pixels, i * 10 + 1, 10, i * 10 + 8, 40, 40, 200, 60);
        var result = MapSketchImporter.Analyse(pixels, W, H, minAreaPercent: 0.1);
        var variant = result.Template.Variants![0];
        Assert.Equal(12, variant.Zones!.Count);
        Assert.Equal(12, variant.Zones.Select(z => z.Name).Distinct().Count());
        Assert.Equal(12, result.Positions.Count);
        Assert.Equal(KnownValues.SpawnPlayers.Length, variant.Zones.SelectMany(z => z.MainObjects ?? []).Count(o => o.Type == "Spawn"));
        Assert.Empty(ZoneGraphValidator.Validate(variant.Zones, variant.Connections!));
    }

    [Fact]
    public void Analyse_RejectsOverflowingDimensionsBeforeAllocation()
    {
        Assert.Throws<ArgumentException>(() => MapSketchImporter.Analyse([], int.MaxValue, 2));
        Assert.Throws<ArgumentException>(() => MapSketchImporter.Analyse([], int.MaxValue, int.MaxValue));
    }

    private const int W = 120, H = 60;

    /// <summary>A white canvas — the importer's background.</summary>
    private static byte[] Canvas()
    {
        var pixels = new byte[W * H * 4];
        for (int i = 0; i < W * H; i++)
        {
            pixels[i * 4] = 255; pixels[i * 4 + 1] = 255; pixels[i * 4 + 2] = 255; pixels[i * 4 + 3] = 255;
        }
        return pixels;
    }

    private static void Rect(byte[] pixels, int x0, int y0, int x1, int y1, byte r, byte g, byte b)
    {
        for (int y = y0; y < y1; y++)
        for (int x = x0; x < x1; x++)
        {
            int i = (y * W + x) * 4;
            pixels[i] = b; pixels[i + 1] = g; pixels[i + 2] = r; pixels[i + 3] = 255;
        }
    }

    [Theory]
    [InlineData(40, 200, 60, "zone_layout_player_spawn")]   // green
    [InlineData(60, 90, 210, "zone_layout_start_zone")]     // blue
    [InlineData(220, 190, 40, "zone_layout_supertreasure_zone")] // gold
    [InlineData(180, 100, 30, "zone_layout_side_zone")]     // orange/brown
    [InlineData(190, 190, 190, "zone_layout_treasure_zone")] // light grey
    [InlineData(210, 40, 40, "zone_layout_wincondition_zone")] // red
    public void RoleForColour_MapsHueToAZoneRole(byte r, byte g, byte b, string expected)
        => Assert.Equal(expected, MapSketchImporter.RoleForColour(r, g, b));

    [Fact]
    public void Analyse_TouchingBlobsBecomeConnectedZones()
    {
        byte[] pixels = Canvas();
        Rect(pixels, 5, 10, 35, 50, 40, 200, 60);      // green  → player start
        Rect(pixels, 35, 10, 70, 50, 190, 190, 190);   // grey   → treasure, touching the green
        Rect(pixels, 70, 10, 110, 50, 40, 200, 60);    // green  → second player start, touching the grey

        SketchImportResult result = MapSketchImporter.Analyse(pixels, W, H);
        Variant variant = Assert.Single(result.Template.Variants ?? []);

        Assert.Equal(3, variant.Zones!.Count);
        Assert.Equal(2, variant.Connections!.Count);          // A–mid and mid–B, not A–B
        Assert.DoesNotContain(variant.Connections, c =>
            (c.From == "Spawn-A" && c.To == "Spawn-B") || (c.From == "Spawn-B" && c.To == "Spawn-A"));

        var spawns = variant.Zones.SelectMany(z => z.MainObjects ?? [])
                                  .Where(o => o.Type == "Spawn")
                                  .Select(o => o.Spawn)
                                  .OrderBy(s => s)
                                  .ToList();
        Assert.Equal(["Player1", "Player2"], spawns);
        Assert.Empty(ZoneGraphValidator.Validate(variant.Zones, variant.Connections));
    }

    [Fact]
    public void Analyse_SeparateIslandsAreStillLinked()
    {
        byte[] pixels = Canvas();
        Rect(pixels, 5, 10, 30, 50, 40, 200, 60);      // far left
        Rect(pixels, 90, 10, 115, 50, 40, 200, 60);    // far right — nothing touches

        SketchImportResult result = MapSketchImporter.Analyse(pixels, W, H);
        Variant variant = result.Template.Variants![0];

        Assert.Equal(2, variant.Zones!.Count);
        Assert.Single(variant.Connections!);
        Assert.Contains(Olden_Era___Template_Editor.Services.Localization.LocalizationManager.T("S.IM.Islands", 1), result.Warnings);
        Assert.Empty(ZoneGraphValidator.Validate(variant.Zones, variant.Connections!));
    }

    [Fact]
    public void Analyse_IgnoresNoiseSpecks()
    {
        byte[] pixels = Canvas();
        Rect(pixels, 5, 10, 40, 50, 40, 200, 60);
        Rect(pixels, 60, 10, 100, 50, 190, 190, 190);
        Rect(pixels, 110, 55, 113, 58, 210, 40, 40);   // a 3×3 speck

        SketchImportResult result = MapSketchImporter.Analyse(pixels, W, H, minAreaPercent: 1.0);

        Assert.Equal(2, result.Template.Variants![0].Zones!.Count);
        Assert.Contains(Olden_Era___Template_Editor.Services.Localization.LocalizationManager.T("S.IM.Noise", 1, 1.0), result.Warnings);
    }

    [Fact]
    public void Analyse_BiggerBlobsBecomeBiggerZones()
    {
        byte[] pixels = Canvas();
        Rect(pixels, 2, 20, 20, 40, 40, 200, 60);      // small
        Rect(pixels, 20, 5, 118, 55, 190, 190, 190);   // large, touching

        SketchImportResult result = MapSketchImporter.Analyse(pixels, W, H);
        List<Zone> zones = result.Template.Variants![0].Zones!;

        Zone small = zones.First(z => z.Name.StartsWith("Spawn", StringComparison.Ordinal));
        Zone large = zones.First(z => !z.Name.StartsWith("Spawn", StringComparison.Ordinal));
        Assert.True(large.Size > small.Size, $"large {large.Size} should exceed small {small.Size}");
        Assert.All(zones, z => Assert.InRange(z.Size!.Value, 0.4, 2.0));
    }

    [Fact]
    public void Analyse_PositionsFollowTheBlobCentres()
    {
        byte[] pixels = Canvas();
        Rect(pixels, 5, 10, 35, 50, 40, 200, 60);
        Rect(pixels, 35, 10, 110, 50, 190, 190, 190);

        SketchImportResult result = MapSketchImporter.Analyse(pixels, W, H);

        (double X, double Y) left = result.Positions["Spawn-A"];
        (double X, double Y) right = result.Positions["Zone-1"];
        Assert.True(left.X < right.X);
        Assert.All(result.Positions.Values, p => { Assert.InRange(p.X, 0, 1); Assert.InRange(p.Y, 0, 1); });
    }

    [Fact]
    public void Analyse_RejectsABlankSheet()
        => Assert.Throws<InvalidOperationException>(() => MapSketchImporter.Analyse(Canvas(), W, H));

    [Fact]
    public void Analyse_RejectsATruncatedBuffer()
        => Assert.Throws<ArgumentException>(() => MapSketchImporter.Analyse(new byte[10], W, H));
}
