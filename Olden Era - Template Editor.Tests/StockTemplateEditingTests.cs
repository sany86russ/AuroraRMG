using System.Text.Json;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Analysis;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public sealed class StockTemplateEditingTests
{
    [Fact]
    public void CountLimitsRetainListAndInlineTargetsWithoutAddingAnEmptySid()
    {
        const string json = """
            {
              "name":"Stock shape","sizeX":128,"sizeZ":128,
              "contentLists":[{"name":"Included","content":[{"sid":"bank"}]}],
              "contentCountLimits":[{
                "name":"Limit",
                "limits":[
                  {"includeLists":"Included","maxCount":3},
                  {"includeLists":["Included"],"content":[{"sid":"bank","variant":2}],"maxCount":4},
                  {"sid":"bank","variant":2,"maxCount":0}
                ]
              }],
              "variants":[{"zones":[{"name":"Start","mainObjects":[{"type":"Spawn","spawn":"Player1"}]}]}]
            }
            """;
        var template = TemplateJsonReader.Deserialize(json, JsonExport.Options)!;
        var limits = Assert.Single(template.ContentCountLimits!).Limits!;
        Assert.Null(limits[0].Sid);
        Assert.Null(limits[1].Sid);
        Assert.Equal(2, Assert.Single(limits[1].Content!).Variant);
        Assert.Equal(2, limits[2].Variant);
        Assert.Equal(0, limits[2].MaxCount);
        var catalog = new TemplateReferenceCatalog
        {
            IsComplete = true,
            Objects = new HashSet<string> { "bank" },
            ObjectRewardVariantIndices = new Dictionary<string, IReadOnlySet<int>> { ["bank"] = new HashSet<int> { 2 } }
        };
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(template, catalog: catalog),
            issue => issue.Severity == TemplateIssueSeverity.Error);
        string saved = JsonSerializer.Serialize(template, JsonExport.Options);
        Assert.True(TemplateRoundTripAudit.Compare(json, saved).PreservesValues);
        using var document = JsonDocument.Parse(saved);
        Assert.False(document.RootElement.GetProperty("contentCountLimits")[0].GetProperty("limits")[0].TryGetProperty("sid", out _));

        limits[0].IncludeLists = ["Missing"];
        limits[1].Content![0].Variant = 3;
        limits[2].Sid = null;
        var issues = TemplateCompatibilityValidator.Validate(template, catalog: new TemplateReferenceCatalog
        {
            IsComplete = true, Objects = catalog.Objects, ObjectRewardVariantIndices = catalog.ObjectRewardVariantIndices,
            Lists = new HashSet<string>()
        });
        Assert.Contains(issues, issue => issue is { Code: "reference.missing", Path: "contentCountLimits[0].limits[0].includeLists[0]" });
        Assert.Contains(issues, issue => issue is { Code: "content.unknownVariant", Path: "contentCountLimits[0].limits[1].content[0].variant" });
        Assert.Contains(issues, issue => issue is { Code: "countLimit.target", Path: "contentCountLimits[0].limits[2]" });
    }

    [Fact]
    public void UnspecifiedMainObjectTypeStaysAbsentAndAnExplicitEmptyTypeIsInvalid()
    {
        const string json = """
            {
              "name":"Stock shape","sizeX":128,"sizeZ":128,
              "variants":[{"zones":[{"name":"Start","mainObjects":[{"type":"Spawn","spawn":"Player1"},{}]}]}]
            }
            """;
        var template = TemplateJsonReader.Deserialize(json, JsonExport.Options)!;
        var objects = Assert.Single(Assert.Single(template.Variants!).Zones!).MainObjects!;
        Assert.Null(objects[1].Type);
        string saved = JsonSerializer.Serialize(template, JsonExport.Options);
        Assert.True(TemplateRoundTripAudit.Compare(json, saved).PreservesValues);
        using var document = JsonDocument.Parse(saved);
        Assert.Empty(document.RootElement.GetProperty("variants")[0].GetProperty("zones")[0].GetProperty("mainObjects")[1].EnumerateObject());
        Assert.Contains(TemplateCompatibilityValidator.Validate(template), issue => issue is
            { Code: "mainObject.defaultType", Severity: TemplateIssueSeverity.Warning });
        objects[1].Type = "";
        Assert.Contains(TemplateCompatibilityValidator.Validate(template), issue => issue is
            { Code: "value.empty", Severity: TemplateIssueSeverity.Error, Path: "variants[0].zones[0].mainObjects[1].type" });
    }

    [Fact]
    public void AuditReportsNestedAdditionsOnKnownAndUnknownShapes()
    {
        const string source = """{"variants":[{"zones":[{"mainObjects":[{}],"future":{"kept":1,"lost":2}}]}]}""";
        const string output = """{"variants":[{"zones":[{"mainObjects":[{"type":""}],"future":{"kept":1,"added":null}}]}]}""";
        var findings = TemplateRoundTripAudit.Compare(source, output);
        Assert.False(findings.PreservesValues);
        Assert.Equal(3, findings.Differences.Count);
        Assert.Contains(findings.Differences, issue => issue.Path == "$.variants[0].zones[0].mainObjects[0].type");
        Assert.Contains(findings.Differences, issue => issue.Path == "$.variants[0].zones[0].future.lost");
        Assert.Contains(findings.Differences, issue => issue.Path == "$.variants[0].zones[0].future.added");
        Assert.False(TemplateRoundTripAudit.Compare("""{"name":null}""", "{}").PreservesValues);
        Assert.False(TemplateRoundTripAudit.Compare("""{"name":"Map"}""", """{"name":"Map","name":"Map"}""").PreservesValues);
    }

    [Fact]
    public void ZoneRenameAndDeleteUpdateKnownAnchorsAndRetainUnknownOnes()
    {
        const string json = """
            {
              "name":"Map","sizeX":128,"sizeZ":128,
              "variants":[{"zones":[
                {"name":"A","mainObjects":[{"type":"Spawn","spawn":"Player1"}]},
                {"name":"B","mainObjects":[{"type":"City","placement":"Uniform","uniformPlacementAnchors":[
                  {"type":"Zone","weight":1,"zoneName":"A"},
                  {"type":"Zone","weight":1,"zoneName":"B"},
                  {"type":"Future","weight":1,"zoneName":"A","futureRule":{"weight":2}}
                ]}]}
              ],"connections":[]}]
            }
            """;
        var template = TemplateJsonReader.Deserialize(json, JsonExport.Options)!;
        Assert.True(TemplateRoundTripAudit.Compare(json, JsonSerializer.Serialize(template, JsonExport.Options)).PreservesValues);
        var variant = Assert.Single(template.Variants!);
        var source = variant.Zones![0];
        var anchors = Assert.Single(variant.Zones![1].MainObjects!).UniformPlacementAnchors!;
        TemplateRefactor.RenameZoneReferences(variant, "A", "Renamed");
        source.Name = "Renamed";
        Assert.Equal(["Renamed", "B", "A"], anchors.Select(anchor => anchor.ZoneName));
        var baselineIssues = TemplateCompatibilityValidator.Validate(template);
        Assert.DoesNotContain(baselineIssues, issue => issue.Code == "reference.zone");
        Assert.Contains(baselineIssues, issue => issue is { Code: "anchor.typeUnverified", Severity: TemplateIssueSeverity.Warning });
        anchors[0].ZoneName = "Missing";
        Assert.Contains(TemplateCompatibilityValidator.Validate(template), issue => issue is
            { Code: "reference.zone", Path: "variants[0].zones[1].mainObjects[0].uniformPlacementAnchors[0].zoneName" });
        anchors[0].ZoneName = "Renamed";

        TemplateRefactor.RemoveZoneReferences(variant, "Renamed");
        Assert.Equal(["B", "A"], anchors.Select(anchor => anchor.ZoneName));
        Assert.Equal("Future", anchors[1].Type);
        Assert.Equal(2, anchors[1].Extra!["futureRule"].GetProperty("weight").GetInt32());
    }

    [Fact]
    public void PastingAndMirroringRepointKnownAnchorsInTheCopiedZoneOnly()
    {
        var copied = new Zone { Name = "A-copy", MainObjects = [new MainObject
        {
            Type = "City",
            UniformPlacementAnchors = [Anchor("A"), Anchor("B"), Anchor("Outside"), Anchor("A", "Future")]
        }] };
        TemplateRefactor.PreparePastedZone(copied, [new() { Name = "A" }, new() { Name = "B" }], "A");
        var copiedObject = Assert.Single(copied.MainObjects);
        Assert.Equal(["A-copy", "B", "A"], copiedObject.UniformPlacementAnchors!.Select(anchor => anchor.ZoneName));
        TemplateRefactor.RemapMainObjectPlacementZones(copied,
            new Dictionary<string, string> { ["A-copy"] = "A-mirror", ["A-mirror"] = "A-copy", ["B"] = "B-mirror" });
        Assert.Equal(["A-mirror", "B-mirror", "A"], copiedObject.UniformPlacementAnchors!.Select(anchor => anchor.ZoneName));

        var variant = new Variant
        {
            Zones =
            [
                new() { Name = "B-mirror" },
                new() { Name = "A-mirror" },
                new() { Name = "Second", MainObjects = [new MainObject { UniformPlacementAnchors = [Anchor("A-mirror")] }] }
            ]
        };
        TemplateRefactor.RemoveZoneReferences(variant, "A-mirror");
        Assert.Null(Assert.Single(variant.Zones[2].MainObjects!).UniformPlacementAnchors);
    }

    private static UniformPlacementAnchor Anchor(string zone, string type = "Zone") =>
        new() { Type = type, ZoneName = zone, Weight = 1 };
}
