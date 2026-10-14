using System.Text.Json;
using System.Text.Json.Nodes;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Analysis;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public sealed class TemplatePropertiesDraftTests
{
    private static RmgTemplate Source() => new()
    {
        Name = "Properties", GameMode = "Classic", SizeX = 128, SizeZ = 128,
        Variants = [new() { Zones = [new() { Name = "A", Size = 1, MainObjects = [new() { Type = "Spawn", Spawn = "Player1" }] },
            new() { Name = "B", Size = 1, MainObjects = [new() { Type = "Spawn", Spawn = "Player2" }] }], Connections = [new() { Name = "Gate", From = "A", To = "B", ConnectionType = "Direct" }] }]
    };

    [Fact]
    public void PropertyDraftAndSuccessfulCommitAreDetachedAndPreserveAbsentGroups()
    {
        var source = Source(); var draft = new TemplatePropertiesDraft(source); var candidate = draft.Snapshot();
        candidate["description"] = "Changed";
        var result = draft.Commit(candidate, out _);
        Assert.Null(source.Description); Assert.Equal("Changed", result.Description);
        Assert.Null(result.GameRules); Assert.Null(result.SpawnInfos); Assert.Null(result.Variants![0].River);
        result.Variants[0].Zones![0].Name = "Other"; Assert.Equal("A", source.Variants![0].Zones![0].Name);
    }

    [Fact]
    public void UnknownRootCardAllianceAndRiverFieldsSurviveTypedRoundTrip()
    {
        var source = Source();
        source.Extra = new() { ["futureRoot"] = JsonDocument.Parse("{\"id\":\"Exact \"}").RootElement.Clone() };
        source.DescBlocks = new() { Blocks = [new() { Type = "text", Parameters = ["Hello"], Extra = new() { ["future"] = JsonDocument.Parse("[1,true]").RootElement.Clone() } }] };
        source.Variants![0].River = new() { CreateRiverSystem = false, Extra = new() { ["later"] = JsonDocument.Parse("7").RootElement.Clone() } };
        var draft = new TemplatePropertiesDraft(source); var candidate = draft.Snapshot(); candidate["name"] = "Renamed";
        var result = draft.Commit(candidate, out _);
        Assert.Equal("Exact ", result.Extra!["futureRoot"].GetProperty("id").GetString());
        Assert.True(result.DescBlocks!.Blocks![0].Extra!["future"][1].GetBoolean());
        Assert.Equal(7, result.Variants![0].River!.Extra!["later"].GetInt32()); Assert.False(result.Variants[0].River!.CreateRiverSystem);
    }

    [Fact]
    public void InvalidNewRootRulesFailWithoutChangingTheSourceOrDraft()
    {
        var source = Source(); var draft = new TemplatePropertiesDraft(source); var candidate = draft.Snapshot();
        candidate["gameRules"] = new JsonObject { ["heroCountMin"] = 8, ["heroCountMax"] = 2 };
        Assert.Throws<InvalidOperationException>(() => draft.Commit(candidate, out _));
        Assert.Null(source.GameRules); Assert.Null(draft.Snapshot()["gameRules"]);
    }

    [Fact]
    public void ExistingImportErrorsDoNotBlockAnUnrelatedDescriptionCorrection()
    {
        var source = Source(); source.Variants![0].Connections![0].To = "Missing";
        var draft = new TemplatePropertiesDraft(source); var candidate = draft.Snapshot(); candidate["description"] = "Corrected";
        var result = draft.Commit(candidate, out var issues);
        Assert.Equal("Corrected", result.Description);
        Assert.Contains(issues, issue => issue.Code == "graph.invalid" && issue.Path == "variants[0]" && issue.Message.Contains("Missing", StringComparison.Ordinal));
    }

    [Fact]
    public void EditingAnAlreadyInvalidFieldCannotUseItsOldErrorAsABaselineExemption()
    {
        var source = Source(); source.SizeX = 0;
        var draft = new TemplatePropertiesDraft(source); var candidate = draft.Snapshot(); candidate["sizeX"] = -1;
        Assert.Throws<InvalidOperationException>(() => draft.Commit(candidate, out _)); Assert.Equal(0, source.SizeX);
    }

    [Fact]
    public void ExistingInvalidHeroRangeCannotHideAChangedInvalidRangeAtAVirtualGroupPath()
    {
        var source = Source(); source.GameRules = new() { HeroCountMin = 5, HeroCountMax = 2 };
        var draft = new TemplatePropertiesDraft(source); var candidate = draft.Snapshot(); candidate["gameRules"]!["heroCountMin"] = 6;
        Assert.Throws<InvalidOperationException>(() => draft.Commit(candidate, out _)); Assert.Equal(5, source.GameRules.HeroCountMin);
    }

    [Fact]
    public void UnchangedTextEditorsPreserveExplicitEmptyStringsAndDoNotCreateAbsentGroups()
    {
        var source = Source(); source.Description = ""; source.GameRules = new() { CustomAI = "" };
        var draft = new TemplatePropertiesDraft(source); var candidate = draft.Snapshot();
        static JsonNode? MustNotParse(string _) => throw new InvalidOperationException("An unchanged text field was parsed.");
        Assert.False(TemplatePropertiesDraft.ApplyTextEdit(candidate, [], "description", "", "", MustNotParse));
        Assert.False(TemplatePropertiesDraft.ApplyTextEdit(candidate, ["gameRules"], "customAI", "", "", MustNotParse));
        Assert.False(TemplatePropertiesDraft.ApplyTextEdit(candidate, ["absentGroup"], "later", "", "", MustNotParse));
        var result = draft.Commit(candidate, out _);
        Assert.Equal("", result.Description); Assert.Equal("", result.GameRules!.CustomAI); Assert.False(candidate.ContainsKey("absentGroup"));
        var raw = new JsonObject { ["explicitNull"] = null };
        Assert.False(TemplatePropertiesDraft.ApplyTextEdit(raw, [], "explicitNull", "", "", MustNotParse));
        Assert.False(TemplatePropertiesDraft.ApplyTextEdit(raw, [], "absent", "", "", MustNotParse));
        Assert.True(raw.ContainsKey("explicitNull")); Assert.False(raw.ContainsKey("absent"));
    }

    [Fact]
    public void ClearingEditedOptionalTextRemovesOnlyThatFieldAndKeepsUnknownNeighbors()
    {
        var source = Source(); source.Description = "Old";
        source.Extra = new() { ["future"] = JsonSerializer.SerializeToElement(new { sid = "Exact " }) };
        var draft = new TemplatePropertiesDraft(source); var candidate = draft.Snapshot();
        Assert.True(TemplatePropertiesDraft.ApplyTextEdit(candidate, [], "description", "Old", "", _ => null));
        var result = draft.Commit(candidate, out _);
        Assert.Null(result.Description); Assert.Equal("Exact ", result.Extra!["future"].GetProperty("sid").GetString());
        Assert.Equal("Old", source.Description);
    }

    [Fact]
    public void CompleteCatalogPreservesExistingUnknownAiForUnrelatedEditsButRejectsNewUnknownReferences()
    {
        var source = Source(); source.GameRules = new() { CustomAI = "legacy_unknown_ai" };
        var catalog = new TemplateReferenceCatalog { IsComplete = true, CustomAI = new HashSet<string>(StringComparer.Ordinal) { "known_ai" },
            Objects = new HashSet<string>(StringComparer.Ordinal) { "known_object" }, Source = "Verified fixture" };
        var draft = new TemplatePropertiesDraft(source, catalog); var candidate = draft.Snapshot(); candidate["description"] = "Corrected";
        var result = draft.Commit(candidate, out var issues);
        Assert.Equal("legacy_unknown_ai", result.GameRules!.CustomAI);
        Assert.Contains(issues, issue => issue.Code == "reference.missing" && issue.Path == "gameRules.customAI" && issue.Message.Contains("legacy_unknown_ai", StringComparison.Ordinal));
        candidate["gameRules"]!["customAI"] = "new_unknown_ai";
        var aiFailure = Assert.Throws<InvalidOperationException>(() => draft.Commit(candidate, out _)); Assert.Contains("new_unknown_ai", aiFailure.Message);
        candidate = draft.Snapshot();
        candidate["mandatoryContent"] = new JsonArray(new JsonObject { ["name"] = "New mandatory", ["content"] = new JsonArray(new JsonObject { ["sid"] = "missing_object", ["variant"] = -1 }) });
        var sidFailure = Assert.Throws<InvalidOperationException>(() => draft.Commit(candidate, out _)); Assert.Contains("missing_object", sidFailure.Message);
        Assert.Equal("legacy_unknown_ai", source.GameRules.CustomAI); Assert.Null(source.MandatoryContent); Assert.Single(source.Variants![0].Zones![0].MainObjects!);
    }
}
