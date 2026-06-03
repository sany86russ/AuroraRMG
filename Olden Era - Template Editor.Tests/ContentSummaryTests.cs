using System.Collections.Generic;
using System.Linq;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Analysis;
using Olden_Era___Template_Editor.Services.Generation;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public class ContentSummaryTests
{
    private static RmgTemplate Template(List<Zone> zones, List<Connection> connections) =>
        new() { Variants = [new Variant { Zones = zones, Connections = connections }] };

    private static Zone Spawn(string name, string player, int treasure = 0, int resources = 0) =>
        new() { Name = name, GuardedContentValue = treasure, ResourcesValue = resources,
                MainObjects = [new MainObject { Type = "Spawn", Spawn = player }] };

    private static Zone Castle(string name, int treasure = 0) =>
        new() { Name = name, GuardedContentValue = treasure, MainObjects = [new MainObject { Type = "City" }] };

    private static Zone Hub(string name) => new() { Name = name, Layout = "zone_layout_center" };

    private static Zone Neutral(string name, int treasure = 0, int resources = 0) =>
        new() { Name = name, GuardedContentValue = treasure, ResourcesValue = resources };

    private static Connection Conn(string a, string b) => new() { From = a, To = b };

    [Fact]
    public void Analyze_ClassifiesRolesAndCountsThem()
    {
        var summary = TemplateContentSummary.Analyze(Template(
            [Spawn("P1", "Player1"), Spawn("P2", "Player2"), Castle("C"), Neutral("N"), Hub("H")],
            []));

        Assert.Equal(5, summary.ZoneCount);
        Assert.Equal(2, summary.PlayerZones);
        Assert.Equal(1, summary.CastleZones);
        Assert.Equal(2, summary.NeutralZones); // plain neutral + hub
    }

    [Fact]
    public void Analyze_SumsTreasureAndResources()
    {
        var p1 = Spawn("P1", "Player1", treasure: 100, resources: 10);
        p1.UnguardedContentValue = 50; // treasure also counts unguarded loot
        var summary = TemplateContentSummary.Analyze(Template(
            [p1, Neutral("N", treasure: 200, resources: 20)],
            []));

        Assert.Equal(350, summary.TotalTreasure);   // (100 + 50) + 200
        Assert.Equal(30, summary.TotalResources);    // 10 + 20
    }

    [Fact]
    public void Analyze_CountsConnectionsAndPerZoneDegree()
    {
        var summary = TemplateContentSummary.Analyze(Template(
            [Spawn("P1", "Player1"), Neutral("N"), Spawn("P2", "Player2")],
            [Conn("P1", "N"), Conn("N", "P2")]));

        Assert.Equal(2, summary.ConnectionCount);
        Assert.Equal(2, summary.Zones.Single(z => z.Name == "N").Connections);
        Assert.Equal(1, summary.Zones.Single(z => z.Name == "P1").Connections);
    }

    [Fact]
    public void Analyze_NullOrEmpty_IsZeroed_AndDoesNotThrow()
    {
        Assert.Equal(0, TemplateContentSummary.Analyze(null).ZoneCount);
        Assert.Equal(0, TemplateContentSummary.Analyze(new RmgTemplate()).ZoneCount);
    }

    [Fact]
    public void Analyze_GeneratedQuickMaps_ProduceSaneSummaries()
    {
        foreach (int players in new[] { 2, 4 })
        foreach (QuickMapScale scale in new[] { QuickMapScale.Medium, QuickMapScale.Large })
        for (int seed = 0; seed < 4; seed++)
        {
            GeneratorSettings settings = RandomTemplateBuilder.Build(new QuickGenerateOptions
            {
                Seed = seed * 13 + players, PlayerCount = players, GameType = QuickGameType.FreeForAll,
                Scale = scale, Length = QuickGameLength.Medium, Chaos = QuickChaos.Normal,
            });
            RmgTemplate tpl = TemplateGenerator.Generate(settings);
            ContentSummary summary = TemplateContentSummary.Analyze(tpl);
            string id = $"p={players}/{scale}/seed={seed}";

            Assert.Equal(settings.PlayerCount, summary.PlayerZones);
            Assert.True(summary.ZoneCount >= settings.PlayerCount, $"{id}: only {summary.ZoneCount} zones");
            Assert.True(summary.ConnectionCount > 0, $"{id}: no connections");
            Assert.True(summary.TotalTreasure >= 0 && summary.TotalResources >= 0, $"{id}: negative totals");
        }
    }
}
