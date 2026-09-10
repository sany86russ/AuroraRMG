using System.Text.Json;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services;

namespace Olden_Era___Template_Editor.Tests;

public class FactionSelectorTests
{
    [Fact]
    public void ArgumentsPreserveExpressionsAndNamesWithSpacesOrCommas()
    {
        Assert.Equal(new[] { "differentFrom: 0 Spawn-A", "differentFrom: 1 My zone, east", "Human" },
            FactionSelectors.ParseArguments("differentFrom: 0 Spawn-A\r\n  differentFrom: 1 My zone, east\n\nHuman  "));
    }

    [Theory]
    [InlineData(false, "Match")]
    [InlineData(true, "FromList")]
    public void ReferenceHasGameShapeAndKeepsTownNeutral(bool different, string type)
    {
        var town = new MainObject { Type = "City", Faction = new TypedSelector() };
        var zones = new List<Zone> { new() { Name = "Spawn-A", MainObjects = [new() { Type = "Spawn", Spawn = "Player1" }] } };
        FactionSelectors.SetReference(town, zones, "Spawn-A", 0, different);
        Assert.Null(town.Owner);
        Assert.Equal("City", town.Type);
        var json = JsonSerializer.Serialize(town, JsonExport.Options);
        var restored = JsonSerializer.Deserialize<MainObject>(json, JsonExport.Options)!;
        Assert.Equal(type, restored.Faction!.Type);
        Assert.Equal(different ? new[] { "differentFrom: 0 Spawn-A" } : new[] { "0", "Spawn-A" }, restored.Faction.Args);
    }

    [Fact]
    public void ChangingTypePreservesAllArgumentsAndUnknownFields()
    {
        var town = JsonSerializer.Deserialize<MainObject>("""{"type":"City","owner":"Player2","faction":{"type":"FromList","args":["Human","differentFrom: 0 Spawn-A"],"futureField":42}}""", JsonExport.Options)!;
        FactionSelectors.SetType(town, "Match");
        Assert.Equal(new[] { "Human", "differentFrom: 0 Spawn-A" }, town.Faction!.Args);
        Assert.Equal("Player2", town.Owner);
        Assert.Contains("\"futureField\":42", JsonSerializer.Serialize(town, JsonExport.Options).Replace(" ", ""));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void InvalidReferenceDoesNotMutateTown(int index)
    {
        var town = new MainObject { Faction = new TypedSelector { Type = "Random", Args = [] } };
        var zones = new List<Zone> { new() { Name = "Spawn-A", MainObjects = [new()] } };
        Assert.Throws<ArgumentException>(() => FactionSelectors.SetReference(town, zones, "Spawn-A", index, false));
        Assert.Equal("Random", town.Faction.Type);
    }

    [Fact]
    public void SelfReferenceIsRejected()
    {
        var town = new MainObject();
        Assert.Throws<ArgumentException>(() => FactionSelectors.SetReference(town,
            [new Zone { Name = "A", MainObjects = [town] }], "A", 0, false));
        Assert.Null(town.Faction);
    }

    [Fact]
    public void ZoneRenameUpdatesMatchAndEveryExclusionWithoutReplacingOtherArguments()
    {
        var match = new TypedSelector { Type = "Match", Args = ["0", "Spawn-A"] };
        var exclude = new TypedSelector { Type = "FromList", Args = ["Human", "differentFrom: 0 Spawn-A", "differentFrom: 1 Spawn-A", "differentFrom: 0 Spawn-AB"] };
        var variant = new Variant { Zones = [new() { Name = "Neutral", MainObjects = [new() { Faction = match }, new() { Faction = exclude }] }] };
        TemplateRefactor.RenameZoneReferences(variant, "Spawn-A", "New spawn");
        Assert.Equal(new[] { "0", "New spawn" }, match.Args);
        Assert.Equal(new[] { "Human", "differentFrom: 0 New spawn", "differentFrom: 1 New spawn", "differentFrom: 0 Spawn-AB" }, exclude.Args);
    }

    [Theory]
    [InlineData("Match", "0", "Missing")]
    [InlineData("Match", "9", "Spawn-A")]
    [InlineData("FromList", "differentFrom: 9 Spawn-A", null)]
    public void ValidatorReportsUnavailableFactionSources(string type, string first, string? second)
    {
        var zone = new Zone { Name = "Spawn-A", MainObjects = [new() { Type = "Spawn" },
            new() { Faction = new TypedSelector { Type = type, Args = second is null ? [first] : [first, second] } }] };
        Assert.Single(FactionSelectors.ValidateReferences([zone]));
    }

    [Fact]
    public void ValidatorAllowsLocalMatchButReportsCycles()
    {
        var source = new MainObject { Type = "Spawn" };
        var town = new MainObject { Faction = new TypedSelector { Type = "Match", Args = ["0"] } };
        var zone = new Zone { Name = "A", MainObjects = [source, town] };
        Assert.Empty(FactionSelectors.ValidateReferences([zone]));
        source.Faction = new TypedSelector { Type = "Match", Args = ["1"] };
        Assert.Single(FactionSelectors.ValidateReferences([zone]));
    }

    [Fact]
    public void InvalidNullReferenceReportsIssueInsteadOfCrashing()
    {
        var zone = new Zone { Name = "A", MainObjects = [new() { Faction = new TypedSelector { Type = "Match", Args = ["0", null!] } }] };
        Assert.Single(FactionSelectors.ValidateReferences([zone]));
    }
}
