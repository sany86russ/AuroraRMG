using System.Collections.Generic;
using System.Linq;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services;

namespace Olden_Era___Template_Editor.Tests;

public class ZoneGraphValidatorTests
{
    private static Zone Z(string name, string? layout = null) => new() { Name = name, Layout = layout };
    private static Connection C(string from, string to) => new() { From = from, To = to };

    [Fact]
    public void ValidGraph_HasNoIssues()
    {
        var zones = new List<Zone> { Z("Spawn-A"), Z("Spawn-B") };
        var conns = new List<Connection> { C("Spawn-A", "Spawn-B") };
        Assert.Empty(ZoneGraphValidator.Validate(zones, conns));
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
        var zones = new List<Zone> { Z("Solo") };
        Assert.Empty(ZoneGraphValidator.Validate(zones, new List<Connection>()));
    }
}
