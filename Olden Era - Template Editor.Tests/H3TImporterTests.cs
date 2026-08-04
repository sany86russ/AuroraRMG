using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

/// <summary>
/// The .h3t table is tab-separated and ~140 columns wide; these tests build rows to that layout so
/// the importer's column mapping, row classification and normalisation are pinned down without a
/// copyrighted sample file.
/// </summary>
public class H3TImporterTests
{
    private const int Columns = 140;

    private static string Row(params (int Index, string Value)[] cells)
    {
        var parts = new string[Columns];
        Array.Fill(parts, "");
        foreach ((int index, string value) in cells) parts[index] = value;
        return string.Join("\t", parts);
    }

    /// <summary>type, id, name (+ optional extra cells).</summary>
    private static string ZoneRow(int type, int id, string name, params (int, string)[] extra)
        => Row([(2, type.ToString()), (28, id.ToString()), (7, name), .. extra]);

    private static string ConnectionRow(int from, int to, int value, params (int, string)[] extra)
        => Row([(127, from.ToString()), (128, to.ToString()), (129, value.ToString()), .. extra]);

    private static readonly string[] MinimalFile =
    [
        "Template\tHeader\trow that is neither a zone nor a connection",
        ZoneRow(0, 1, "Red", (29, "x"), (34, "20")),                 // human start
        ZoneRow(1, 2, "Blue", (30, "x"), (34, "20")),                // computer start
        ZoneRow(4, 3, "Treasury", (34, "40"), (48, "x")),            // treasure zone allowing a town
        ConnectionRow(1, 3, 7500, (132, "+")),
        ConnectionRow(3, 2, 7500),
    ];

    [Fact]
    public void Parse_MapsZonesConnectionsAndStarts()
    {
        H3TImportResult result = H3TImporter.ParseLines(MinimalFile);
        Variant variant = Assert.Single(result.Template.Variants ?? []);

        Assert.Equal(3, result.ZoneCount);
        Assert.Equal(2, result.ConnectionCount);

        Assert.Equal("zone_layout_player_spawn", variant.Zones![0].Layout);
        Assert.Equal("zone_layout_ai_spawn", variant.Zones[1].Layout);
        Assert.Equal("zone_layout_treasure_zone", variant.Zones[2].Layout);

        // Both starts become player spawns, numbered in file order.
        var spawns = variant.Zones.SelectMany(z => z.MainObjects ?? [])
                                  .Where(o => o.Type == "Spawn")
                                  .Select(o => o.Spawn)
                                  .ToList();
        Assert.Equal(["Player1", "Player2"], spawns);

        // A zone that allows towns gets a capturable, unowned city.
        MainObject city = Assert.Single(variant.Zones[2].MainObjects ?? [], o => o.Type == "City");
        Assert.Null(city.Owner);

        Connection first = variant.Connections![0];
        Assert.Equal("Red", first.From);
        Assert.Equal("Treasury", first.To);
        Assert.Equal(7500, first.GuardValue);
        Assert.True(first.Road);
        Assert.Null(variant.Connections[1].Road);
    }

    [Fact]
    public void Parse_ProducesASelfContainedTemplate()
    {
        H3TImportResult result = H3TImporter.ParseLines(MinimalFile);
        Variant variant = result.Template.Variants![0];

        // Every referenced layout must have a definition, or the game cannot shape the zone.
        var defined = (result.Template.ZoneLayouts ?? []).Select(l => l.Name ?? "").ToHashSet(StringComparer.Ordinal);
        Assert.All(variant.Zones!, z => Assert.Contains(z.Layout ?? "", defined));

        // …and the graph itself has to be valid.
        Assert.Empty(ZoneGraphValidator.Validate(variant.Zones!, variant.Connections!));
    }

    [Fact]
    public void Parse_NormalizesZoneSizesRelativeToEachOther()
    {
        H3TImportResult result = H3TImporter.ParseLines(MinimalFile);
        List<Zone> zones = result.Template.Variants![0].Zones!;

        // Raw 20 / 20 / 40 → average 26.67 → the treasury is the biggest, all within the UI range.
        Assert.True(zones[2].Size > zones[0].Size);
        Assert.All(zones, z => Assert.InRange(z.Size!.Value, 0.4, 2.0));
    }

    [Fact]
    public void Parse_DropsSelfLoopsAndDuplicateEdges()
    {
        string[] file =
        [
            ZoneRow(0, 1, "Red", (29, "x")),
            ZoneRow(4, 2, "Mid"),
            ConnectionRow(1, 2, 100),
            ConnectionRow(2, 1, 100),   // the same edge, written the other way round
            ConnectionRow(1, 1, 100),   // self-loop
            ConnectionRow(1, 9, 100),   // dangling zone ordinal
        ];

        H3TImportResult result = H3TImporter.ParseLines(file);

        Assert.Equal(1, result.ConnectionCount);
        Assert.Contains(result.Warnings, w => w.Contains("not in the file"));
    }

    [Theory]
    [InlineData("1", "Portal")]
    [InlineData("2", "Proximity")]
    [InlineData("3", "GladiatorArena")]
    [InlineData("", "Direct")]
    public void Parse_MapsTheConnectionTypeCode(string code, string expected)
    {
        string[] file =
        [
            ZoneRow(0, 1, "Red", (29, "x")),
            ZoneRow(4, 2, "Mid"),
            ConnectionRow(1, 2, 100, (133, code)),
        ];

        H3TImportResult result = H3TImporter.ParseLines(file);

        Assert.Equal(expected, result.Template.Variants![0].Connections![0].ConnectionType);
    }

    [Fact]
    public void Parse_MapsTerrainFlagsToBiomes()
    {
        // Columns 75..84 are Dirt, Sand, Grass, Snow, Swamp, Rough, Cave, Lava, Highlands, Wasteland.
        string[] file =
        [
            ZoneRow(0, 1, "Red", (29, "x"), (77, "x"), (82, "x")),   // Grass + Lava
            ZoneRow(4, 2, "Cavern", (81, "x")),                       // Cave — no surface equivalent
            ConnectionRow(1, 2, 100),
        ];

        H3TImportResult result = H3TImporter.ParseLines(file);
        List<Zone> zones = result.Template.Variants![0].Zones!;

        Assert.Equal("FromList", zones[0].ZoneBiome!.Type);
        Assert.Equal(["Grass", "Lava"], zones[0].ZoneBiome!.Args);
        Assert.Equal("MatchZone", zones[1].ZoneBiome!.Type);
        Assert.Contains(result.Warnings, w => w.Contains("Cavern"));
    }

    [Fact]
    public void Parse_RejectsAFileWithoutZones()
        => Assert.Throws<InvalidDataException>(() => H3TImporter.ParseLines(["just", "some\ttext"]));

    [Fact]
    public void Parse_WarnsWhenNoPlayerStartIsMarked()
    {
        string[] file = [ZoneRow(4, 1, "Mid"), ZoneRow(4, 2, "Mid2"), ConnectionRow(1, 2, 100)];

        H3TImportResult result = H3TImporter.ParseLines(file);

        Assert.Contains(result.Warnings, w => w.Contains("No player start"));
    }
}
