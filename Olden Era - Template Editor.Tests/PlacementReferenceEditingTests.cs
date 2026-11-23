using System.Text.Json;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Analysis;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public sealed class PlacementReferenceEditingTests
{
    [Fact]
    public void RenamingAndRemovingAConnectionUpdateLocalPlacementAndIsolateSharedGroups()
    {
        var template = SharedFixture();
        var variant = template.Variants![0];
        var outside = template.Variants[1];
        var zone = variant.Zones![0];
        var authored = template.MandatoryContent![0];

        TemplateRefactor.ApplyConnectionEdits(variant, [new() { Name = "Renamed", From = "Start", To = "Treasure" }], template);

        Assert.Equal(["Renamed", "true", "0.4", "0"], zone.MainObjects![1].PlacementArgs);
        Assert.Equal("Renamed", Assert.Single(zone.Roads!).To!.Args![0]);
        var local = FindGroup(template, zone);
        Assert.NotEqual(authored.Name, local.Name);
        Assert.Equal("Renamed", FindRule(local, "Connection").Args![0]);
        Assert.Equal("Player2", Assert.Single(local.Content!).Extra!["owner"].GetString());
        Assert.Equal("Gate", FindRule(FindGroup(template, outside.Zones![0]), "Connection").Args![0]);
        Assert.Equal("Gate", Assert.Single(outside.Connections!).Name);
        Assert.Empty(TemplateCompatibilityValidator.Validate(template));

        TemplateRefactor.RemoveConnection(variant, Assert.Single(variant.Connections!), template);

        Assert.Equal("Uniform", zone.MainObjects[1].Placement);
        Assert.Null(zone.MainObjects[1].PlacementArgs);
        Assert.Empty(zone.Roads!);
        Assert.DoesNotContain(FindGroup(template, zone).Content![0].Rules!, rule => rule.Type == "Connection");
        Assert.Equal("Gate", FindRule(authored, "Connection").Args![0]);
    }

    [Fact]
    public void RemovingAMainObjectReindexesNearbySpiresOnlyInAffectedConsumers()
    {
        var template = SharedFixture();
        var variant = template.Variants![0];
        var zone = variant.Zones![0];
        var outside = template.Variants[1].Zones![0];
        Assert.Equal("2", FindRule(FindGroup(template, zone), "MainObject").Args![0]);

        TemplateRefactor.RemoveMainObjects(variant, [(zone, 1)], template);

        Assert.Equal(2, zone.MainObjects!.Count);
        Assert.Equal("1", FindRule(FindGroup(template, zone), "MainObject").Args![0]);
        Assert.Equal("2", FindRule(FindGroup(template, outside), "MainObject").Args![0]);
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(template), issue => issue.Code == "placement.mainObjectTarget");
    }

    [Fact]
    public void ConflictingSharedDefinitionsRejectConnectionAndMirroredObjectEditsBeforeMutation()
    {
        var template = SharedFixture();
        var variant = template.Variants![0];
        var a = variant.Zones![0];
        var b = variant.Zones[1];
        b.MandatoryContent = ["Ambiguous"];
        b.MainObjects = [new() { Type = "City" }, new() { Type = "City" }, new() { Type = "City" }];
        for (int i = 0; i < 2; i++)
            template.MandatoryContent!.Add(new()
            {
                Name = "Ambiguous",
                Content =
                [
                    new() { Sid = "mine_wood", Rules = [new() { Type = "Connection", Args = ["Gate"] }, new() { Type = "MainObject", Args = ["2"] }] }
                ]
            });
        string before = JsonSerializer.Serialize(template, JsonExport.Options);

        Assert.Throws<InvalidOperationException>(() => TemplateRefactor.ApplyConnectionEdits(variant,
            [new() { Name = "New", From = "Start", To = "Treasure" }], template));
        Assert.Equal(before, JsonSerializer.Serialize(template, JsonExport.Options));
        Assert.Throws<InvalidOperationException>(() => TemplateRefactor.RemoveConnection(variant, Assert.Single(variant.Connections!), template));
        Assert.Equal(before, JsonSerializer.Serialize(template, JsonExport.Options));
        Assert.Throws<InvalidOperationException>(() => TemplateRefactor.RemoveMainObjects(variant, [(a, 1), (b, 1)], template));
        Assert.Equal(before, JsonSerializer.Serialize(template, JsonExport.Options));
    }

    [Fact]
    public void ValidationUsesConnectionsAndMainObjectIndicesOfTheGroupConsumer()
    {
        var template = SharedFixture();
        var variant = template.Variants![0];
        var zone = variant.Zones![0];
        variant.Zones.Add(new() { Name = "Far" });
        variant.Connections!.Add(new() { Name = "Far Gate", From = "Treasure", To = "Far", ConnectionType = "Direct" });
        zone.MainObjects![1].PlacementArgs![0] = "Far Gate";
        var group = template.MandatoryContent![0];
        FindRule(group, "Connection").Args![0] = "Far Gate";
        FindRule(group, "MainObject").Args![0] = "4";

        var issues = TemplateCompatibilityValidator.Validate(template);
        Assert.Contains(issues, issue => issue is { Code: "placement.objectConnectionTarget", Severity: TemplateIssueSeverity.Warning });
        Assert.Contains(issues, issue => issue is { Code: "placement.connectionTarget", Path: "variants[0].zones[0].mandatoryContent[0]" });
        Assert.Contains(issues, issue => issue is { Code: "placement.mainObjectTarget", Path: "variants[0].zones[0].mandatoryContent[0]" });
    }

    [Fact]
    public void GeneratingCastlelessNeutralContentPreservesFieldsOutsideItsInapplicablePlacementRule()
    {
        const string item = """
            {"name":"Existing encounter","sid":"mine_wood","owner":"Player2","designatedEncounter":false,
             "road":false,"future":{"value":17},"rules":[{"type":"MainObject","args":["0"],"weight":1},{"type":"Road","weight":1}]}
            """;
        var input = JsonSerializer.Deserialize<ContentItem>(item, JsonExport.Options)!;
        var settings = new GeneratorSettings
        {
            Seed = 1729, SpawnRemoteFootholds = false, ProfileOptions = GenerationProfileOptions.CurrentDefaults(),
            LowNeutralMandatoryContent = [input], ZoneCfg = new() { Advanced = new() { Enabled = true, NeutralLowNoCastleCount = 1 } }
        };
        settings.ProfileOptions.Tempo = MapTempo.Legacy;
        var template = TemplateGenerator.Generate(settings);
        var actual = Assert.Single(template.MandatoryContent!.SelectMany(group => group.Content!), value => value.Name == input.Name);
        Assert.Equal("Road", Assert.Single(actual.Rules!).Type);
        Assert.NotSame(input.Extra, actual.Extra);
        Assert.Equal(input.Extra!.Keys, actual.Extra!.Keys);
        foreach (var (key, expected) in input.Extra)
            Assert.Equal(expected.GetRawText(), actual.Extra[key].GetRawText());
        Assert.Equal(2, input.Rules!.Count);
    }

    [Fact]
    public void GeneratedFootholdUsesTheNeutralCastleCount()
    {
        var settings = new GeneratorSettings
        {
            Seed = 1729, PlayerStartsWithCastles = true, ProfileOptions = GenerationProfileOptions.CurrentDefaults(),
            ZoneCfg = new() { PlayerZoneCastles = 2, NeutralZoneCastles = 1, Advanced = new() { Enabled = true, NeutralLowCastleCount = 1 } }
        };
        settings.ProfileOptions.Tempo = MapTempo.Legacy;
        var template = TemplateGenerator.Generate(settings);
        var neutral = Assert.Single(Assert.Single(template.Variants!).Zones!, zone => zone.Name.StartsWith("Neutral-", StringComparison.Ordinal));
        Assert.Single(neutral.MainObjects!);
        var foothold = Assert.Single(FindGroup(template, neutral).Content!, item => item.Sid == "remote_foothold");
        Assert.Equal("0", Assert.Single(foothold.Rules!, rule => rule.Type == "MainObject").Args![0]);
    }

    private static MandatoryContentGroup FindGroup(RmgTemplate template, Zone zone) =>
        Assert.Single(template.MandatoryContent!, group => group.Name == Assert.Single(zone.MandatoryContent!));

    private static ContentPlacementRule FindRule(MandatoryContentGroup group, string type) =>
        Assert.Single(Assert.Single(group.Content!).Rules!, rule => rule.Type == type);

    private static RmgTemplate SharedFixture()
    {
        const string json = """
            {
              "name":"Stock-style placement","sizeX":128,"sizeZ":128,
              "mandatoryContent":[{"name":"Shared","content":[{
                "name":"spire","sid":"gladiator_spire","owner":"Player2",
                "rules":[{"type":"Connection","args":["Gate"],"weight":1},{"type":"MainObject","args":["2"],"weight":1}]
              }]}],
              "variants":[{"zones":[
                {"name":"Start","mainObjects":[
                  {"type":"Spawn","spawn":"Player1"},
                  {"type":"City","placement":"Connection","placementArgs":["Gate","true","0.4","0"]},
                  {"type":"City","placement":"Uniform"}
                ], "roads":[{"type":"Stone","from":{"type":"MainObject","args":["0"]},"to":{"type":"Connection","args":["Gate"]}}],
                "mandatoryContent":["Shared"]},
                {"name":"Treasure"}
              ], "connections":[{"name":"Gate","from":"Start","to":"Treasure","connectionType":"Direct"}]}]
            }
            """;
        var template = TemplateJsonReader.Deserialize(json, JsonExport.Options)!;
        template.Variants!.Add(TemplateRefactor.CreateEditingCopy(template).Variants![0]);
        return template;
    }
}
