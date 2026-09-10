using System.Text.Json;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services;

namespace Olden_Era___Template_Editor.Tests;

public class EditorReferenceLifecycleTests
{
    static MainObject Match(params string[] args) => new() { Type = "City", Faction = new TypedSelector { Type = "Match", Args = args.ToList() } };
    static MainObject Exclude(params string[] args) => new() { Type = "City", Faction = new TypedSelector { Type = "FromList", Args = args.ToList() } };

    [Theory]
    [InlineData("Match")]
    [InlineData("FromList")]
    [InlineData("Random")]
    public void CapturableKeepsFactionAndClearsStartIdentity(string type)
    {
        var town = Match("0", "Spawn-A"); town.Faction!.Type = type;
        town.Type = "Spawn"; town.Spawn = "Player1"; town.Owner = "Player1"; town.IsStartCity = true;
        town.GuardChance = 0; town.GuardValue = 0;
        var rule = JsonSerializer.Serialize(town.Faction, JsonExport.Options);
        TemplateRefactor.MakeCapturable(town);
        Assert.Equal(rule, JsonSerializer.Serialize(town.Faction, JsonExport.Options));
        Assert.Equal("City", town.Type); Assert.Null(town.Spawn); Assert.Null(town.Owner); Assert.False(town.IsStartCity);
        Assert.Equal(1, town.GuardChance); Assert.Equal(5000, town.GuardValue);
    }

    [Fact]
    public void CapturableDoesNotOverwriteCustomGuard()
    {
        var town = new MainObject { GuardValue = 8800, GuardChance = .7, GuardWeeklyIncrement = .2 };
        TemplateRefactor.MakeCapturable(town);
        Assert.Equal(8800, town.GuardValue); Assert.Equal(.7, town.GuardChance); Assert.Equal(.2, town.GuardWeeklyIncrement);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DeletingObjectReindexesLocalAndExternalReferencesAndRoads(bool explicitZone)
    {
        var match = explicitZone ? Match("2", "A") : Match("2");
        var exclusion = Exclude("Human", "differentFrom: 2 A", "differentFrom: 1 A");
        var a = new Zone { Name = "A", MainObjects = [new(), new(), new(), match], Roads = [
            new() { From = new() { Type = "MainObject", Args = ["1"] }, To = new() { Type = "MainObject", Args = ["0"] } },
            new() { From = new() { Type = "MainObject", Args = ["2"] }, To = new() { Type = "MainObject", Args = ["0"] } }] };
        var b = new Zone { Name = "B", MainObjects = [exclusion, Match("1", "A"), Match("2", "A")] };
        var variant = new Variant { Zones = [a,b] };
        Assert.Equal(2, TemplateRefactor.RemoveMainObject(variant, a, 1));
        Assert.Equal("1", match.Faction!.Args![0]);
        Assert.Equal(new[] {"Human", "differentFrom: 1 A"}, exclusion.Faction!.Args);
        Assert.Equal("Random", b.MainObjects[1].Faction!.Type);
        Assert.Equal(new[] {"1", "A"}, b.MainObjects[2].Faction!.Args);
        Assert.Single(a.Roads); Assert.Equal("1", a.Roads[0].From!.Args![0]);
        Assert.Empty(FactionSelectors.ValidateReferences(variant.Zones));
    }

    [Fact]
    public void InvalidDeletionLeavesDocumentUntouched()
    {
        var zone = new Zone {Name="A", MainObjects=[new()]}; var variant=new Variant {Zones=[zone]};
        var before=JsonSerializer.Serialize(variant,JsonExport.Options);
        Assert.Throws<ArgumentOutOfRangeException>(()=>TemplateRefactor.RemoveMainObject(variant,zone,1));
        Assert.Equal(before,JsonSerializer.Serialize(variant,JsonExport.Options));
    }

    [Fact]
    public void DeletingZoneClearsOnlyRulesUsingThatZone()
    {
        var a=new Zone {Name="A",MainObjects=[new()]};
        var b=new Zone {Name="B",MainObjects=[Match("0","A"),Exclude("Human","differentFrom: 0 A","differentFrom: 0 C")]};
        var variant=new Variant {Zones=[a,b,new(){Name="C",MainObjects=[new()]}]};
        TemplateRefactor.RemoveZoneReferences(variant,"A"); variant.Zones.Remove(a);
        Assert.Equal("Random",b.MainObjects[0].Faction!.Type);
        Assert.Equal(new[]{"Human","differentFrom: 0 C"},b.MainObjects[1].Faction!.Args);
        Assert.Empty(FactionSelectors.ValidateReferences(variant.Zones));
    }

    [Fact]
    public void RemovingLastExclusionFallsBackToRandomFaction()
    {
        var obj=Exclude("differentFrom: 0 A");
        Assert.Equal(1,FactionSelectors.RemoveSource(obj.Faction,"B","A",null));
        Assert.Equal("Random",obj.Faction!.Type);Assert.Empty(obj.Faction.Args!);
    }

    [Fact]
    public void MirroringSwapsReferencesOnceAndKeepsAxisReferences()
    {
        var names=new Dictionary<string,string>{{"Left","Right"},{"Right","Left"}};
        var match=Match("0","Left"); var exclude=Exclude("Human","differentFrom: 0 Left","differentFrom: 1 Right","differentFrom: 0 Axis");
        FactionSelectors.RemapZones(match.Faction,names); FactionSelectors.RemapZones(exclude.Faction,names);
        Assert.Equal(new[]{"0","Right"},match.Faction!.Args);
        Assert.Equal(new[]{"Human","differentFrom: 0 Right","differentFrom: 1 Left","differentFrom: 0 Axis"},exclude.Faction!.Args);
    }

    [Fact]
    public void PastingRedirectsInternalFactionReferencesAndClearsDuplicateStartFlag()
    {
        var original=new Zone {Name="A",MainObjects=[new(){Owner="Player1"}]};
        var copy=new Zone {Name="A-copy",MainObjects=[new(){Owner="Player1",IsStartCity=true}, Match("0","A"),Match("0","B")]};
        TemplateRefactor.PreparePastedZone(copy,[original],"A");
        Assert.Null(copy.MainObjects[0].Owner); Assert.False(copy.MainObjects[0].IsStartCity);
        Assert.Equal(new[]{"0","A-copy"},copy.MainObjects[1].Faction!.Args);
        Assert.Equal(new[]{"0","B"},copy.MainObjects[2].Faction!.Args);
    }
}
