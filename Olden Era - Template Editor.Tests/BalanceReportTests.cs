using System.Collections.Generic;
using System.Linq;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Analysis;
using Olden_Era___Template_Editor.Services.Generation;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public class BalanceReportTests
{
    // ── Hand-built templates: deterministic, language-independent assertions ──────

    private static RmgTemplate Template(List<Zone> zones, List<Connection> connections) =>
        new() { Variants = [new Variant { Zones = zones, Connections = connections }] };

    private static Zone Spawn(string name, string player, int wealth) =>
        new() { Name = name, GuardedContentValue = wealth, MainObjects = [new MainObject { Type = "Spawn", Spawn = player }] };

    private static Zone Neutral(string name, int wealth, bool castle = false) =>
        new()
        {
            Name = name,
            GuardedContentValue = wealth,
            MainObjects = castle ? [new MainObject { Type = "City" }] : null,
        };

    private static Connection Conn(string a, string b) => new() { From = a, To = b };

    [Fact]
    public void Analyze_SymmetricTwoPlayers_ScoresPerfect_AndReportsWellBalanced()
    {
        // P1 — N — P2, both starts identical: nothing to fault.
        var report = TemplateBalanceReport.Analyze(Template(
            [Spawn("P1", "Player1", 500), Spawn("P2", "Player2", 500), Neutral("N", 200)],
            [Conn("P1", "N"), Conn("P2", "N")]));

        Assert.True(report.Applicable);
        Assert.Equal(100, report.Score);
        Assert.Equal(2, report.Players.Count);
        Assert.Contains(report.Findings, f => f.Key == "S.Bal.Find.WellBalanced");
        Assert.DoesNotContain(report.Findings, f => f.Severity == BalanceSeverity.Warning);
    }

    [Fact]
    public void Analyze_LopsidedStart_FlagsThePoorerPlayer_AndLowersScore()
    {
        var report = TemplateBalanceReport.Analyze(Template(
            [Spawn("P1", "Player1", 1000), Spawn("P2", "Player2", 100), Neutral("N", 200)],
            [Conn("P1", "N"), Conn("P2", "N")]));

        Assert.True(report.Applicable);
        Assert.True(report.Score < 100, $"expected an imbalance penalty, got {report.Score}");

        BalanceFinding poor = Assert.Single(report.Findings, f => f.Key == "S.Bal.Find.PoorStart");
        Assert.Equal(BalanceSeverity.Warning, poor.Severity);
        Assert.Equal(2, (int)poor.Args[0]);   // Player2 is the poorer one
        Assert.Equal(90, (int)poor.Args[1]);  // 90% poorer
    }

    [Fact]
    public void Analyze_UnevenCastleAccess_IsFlagged()
    {
        // P1 sits next to the neutral castle (1 hop) while P2 is twice as far (via Mid) — uneven access.
        var report = TemplateBalanceReport.Analyze(Template(
            [Spawn("P1", "Player1", 500), Spawn("P2", "Player2", 500), Neutral("Castle", 300, castle: true), Neutral("Mid", 100)],
            [Conn("P1", "Castle"), Conn("Castle", "Mid"), Conn("Mid", "P2")]));

        Assert.True(report.Applicable);
        Assert.Contains(report.Findings, f => f.Key == "S.Bal.Find.CastleUneven");
    }

    [Fact]
    public void Analyze_SinglePlayer_IsNotApplicable()
    {
        var report = TemplateBalanceReport.Analyze(Template(
            [Spawn("P1", "Player1", 500), Neutral("N", 200)],
            [Conn("P1", "N")]));

        Assert.False(report.Applicable);
        Assert.Empty(report.Players);
    }

    [Fact]
    public void Analyze_NullOrEmptyTemplate_IsNotApplicable_AndDoesNotThrow()
    {
        Assert.False(TemplateBalanceReport.Analyze(null).Applicable);
        Assert.False(TemplateBalanceReport.Analyze(new RmgTemplate()).Applicable);
    }

    // ── Smoke test over real generated maps ───────────────────────────────────────

    [Fact]
    public void Analyze_GeneratedQuickMaps_ProduceSaneReports()
    {
        foreach (int players in new[] { 2, 4, 6 })
        foreach (QuickMapScale scale in new[] { QuickMapScale.Medium, QuickMapScale.Large })
        for (int seed = 0; seed < 5; seed++)
        {
            GeneratorSettings settings = RandomTemplateBuilder.Build(new QuickGenerateOptions
            {
                Seed = seed * 17 + players, PlayerCount = players, GameType = QuickGameType.FreeForAll,
                Scale = scale, Length = QuickGameLength.Medium, Chaos = QuickChaos.Normal,
            });
            RmgTemplate tpl = TemplateGenerator.Generate(settings);
            BalanceReport report = TemplateBalanceReport.Analyze(tpl);
            string id = $"p={players}/{scale}/seed={seed}";

            Assert.True(report.Applicable, $"{id}: expected an applicable report");
            Assert.InRange(report.Score, 0, 100);
            Assert.Equal(settings.PlayerCount, report.Players.Count);
            Assert.All(report.Players, p =>
            {
                Assert.True(p.StartWealth >= 0, $"{id}: negative start wealth");
                Assert.True(p.NearestOpponentHops >= 1, $"{id}: opponent at {p.NearestOpponentHops} hops");
            });
        }
    }
}
