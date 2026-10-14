using System.Text.Json;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public sealed class PortalPlacementRefactorTests
{
    [Fact]
    public void ConnectionRenameRewritesConfirmedFirstArgumentWithoutEditingOtherArgumentsOrUnknownRules()
    {
        var rule = new ContentPlacementRule { Type = "Connection", Args = ["Gate-A", "Gate-A"], Extra = new() { ["later"] = JsonDocument.Parse("true").RootElement.Clone() } };
        var unknown = new ContentPlacementRule { Type = "Future", Args = ["Gate-A"] };
        var connection = new Connection { Name = "Portal", From = "A", To = "B", PortalPlacementRulesFrom = [rule, unknown] };
        var variant = new Variant { Connections = [connection] };
        TemplateRefactor.RenameConnectionReferences(variant, new Dictionary<string, string> { ["Gate-A"] = "Gate-B" });
        Assert.Equal(["Gate-B", "Gate-A"], rule.Args); Assert.Equal("Gate-A", unknown.Args![0]); Assert.True(rule.Extra!["later"].GetBoolean());
        Assert.Single(TemplateRefactor.UnverifiedPortalPlacementWarnings(connection));
    }

    [Fact]
    public void ConnectionManagerSwapsRewriteTheCommittedDraftExactlyOnce()
    {
        var variant = new Variant { Connections = [new() { Name = "One", From = "A", To = "B" }, new() { Name = "Two", From = "B", To = "C" }] };
        TemplateRefactor.ApplyConnectionEdits(variant,
        [new() { Name = "Two", From = "A", To = "B", PortalPlacementRulesFrom = [new() { Type = "Connection", Args = ["One"] }] },
         new() { Name = "One", From = "B", To = "C", PortalPlacementRulesTo = [new() { Type = "Connection", Args = ["Two"] }] }]);
        Assert.Equal("Two", variant.Connections[0].PortalPlacementRulesFrom![0].Args![0]);
        Assert.Equal("One", variant.Connections[1].PortalPlacementRulesTo![0].Args![0]);
    }

    [Fact]
    public void DeletingConnectionDropsItsPortalAnchorsButPreservesUnrelatedRules()
    {
        var connection = new Connection { PortalPlacementRulesTo = [new() { Type = "Connection", Args = ["Gone"] }, new() { Type = "Crossroads", Args = [] }, new() { Type = "Future", Args = ["Gone"] }] };
        TemplateRefactor.RemoveRoadsFor(new Variant { Connections = [connection] }, "Gone");
        Assert.Equal(2, connection.PortalPlacementRulesTo.Count); Assert.Equal("Crossroads", connection.PortalPlacementRulesTo[0].Type);
        Assert.Equal("Gone", connection.PortalPlacementRulesTo[1].Args![0]);
    }

    [Fact]
    public void RemovingMainObjectReindexesOnlyThePortalEndpointInItsOwnZone()
    {
        var a = new Zone { Name = "A", MainObjects = [new(), new(), new()] };
        var connection = new Connection { From = "A", To = "B",
            PortalPlacementRulesFrom = [new() { Type = "MainObject", Args = ["1"] }, new() { Type = "MainObject", Args = ["2"] }],
            PortalPlacementRulesTo = [new() { Type = "MainObject", Args = ["2"] }] };
        TemplateRefactor.RemoveMainObject(new Variant { Zones = [a, new() { Name = "B" }], Connections = [connection] }, a, 1);
        Assert.Single(connection.PortalPlacementRulesFrom); Assert.Equal("1", connection.PortalPlacementRulesFrom[0].Args![0]);
        Assert.Equal("2", connection.PortalPlacementRulesTo[0].Args![0]);
    }

    [Fact]
    public void ZoneRenameAndRemovalRepairVerifiedGateAndMainObjectNearZoneTargets()
    {
        var obj = new MainObject { Placement = "NearZone", PlacementArgs = ["A", "0.5"] };
        var connection = new Connection { Name = "Gate", From = "B", To = "C", GatePlacement = "NearZone", GatePlacementArgs = ["A", "0.25"] };
        var variant = new Variant { Zones = [new() { Name = "A" }, new() { Name = "B", MainObjects = [obj] }, new() { Name = "C" }], Connections = [connection] };
        TemplateRefactor.RenameZoneReferences(variant, "A", "Start");
        Assert.Equal("Start", obj.PlacementArgs![0]); Assert.Equal("Start", connection.GatePlacementArgs![0]);
        TemplateRefactor.RemoveZoneReferences(variant, "Start");
        Assert.Equal("Uniform", obj.Placement); Assert.Null(obj.PlacementArgs);
        Assert.Equal("Random", connection.GatePlacement); Assert.Null(connection.GatePlacementArgs);
    }

    [Fact]
    public void MirroredConnectionRemapsVerifiedSpatialReferencesAndKeepsLocalObjectIndices()
    {
        var copied = new Connection { Name = "Portal-M", GuardZone = "A", GatePlacement = "NearZone", GatePlacementArgs = ["C"],
            PortalPlacementRulesFrom = [new() { Type = "Connection", Args = ["Gate"] }, new() { Type = "MainObject", Args = ["2"] }],
            PortalPlacementRulesTo = [new() { Type = "NearZone", Args = ["A"] }] };
        var warnings = TemplateRefactor.RemapConnectionSpatialReferences(copied, new Dictionary<string, string> { ["A"] = "A-M", ["C"] = "C-M" }, new Dictionary<string, string> { ["Gate"] = "Gate-M" });
        Assert.Equal("A-M", copied.GuardZone); Assert.Equal("C-M", copied.GatePlacementArgs![0]);
        Assert.Equal("Gate-M", copied.PortalPlacementRulesFrom![0].Args![0]); Assert.Equal("2", copied.PortalPlacementRulesFrom[1].Args![0]);
        Assert.Equal("A", copied.PortalPlacementRulesTo![0].Args![0]); Assert.Single(warnings);
    }

    [Fact]
    public void CompleteMirrorPassResolvesLaterTwinsReversedOrientationAndIsIdempotent()
    {
        var sourcePortal = new Connection { Name = "Portal", From = "A", To = "B", ConnectionType = "Portal", GuardZone = "A",
            PortalPlacementRulesFrom = [new() { Type = "Connection", Args = ["Gate"] }] };
        var sourceGate = new Connection { Name = "Gate", From = "A", To = "C", ConnectionType = "Direct" };
        var portalCopy = new Connection { Name = "Portal-M", From = "A-M", To = "B-M", ConnectionType = "Portal" };
        var gateCopy = new Connection { Name = "Gate-M", From = "C-M", To = "A-M", ConnectionType = "Direct" };
        var zoneMap = new Dictionary<string, string> { ["A"] = "A-M", ["A-M"] = "A", ["B"] = "B-M", ["B-M"] = "B", ["C"] = "C-M", ["C-M"] = "C" };
        var names = TemplateRefactor.MirroredConnectionNames([sourcePortal, sourceGate, portalCopy, gateCopy], zoneMap);
        Assert.Equal("Gate-M", names["Gate"]);
        Assert.Empty(TemplateRefactor.ApplyMirroredConnectionReferences(sourcePortal, portalCopy, zoneMap, names));
        Assert.Equal("Gate-M", portalCopy.PortalPlacementRulesFrom![0].Args![0]); Assert.Equal("A-M", portalCopy.GuardZone);
        TemplateRefactor.ApplyMirroredConnectionReferences(sourcePortal, portalCopy, zoneMap, names);
        Assert.Equal("Gate-M", portalCopy.PortalPlacementRulesFrom[0].Args![0]); Assert.Equal("A-M", portalCopy.GuardZone);
        Assert.Equal("Gate", sourcePortal.PortalPlacementRulesFrom![0].Args![0]); Assert.Equal("A", sourcePortal.GuardZone);
    }

    [Fact]
    public void MirrorNameMapDistinguishesConnectionTypesAndDoesNotGuessParallelSameTypeTargets()
    {
        var zones = new Dictionary<string, string> { ["A"] = "A-M", ["B"] = "B-M" };
        var original = new Connection { Name = "Portal", From = "A", To = "B", ConnectionType = "Portal" };
        var right = new Connection { Name = "Right", From = "A-M", To = "B-M", ConnectionType = "Portal" };
        var different = new Connection { Name = "Other", From = "A-M", To = "B-M", ConnectionType = "Direct" };
        Assert.Equal("Right", TemplateRefactor.MirroredConnectionNames([original, right, different], zones)["Portal"]);
        var ambiguous = new Connection { Name = "Ambiguous", From = "B-M", To = "A-M", ConnectionType = "Portal" };
        Assert.False(TemplateRefactor.MirroredConnectionNames([original, right, ambiguous], zones).ContainsKey("Portal"));
    }

    [Fact]
    public void MainObjectNearZoneMirrorRemapsOnlyTheVerifiedZoneArgument()
    {
        var zone = new Zone { MainObjects = [new() { Placement = "NearZone", PlacementArgs = ["Other", "0.3"] }, new() { Placement = "Future", PlacementArgs = ["Other"] }] };
        TemplateRefactor.RemapMainObjectPlacementZones(zone, new Dictionary<string, string> { ["Other"] = "Other-M" });
        Assert.Equal(["Other-M", "0.3"], zone.MainObjects[0].PlacementArgs);
        Assert.Equal("Other", zone.MainObjects[1].PlacementArgs![0]);
    }
}
