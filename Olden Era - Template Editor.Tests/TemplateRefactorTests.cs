using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public class TemplateRefactorTests
{
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
