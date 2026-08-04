using System.Collections.Generic;
using System.Linq;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services;

namespace Olden_Era___Template_Editor.Tests;

public class ZoneGraphValidatorTests
{
    private static Zone Z(string name, string? layout = null) => new() { Name = name, Layout = layout };
    private static Connection C(string from, string to, string? name = null) => new() { From = from, To = to, Name = name };

    /// <summary>A zone carrying the given player's start, so the graph passes the spawn-chain check.</summary>
    private static Zone Spawn(string name, string player) => new()
    {
        Name = name,
        MainObjects = [new MainObject { Type = "Spawn", Spawn = player }],
    };

    [Fact]
    public void ValidGraph_HasNoIssues()
    {
        var zones = new List<Zone> { Spawn("Spawn-A", "Player1"), Spawn("Spawn-B", "Player2") };
        var conns = new List<Connection> { C("Spawn-A", "Spawn-B") };
        Assert.Empty(ZoneGraphValidator.Validate(zones, conns));
    }

    [Fact]
    public void MissingSpawn_IsReported()
    {
        var zones = new List<Zone> { Z("A"), Z("B") };
        var conns = new List<Connection> { C("A", "B") };
        Assert.Contains(ZoneGraphValidator.Validate(zones, conns), i => i.Contains("старта игрока"));
    }

    [Fact]
    public void DuplicateSpawn_IsReported()
    {
        var zones = new List<Zone> { Spawn("Spawn-A", "Player1"), Spawn("Spawn-B", "Player1") };
        var conns = new List<Connection> { C("Spawn-A", "Spawn-B") };
        Assert.Contains(ZoneGraphValidator.Validate(zones, conns), i => i.Contains("более чем одной"));
    }

    [Fact]
    public void GapInThePlayerChain_IsReported()
    {
        // Player1 + Player3 with no Player2 leaves side 2 without a starting town.
        var zones = new List<Zone> { Spawn("Spawn-A", "Player1"), Spawn("Spawn-C", "Player3") };
        var conns = new List<Connection> { C("Spawn-A", "Spawn-C") };
        Assert.Contains(ZoneGraphValidator.Validate(zones, conns), i => i.Contains("Player2"));
    }

    [Fact]
    public void SplitGraph_IsReportedAsUnreachable()
    {
        var zones = new List<Zone>
        {
            Spawn("Spawn-A", "Player1"), Z("N-1"),
            Spawn("Spawn-B", "Player2"), Z("N-2"),
        };
        var conns = new List<Connection> { C("Spawn-A", "N-1", "c1"), C("Spawn-B", "N-2", "c2") };
        Assert.Contains(ZoneGraphValidator.Validate(zones, conns), i => i.Contains("разорвана"));
    }

    [Fact]
    public void RoadPointingAtAMissingConnection_IsReported()
    {
        var zones = new List<Zone> { Spawn("Spawn-A", "Player1"), Spawn("Spawn-B", "Player2") };
        zones[0].Roads =
        [
            new Road
            {
                From = new RoadEndpoint { Type = "MainObject", Args = ["0"] },
                To = new RoadEndpoint { Type = "Connection", Args = ["Ghost-Link"] },
            },
        ];
        var conns = new List<Connection> { C("Spawn-A", "Spawn-B", "Bridge") };
        Assert.Contains(ZoneGraphValidator.Validate(zones, conns), i => i.Contains("Ghost-Link"));
    }

    [Fact]
    public void ZoneBiomeCopyingItself_IsReported()
    {
        var zones = new List<Zone> { Spawn("Spawn-A", "Player1"), Spawn("Spawn-B", "Player2") };
        zones[0].ZoneBiome = new BiomeSelector { Type = "MatchZone", Args = ["Spawn-A"] };
        var conns = new List<Connection> { C("Spawn-A", "Spawn-B") };
        Assert.Contains(ZoneGraphValidator.Validate(zones, conns), i => i.Contains("сам с себя"));
    }

    [Fact]
    public void DanglingConnection_IsReported()
    {
        var zones = new List<Zone> { Z("Spawn-A"), Z("Spawn-B") };
        var conns = new List<Connection> { C("Spawn-A", "Ghost") };
        var issues = ZoneGraphValidator.Validate(zones, conns);
        Assert.Contains(issues, i => i.Contains("Ghost"));
    }

    [Fact]
    public void DuplicateZoneName_IsReported()
    {
        var zones = new List<Zone> { Z("Spawn-A"), Z("Spawn-A") };
        var conns = new List<Connection>();
        Assert.Contains(ZoneGraphValidator.Validate(zones, conns), i => i.Contains("Дублирующееся"));
    }

    [Fact]
    public void SelfLoop_IsReported()
    {
        var zones = new List<Zone> { Z("Hub"), Z("Spawn-A") };
        var conns = new List<Connection> { C("Hub", "Hub"), C("Hub", "Spawn-A") };
        Assert.Contains(ZoneGraphValidator.Validate(zones, conns), i => i.Contains("сама на себя"));
    }

    [Fact]
    public void IsolatedZone_IsReported()
    {
        var zones = new List<Zone> { Z("Spawn-A"), Z("Spawn-B"), Z("Lonely") };
        var conns = new List<Connection> { C("Spawn-A", "Spawn-B") };
        Assert.Contains(ZoneGraphValidator.Validate(zones, conns), i => i.Contains("Lonely"));
    }

    [Fact]
    public void SingleZone_IsNotFlaggedAsIsolated()
    {
        var zones = new List<Zone> { Spawn("Solo", "Player1") };
        Assert.Empty(ZoneGraphValidator.Validate(zones, new List<Connection>()));
    }

    [Fact]
    public void DuplicateConnectionName_IsReported()
    {
        var zones = new List<Zone> { Z("A"), Z("B"), Z("C") };
        var conns = new List<Connection> { C("A", "B", "Bridge"), C("B", "C", "Bridge") };
        Assert.Contains(ZoneGraphValidator.Validate(zones, conns),
            i => i.Contains("имя связи") && i.Contains("Bridge"));
    }

    [Fact]
    public void UniqueConnectionNames_AreNotFlagged()
    {
        var zones = new List<Zone> { Z("A"), Z("B"), Z("C") };
        var conns = new List<Connection> { C("A", "B", "Bridge-1"), C("B", "C", "Bridge-2") };
        Assert.DoesNotContain(ZoneGraphValidator.Validate(zones, conns), i => i.Contains("имя связи"));
    }

    [Fact]
    public void EmptyConnectionNames_AreNotFlaggedAsDuplicates()
    {
        var zones = new List<Zone> { Z("A"), Z("B"), Z("C") };
        var conns = new List<Connection> { C("A", "B"), C("B", "C") };
        Assert.DoesNotContain(ZoneGraphValidator.Validate(zones, conns), i => i.Contains("имя связи"));
    }
}
