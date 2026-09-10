using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public class TemplateRefactorTests
{
    [Fact]
    public void EditorCopy_PreservesUnknownDataAndDoesNotMutateAnotherWindow()
    {
        const string json = """{"name":"Original","futureSetting":{"active":true},"variants":[{"zones":[{"name":"A"}],"connections":[]}]}""";
        var original = System.Text.Json.JsonSerializer.Deserialize<RmgTemplate>(json)!;
        var copy = TemplateRefactor.CreateEditingCopy(original);
        copy.Name = "Edited";
        copy.Variants![0].Zones![0].Name = "Renamed";
        Assert.Equal("Original", original.Name);
        Assert.Equal("A", original.Variants![0].Zones![0].Name);
        Assert.Contains("futureSetting", System.Text.Json.JsonSerializer.Serialize(copy));
    }

    [Fact]
    public void PastedPlayerZone_BecomesNeutralAndDropsRoadsToOriginalConnections()
    {
        var original = new Zone { Name = "A", MainObjects = [new MainObject { Type = "Spawn", Spawn = "Player1", Owner = "Player1" }] };
        var clone = new Zone { Name = "A-copy", MainObjects = [new MainObject { Type = "Spawn", Spawn = "Player1", Owner = "Player1" }],
            Roads = [new Road { From = new RoadEndpoint { Type = "Connection", Args = ["AB"] } }] };
        Assert.True(TemplateRefactor.PreparePastedZone(clone, [original]));
        Assert.Equal("City", clone.MainObjects[0].Type);
        Assert.Null(clone.MainObjects[0].Spawn);
        Assert.Null(clone.MainObjects[0].Owner);
        Assert.Empty(clone.Roads);
        Assert.Equal("Player1", original.MainObjects[0].Spawn);
    }

    [Fact]
    public void ApplyConnectionEdits_RenamesRoadsIncludingNameSwaps()
    {
        var variant = SampleVariant();
        variant.Connections!.Add(new Connection { Name = "Portal", From = "Spawn-A", To = "Neutral-B" });
        variant.Zones![0].Roads = [new Road
        {
            From = new RoadEndpoint { Type = "Connection", Args = ["Ring-A-B"] },
            To = new RoadEndpoint { Type = "Connection", Args = ["Portal"] }
        }];
        TemplateRefactor.ApplyConnectionEdits(variant,
        [
            new Connection { Name = "Portal", From = "Spawn-A", To = "Neutral-B" },
            new Connection { Name = "Ring-A-B", From = "Spawn-A", To = "Neutral-B" }
        ]);
        Assert.Equal("Portal", variant.Zones[0].Roads![0].From!.Args![0]);
        Assert.Equal("Ring-A-B", variant.Zones[0].Roads![0].To!.Args![0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ApplyConnectionEdits_InvalidDraftDoesNotChangeTheMap(string name)
    {
        var variant = SampleVariant();
        var original = System.Text.Json.JsonSerializer.Serialize(variant);
        Assert.Throws<InvalidOperationException>(() => TemplateRefactor.ApplyConnectionEdits(variant,
            [new Connection { Name = name, From = "Spawn-A", To = "Neutral-B" }]));
        Assert.Equal(original, System.Text.Json.JsonSerializer.Serialize(variant));
    }

    [Fact]
    public void DeleteZone_RepairsGuardZoneOnSurvivingConnections()
    {
        var variant = SampleVariant();
        variant.Zones!.Add(new Zone { Name = "C" });
        var surviving = new Connection { Name = "BC", From = "Neutral-B", To = "C", GuardZone = "Spawn-A" };
        variant.Connections!.Add(surviving);
        TemplateRefactor.RemoveZoneReferences(variant, "Spawn-A");
        Assert.Equal("Neutral-B", surviving.GuardZone);
    }

    private static Variant SampleVariant() => new()
    {
        Orientation = new Orientation { ZeroAngleZone = "Spawn-A" },
        Zones =
        [
            new Zone { Name = "Spawn-A" },
            new Zone
            {
                Name = "Neutral-B",
                ZoneBiome = new BiomeSelector { Type = "MatchZone", Args = ["Spawn-A"] },
                ContentBiome = new BiomeSelector { Type = "MatchMainObject", Args = ["0"] },
            },
        ],
        Connections =
        [
            new Connection { Name = "Ring-A-B", From = "Spawn-A", To = "Neutral-B", GuardZone = "Spawn-A" },
        ],
    };

    [Fact]
    public void RenameZoneReferences_RepointsEveryReferenceToTheOldName()
    {
        Variant variant = SampleVariant();

        TemplateRefactor.RenameZoneReferences(variant, "Spawn-A", "Home");

        Connection conn = variant.Connections![0];
        Assert.Equal("Home", conn.From);
        Assert.Equal("Home", conn.GuardZone);
        Assert.Equal("Neutral-B", conn.To);
        Assert.Equal("Home", variant.Zones![1].ZoneBiome!.Args![0]);
        Assert.Equal("Home", variant.Orientation!.ZeroAngleZone);
        // A non-MatchZone selector must not be touched.
        Assert.Equal("0", variant.Zones![1].ContentBiome!.Args![0]);
    }

    [Fact]
    public void RemoveZoneReferences_DropsConnectionsRoadsBiomesAndTheAnchor()
    {
        Variant variant = SampleVariant();
        variant.Zones![1].Roads =
        [
            new Road
            {
                From = new RoadEndpoint { Type = "MainObject", Args = ["0"] },
                To = new RoadEndpoint { Type = "Connection", Args = ["Ring-A-B"] },
            },
        ];

        int removed = TemplateRefactor.RemoveZoneReferences(variant, "Spawn-A");

        Assert.Equal(1, removed);
        Assert.Empty(variant.Connections!);
        Assert.Empty(variant.Zones![1].Roads!);                       // road to the deleted link is gone
        Assert.Empty(variant.Zones![1].ZoneBiome!.Args!);              // MatchZone target cleared
        Assert.Equal("Neutral-B", variant.Orientation!.ZeroAngleZone); // anchor moved to a survivor
    }

    [Fact]
    public void RemoveRoadsFor_OnlyDropsRoadsOfThatConnection()
    {
        Variant variant = SampleVariant();
        variant.Zones![0].Roads =
        [
            new Road { To = new RoadEndpoint { Type = "Connection", Args = ["Ring-A-B"] } },
            new Road { To = new RoadEndpoint { Type = "Connection", Args = ["Other"] } },
        ];

        TemplateRefactor.RemoveRoadsFor(variant, "Ring-A-B");

        Road kept = Assert.Single(variant.Zones![0].Roads!);
        Assert.Equal("Other", kept.To!.Args![0]);
    }

    [Theory]
    [InlineData("", "Home")]
    [InlineData("Spawn-A", "")]
    [InlineData("Spawn-A", "Spawn-A")]
    public void RenameZoneReferences_IgnoresNoOpAndBlankRenames(string oldName, string newName)
    {
        Variant variant = SampleVariant();

        TemplateRefactor.RenameZoneReferences(variant, oldName, newName);

        Assert.Equal("Spawn-A", variant.Connections![0].From);
        Assert.Equal("Spawn-A", variant.Orientation!.ZeroAngleZone);
    }
}
