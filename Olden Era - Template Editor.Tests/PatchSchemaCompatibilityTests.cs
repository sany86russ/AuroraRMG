using System.Text.Json;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Analysis;

namespace Olden_Era___Template_Editor.Tests;

public class PatchSchemaCompatibilityTests
{
    [Fact]
    public void PatchFields_PreserveFractionalWeightsFalseValuesAndUnknownFields()
    {
        const string json = """
        {
          "name":"Patch","sizeX":256,"sizeZ":256,"maxAlliancesCount":2,
          "spawnInfos":[{"spawn":"Player1","allianceIndex":1,"isAllianceLocked":false,"futureSpawn":7}],
          "descBlocks":{"futureDescription":true,"blocks":[{"type":"tags","p":["map_tags_1,map_tags_2"],"futureBlock":{"sid":"exact "}}]},
          "gameRules":{"disableFactionLaws":true,"disableMagicGuild":false,"disableMagicCustomLearning":true,
            "customAI":"author-profile","winConditions":{"isResetColdownCityHold":false}},
          "variants":[{"river":{"createRiverSystem":true,"tributarySpacing":24.5,"futureRiver":"retained"},
            "zones":[{"name":"A","guardReactionDistribution":[0.25,1.75,0,0,0.5,0],
              "randomHireEnableWeeklyUnitIncrement":[true,false],"randomHireEnableModificatorWeeklyUnitIncrement":[false,true],
              "randomHireInitialUnitIncrement":[1,4],"mainObjects":[{"type":"City","isKeyObject":true,
                "enableWeeklyUnitIncrement":false,"weekOneOnlyModificatorGrowth":true,"initialUnitIncrement":5,"futureObject":[2,3]}]}],
            "connections":[{"from":"A","to":"B","portalFromEnabled":false,"portalToEnabled":true,"guardReaction":"Friendly","futureConnection":8}]}],
          "futureRoot":{"nested":"never discard"}
        }
        """;
        var model = JsonSerializer.Deserialize<RmgTemplate>(json, JsonExport.Options)!;
        Assert.Equal(1, model.SpawnInfos![0].AllianceIndex);
        Assert.False(model.SpawnInfos[0].IsAllianceLocked);
        Assert.Equal(24.5, model.Variants![0].River!.TributarySpacing);
        Assert.Equal(new double[] { 0.25, 1.75, 0, 0, 0.5, 0 }, model.Variants[0].Zones![0].GuardReactionDistribution);
        Assert.False(model.GameRules!.WinConditions!.IsResetColdownCityHold);
        using var original = JsonDocument.Parse(json);
        using var saved = JsonDocument.Parse(JsonSerializer.Serialize(model, JsonExport.Options));
        AssertJsonEqual(original.RootElement, saved.RootElement);
    }

    [Fact]
    public void MissingOptionalPatchFields_RemainAbsentAfterEditing()
    {
        const string json = """{"name":"Legacy","sizeX":128,"sizeZ":128,"gameRules":{},"variants":[{"zones":[{"name":"A","mainObjects":[{"type":"City"}]}],"connections":[{"from":"A","to":"B"}]}]}""";
        var model = JsonSerializer.Deserialize<RmgTemplate>(json, JsonExport.Options)!;
        model.Name = "Edited";
        using var saved = JsonDocument.Parse(JsonSerializer.Serialize(model, JsonExport.Options));
        foreach (string field in new[] { "spawnInfos", "maxAlliancesCount", "descBlocks" })
            Assert.False(saved.RootElement.TryGetProperty(field, out _));
        Assert.False(saved.RootElement.GetProperty("gameRules").TryGetProperty("disableMagicGuild", out _));
        var variant = saved.RootElement.GetProperty("variants")[0];
        Assert.False(variant.TryGetProperty("river", out _));
        Assert.False(variant.GetProperty("zones")[0].GetProperty("mainObjects")[0].TryGetProperty("enableWeeklyUnitIncrement", out _));
        Assert.False(variant.GetProperty("connections")[0].TryGetProperty("portalFromEnabled", out _));
    }

    [Fact]
    public void SingleObjectAndScalarForms_KeepExistingImportConventions()
    {
        const string json = """
        {"name":"Collapsed","sizeX":128,"sizeZ":128,
          "spawnInfos":{"spawn":"Player1","allianceIndex":-1},
          "descBlocks":{"blocks":{"type":"text","p":"description_sid"}},
          "variants":{"zones":{"name":"A","mainObjects":{"type":"Spawn","spawn":"Player1"}}}}
        """;
        var model = JsonSerializer.Deserialize<RmgTemplate>(json, JsonExport.Options)!;
        Assert.Equal(-1, Assert.Single(model.SpawnInfos!).AllianceIndex);
        Assert.Equal("description_sid", Assert.Single(Assert.Single(model.DescBlocks!.Blocks!).Parameters!));
        Assert.Single(model.Variants!);
        using var saved = JsonDocument.Parse(JsonSerializer.Serialize(model, JsonExport.Options));
        Assert.Equal(JsonValueKind.Array, saved.RootElement.GetProperty("spawnInfos").ValueKind);
    }

    [Fact]
    public void IntegerReactionWeights_RemainReadable()
    {
        var zone = JsonSerializer.Deserialize<Zone>("""{"name":"A","guardReactionDistribution":[5,15,40,25,10,5]}""", JsonExport.Options)!;
        Assert.Equal(new double[] { 5, 15, 40, 25, 10, 5 }, zone.GuardReactionDistribution);
    }

    [Fact]
    public void CollapsedHireArrays_AcceptTypedScalarsAndRejectWrongTypes()
    {
        var zone = JsonSerializer.Deserialize<Zone>("""{"name":"A","randomHireEnableWeeklyUnitIncrement":false,"randomHireEnableModificatorWeeklyUnitIncrement":true,"randomHireInitialUnitIncrement":4}""", JsonExport.Options)!;
        Assert.False(Assert.Single(zone.RandomHireEnableWeeklyUnitIncrement!));
        Assert.True(Assert.Single(zone.RandomHireEnableModificatorWeeklyUnitIncrement!));
        Assert.Equal(4, Assert.Single(zone.RandomHireInitialUnitIncrement!));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Zone>("""{"randomHireInitialUnitIncrement":false}""", JsonExport.Options));
    }

    [Fact]
    public void Validator_FfaUsesSeparateIndependentSlotsAndOneBasedTeams()
    {
        var template = ValidTemplate();
        template.SpawnInfos = [new() { Spawn = "Player1", AllianceIndex = -1 }, new() { Spawn = "Player2", AllianceIndex = -1 }];
        template.MaxAlliancesCount = 2;
        AssertNoErrors(template);
        template.MaxAlliancesCount = 1;
        AssertIssue(template, "alliance.count");
        template.SpawnInfos[0].AllianceIndex = 0;
        AssertIssue(template, "alliance.index");
    }

    [Fact]
    public void Validator_DoesNotAcceptMissingDuplicateOrExcessAllianceAssignments()
    {
        var template = ValidTemplate();
        template.SpawnInfos = [new() { Spawn = "Player1", AllianceIndex = 1 }, new() { Spawn = "Player1", AllianceIndex = 1 }, new() { Spawn = "Player3", AllianceIndex = 3 }];
        template.MaxAlliancesCount = 2;
        foreach (string code in new[] { "alliance.duplicateSpawn", "alliance.missingSpawn", "alliance.spawn", "alliance.limit" }) AssertIssue(template, code);
    }

    [Fact]
    public void Validator_InspectsEveryVariantAndEveryObjective()
    {
        var template = ValidTemplate();
        template.GameRules!.WinConditions = new() { Classic = false, CityHold = true, CityHoldDays = 3 };
        template.Variants![0].Zones!.Add(new() { Name = "Hold", MainObjects = [new() { Type = "City", HoldCityWinCon = true }] });
        template.Variants[0].Connections!.Add(new() { From = "A", To = "Hold", ConnectionType = "Direct" });
        template.Variants.Add(new()
        {
            Zones = [new() { Name = "Solo", MainObjects = [new() { Type = "Spawn", Spawn = "Player1" }] }], Connections = [],
        });
        var issues = TemplateCompatibilityValidator.Validate(template);
        Assert.Contains(issues, i => i.Code == "variants.spawns" && i.Path == "variants[1]");
        Assert.Contains(issues, i => i.Code == "win.cityHoldTarget" && i.Path == "variants[1]");
        Assert.DoesNotContain(issues, i => i.Code == "win.cityHoldTarget" && i.Path == "variants[0]");
    }

    [Fact]
    public void Validator_FullCatalogRejectsMissingReferencesButResolvesInlineDefinitions()
    {
        var template = ValidTemplate();
        template.ContentLists = [JsonSerializer.Deserialize<JsonElement>("""{"name":"list ","content":[{"sid":"gold","weight":1}]}""")];
        template.ContentPools = [JsonSerializer.Deserialize<JsonElement>("""{"name":"pool ","groups":[{"weight":1,"includeLists":"list "}]}""")];
        var zone = Assert.IsType<List<Zone>>(Assert.Single(template.Variants!).Zones)[0];
        zone.GuardedContentPool = ["pool "];
        zone.Layout = "layout";
        var catalog = new TemplateReferenceCatalog
        {
            IsComplete = true, Source = "test snapshot", Pools = new HashSet<string>(), Lists = new HashSet<string>(),
            Layouts = new HashSet<string> { "layout" }, Objects = new HashSet<string> { "gold" },
        };
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(template, catalog: catalog), i => i.Severity == TemplateIssueSeverity.Error);
        zone.GuardedContentPool = ["pool"];
        Assert.Contains(TemplateCompatibilityValidator.Validate(template, catalog: catalog), i => i.Code == "reference.missing" && i.Path.EndsWith("guardedContentPool[0]"));
    }

    [Fact]
    public void Validator_PartialCatalogDoesNotInventUnsupportedGameContent()
    {
        var template = ValidTemplate();
        template.Variants![0].Zones![0].GuardedContentPool = ["new_game_pool"];
        var issues = TemplateCompatibilityValidator.Validate(template);
        Assert.Contains(issues, i => i.Code == "reference.unverified" && i.Severity == TemplateIssueSeverity.Warning);
        Assert.DoesNotContain(issues, i => i.Severity == TemplateIssueSeverity.Error);
    }

    [Fact]
    public void Validator_TolerantImportReportsErrorsAsWarningsWithoutMutating()
    {
        var template = ValidTemplate();
        template.SizeX = 0;
        template.Variants![0].Connections![0].To = "Unknown";
        string before = JsonSerializer.Serialize(template, JsonExport.Options);
        Assert.Contains(TemplateCompatibilityValidator.Validate(template), i => i.Severity == TemplateIssueSeverity.Error);
        var imported = TemplateCompatibilityValidator.Validate(template, TemplateValidationMode.TolerantImport);
        Assert.NotEmpty(imported);
        Assert.DoesNotContain(imported, i => i.Severity == TemplateIssueSeverity.Error);
        Assert.Equal(before, JsonSerializer.Serialize(template, JsonExport.Options));
    }

    [Fact]
    public void Validator_RejectsInvalidNumbersAndReactionDistribution()
    {
        var template = ValidTemplate();
        var zone = template.Variants![0].Zones![0];
        zone.GuardReactionDistribution = [0, 0, 0, 0, 0, 0];
        zone.GuardMultiplier = double.NaN;
        zone.GuardRandomization = 2;
        template.Variants[0].River = new() { CreateRiverSystem = true, TributarySpacing = 0 };
        foreach (string code in new[] { "guard.reactionZero", "number.nonnegative", "number.fraction", "river.spacing" }) AssertIssue(template, code);
        zone.GuardReactionDistribution = [1, 2];
        AssertIssue(template, "guard.reactionCount");
    }

    [Fact]
    public void Validator_PreservesLegacyHeroLimitsAndLongTournamentDurationsWithWarning()
    {
        var template = ValidTemplate();
        template.GameRules!.HeroCountMax = 12;
        template.GameRules.WinConditions = new()
        {
            Classic = true, Tournament = true, TournamentDays = [7, 6, 6], TournamentAnnounceDays = [1, 9, 16], TournamentPointsToWin = 2,
        };
        var issues = TemplateCompatibilityValidator.Validate(template);
        Assert.Contains(issues, i => i.Code == "heroes.extendedLimit" && i.Severity == TemplateIssueSeverity.Warning);
        Assert.DoesNotContain(issues, i => i.Severity == TemplateIssueSeverity.Error);
    }

    [Fact]
    public void Validator_RejectsRulesWithNoCompletionAndMalformedTournament()
    {
        var template = ValidTemplate();
        template.GameRules!.WinConditions = new() { Classic = false };
        AssertIssue(template, "win.none");
        template.GameRules.WinConditions = new() { Classic = true, Tournament = true, TournamentDays = [2, 2], TournamentAnnounceDays = [3], TournamentPointsToWin = 1 };
        AssertIssue(template, "tournament.arrayLengths");
    }

    [Fact]
    public void Validator_NullImportedEntriesProduceDiagnosticsInsteadOfCrashing()
    {
        var template = ValidTemplate();
        template.Variants![0].Zones![0].MainObjects!.Add(null!);
        template.Variants[0].Connections!.Add(null!);
        template.SpawnInfos = [null!];
        template.ZoneLayouts = [null!];
        AssertIssue(template, "node.null");
    }

    [Fact]
    public void Validator_ArenaConnectionIsAValidFinalBattleTarget()
    {
        var template = ValidTemplate();
        template.GameRules!.WinConditions = new() { Classic = true, GladiatorArena = true, GladiatorArenaCountDay = 1 };
        template.Variants![0].Connections![0].ConnectionType = "GladiatorArena";
        AssertNoErrors(template);
    }

    [Fact]
    public void Validator_RejectsBackwardsBattleDatesAndAllowsZeroDurationForLaterNativeRound()
    {
        var template = ValidTemplate();
        var win = template.GameRules!.WinConditions = new() { Classic = true, Tournament = true, TournamentDays = [100, 1], TournamentAnnounceDays = [1, 2], TournamentPointsToWin = 1 };
        AssertIssue(template, "tournament.fightOrder");
        win.TournamentDays = [1, 0]; win.TournamentAnnounceDays = [1, 3];
        AssertNoErrors(template);
        win.TournamentDays = [0]; win.TournamentAnnounceDays = [1];
        AssertIssue(template, "tournament.firstDay");
    }

    [Fact]
    public void Validator_LegacyAllVariantOverrideAndTournamentIslandsRemainSupported()
    {
        var template = ValidTemplate();
        template.ValueOverrides = [new() { Sid = "scroll_box", Variant = -1, GuardValue = 1000 }];
        template.Variants![0].Connections = [];
        template.GameRules!.WinConditions = new() { Classic = true, Tournament = true, TournamentDays = [2], TournamentAnnounceDays = [1], TournamentPointsToWin = 1 };
        AssertNoErrors(template);
    }

    [Fact]
    public void Validator_NativeBudgetOverflowRiskIsExplicitAndUnknownEnumsAreToleratedOnlyOnImport()
    {
        var template = ValidTemplate();
        template.SizeX = template.SizeZ = 512;
        var variant = Assert.Single(template.Variants!);
        var zone = Assert.IsType<List<Zone>>(variant.Zones)[0];
        zone.GuardedContentValuePerArea = 100000;
        AssertIssue(template, "budget.overflowRisk");
        zone.GuardedContentValuePerArea = 1000;
        Assert.Single(variant.Connections!).ConnectionType = "UnknownFutureType";
        AssertIssue(template, "value.unknown");
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(template, TemplateValidationMode.TolerantImport), i => i.Severity == TemplateIssueSeverity.Error);
    }

    [Fact]
    public void Validator_RejectsInlineWeightsOutsideNativeFloatAndSumRanges()
    {
        var template = ValidTemplate();
        template.ContentPools = [JsonSerializer.Deserialize<JsonElement>("""{"name":"overflow","groups":[{"weight":1e50,"content":[{"sid":"gold","weight":1}]}]}""")];
        AssertIssue(template, "pool.weightRange");
        template.ContentPools = [JsonSerializer.Deserialize<JsonElement>("""{"name":"overflow","groups":[{"weight":3e38},{"weight":3e38}]}""")];
        AssertIssue(template, "pool.weightSum");
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(template, TemplateValidationMode.TolerantImport), i => i.Severity == TemplateIssueSeverity.Error);
        template.ContentPools = [JsonSerializer.Deserialize<JsonElement>("""{"name":"fractional","groups":[{"weight":0.25,"content":[{"sid":"gold","weight":1.75}]}]}""")];
        AssertNoErrors(template);
    }

    [Fact]
    public void Validator_DoesNotSilentlySkipMalformedInlineReferenceTypes()
    {
        var template = ValidTemplate();
        template.ContentLists = [JsonSerializer.Deserialize<JsonElement>("""{"name":"list","content":[]}""")];
        template.ContentPools = [JsonSerializer.Deserialize<JsonElement>("""{"name":"pool","groups":[{"includeLists":["list",false,null],"content":[{"sid":12,"weight":1}],"weight":1}]}""")];
        var errors = TemplateCompatibilityValidator.Validate(template).Where(i => i.Code == "reference.type").ToArray();
        Assert.Equal(3, errors.Length);
        Assert.All(errors, error => Assert.Equal(TemplateIssueSeverity.Error, error.Severity));
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(template, TemplateValidationMode.TolerantImport), i => i.Severity == TemplateIssueSeverity.Error);
    }

    [Fact]
    public void Validator_UsesNativeFloatReactionWeightsRatherThanOnlyDoubleValidity()
    {
        var template = ValidTemplate();
        var zone = template.Variants![0].Zones![0];
        zone.GuardReactionDistribution = [1e50, 0, 0, 0, 0, 0];
        AssertIssue(template, "number.nativeFloat");
        zone.GuardReactionDistribution = [1e-50, 0, 0, 0, 0, 0];
        AssertIssue(template, "guard.reactionUnderflow");
        AssertIssue(template, "guard.reactionZero");
        zone.GuardReactionDistribution = [float.MaxValue, float.MaxValue, 0, 0, 0, 0];
        AssertIssue(template, "guard.reactionSum");
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(template, TemplateValidationMode.TolerantImport), i => i.Severity == TemplateIssueSeverity.Error);
        zone.GuardReactionDistribution = [.25, .75, 1.5, 0, 0, 0];
        AssertNoErrors(template);
    }

    [Theory]
    [InlineData("1e-50")]
    [InlineData("5e-324")]
    [InlineData("1e-400")]
    public void Validator_RejectsPositiveInlineWeightsThatDisappearInNativeFloats(string tinyWeight)
    {
        var template = ValidTemplate();
        template.ContentPools = [JsonSerializer.Deserialize<JsonElement>("{\"name\":\"tiny\",\"groups\":[{\"weight\":" + tinyWeight + ",\"content\":[{\"sid\":\"gold\",\"weight\":" + tinyWeight + "}]}]}")];
        Assert.Equal(2, TemplateCompatibilityValidator.Validate(template).Count(i => i.Code == "pool.weightUnderflow" && i.Severity == TemplateIssueSeverity.Error));
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(template, TemplateValidationMode.TolerantImport), i => i.Severity == TemplateIssueSeverity.Error);
    }

    [Fact]
    public void Validator_PreservesTheProvenNativeMinusOneContentVariantDefault()
    {
        var template = ValidTemplate();
        var item = new ContentItem { Sid = "gold", Variant = -1 };
        template.MandatoryContent = [new() { Name = "native-default", Content = [item] }];
        AssertNoErrors(template);
        Assert.Equal(-1, item.Variant);
        item.Variant = -2;
        AssertIssue(template, "number.variant");
    }

    [Fact]
    public void LayoutCompletionKeepsMalformedNodesForValidationWithoutDereferencingThem()
    {
        var template = ValidTemplate();
        var variants = Assert.IsType<List<Variant>>(template.Variants);
        var variant = Assert.Single(variants);
        var zones = Assert.IsType<List<Zone>>(variant.Zones);
        zones[0].Layout = "author-layout ";
        var layouts = new List<ZoneLayout> { null! };
        zones.Add(null!); variants.Add(null!); template.ZoneLayouts = layouts;
        Assert.Equal(new[] { "author-layout " }, TemplateGenerator.EnsureZoneLayoutsDefined(template));
        Assert.Equal("author-layout ", Assert.Single(layouts.OfType<ZoneLayout>()).Name);
        Assert.Null(layouts[0]); Assert.Null(variants[1]); Assert.Null(zones[^1]);
        AssertIssue(template, "node.null");
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(template, TemplateValidationMode.TolerantImport), i => i.Severity == TemplateIssueSeverity.Error);
    }

    [Theory]
    [InlineData("-2", "number.variant")]
    [InlineData("\"invalid\"", "content.variantType")]
    [InlineData("1.5", "content.variantType")]
    public void Validator_ChecksKnownInlineContentWeightVariantsWithoutInspectingFutureExtensions(string variant, string code)
    {
        var template = ValidTemplate();
        template.ContentLists = [JsonSerializer.Deserialize<JsonElement>("{\"name\":\"list\",\"content\":[{\"sid\":\"gold\",\"variant\":" + variant + ",\"weight\":1}],\"future\":{\"variant\":\"preserve\"}}")];
        template.ContentPools = [JsonSerializer.Deserialize<JsonElement>("{\"name\":\"pool\",\"groups\":[{\"content\":[{\"sid\":\"gold\",\"variant\":" + variant + ",\"weight\":1}],\"weight\":1}],\"future\":{\"variant\":-99}}")];
        var errors = TemplateCompatibilityValidator.Validate(template).Where(issue => issue.Code == code).ToArray();
        Assert.Equal(2, errors.Length);
        Assert.All(errors, error => Assert.Equal(TemplateIssueSeverity.Error, error.Severity));
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(template, TemplateValidationMode.TolerantImport), issue => issue.Severity == TemplateIssueSeverity.Error);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("null")]
    public void Validator_AllowsNativeInlineVariantDefaultsAndScalarContentShapes(string variant)
    {
        var template = ValidTemplate();
        template.ContentLists = [JsonSerializer.Deserialize<JsonElement>("{\"name\":\"list\",\"content\":{\"sid\":\"gold\",\"variant\":" + variant + ",\"weight\":1},\"future\":{\"variant\":\"preserve\"}}")];
        template.ContentPools = [JsonSerializer.Deserialize<JsonElement>("{\"name\":\"pool\",\"groups\":{\"content\":{\"sid\":\"gold\",\"variant\":" + variant + ",\"weight\":1},\"weight\":1},\"future\":{\"variant\":-99}}")];
        AssertNoErrors(template);
        string before = JsonSerializer.Serialize(template, JsonExport.Options);
        TemplateCompatibilityValidator.Validate(template);
        Assert.Equal(before, JsonSerializer.Serialize(template, JsonExport.Options));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Validator_RejectsUnknownConfiguredRewardIndicesAcrossMandatoryListsAndPools(bool collapsed)
    {
        var template = ValidTemplate();
        template.MandatoryContent = [new() { Name = "configured", Content = [new() { Sid = "scroll_box", Variant = 999 }] }];
        string content = "{\"sid\":\"scroll_box\",\"variant\":999,\"weight\":1}";
        if (!collapsed) content = "[" + content + "]";
        template.ContentLists = [JsonSerializer.Deserialize<JsonElement>("{\"name\":\"list\",\"content\":" + content + ",\"future\":{\"sid\":\"scroll_box\",\"variant\":999}}")];
        string group = "{\"weight\":1,\"content\":" + content + "}";
        if (!collapsed) group = "[" + group + "]";
        template.ContentPools = [JsonSerializer.Deserialize<JsonElement>("{\"name\":\"pool\",\"groups\":" + group + "}")];
        template.ValueOverrides = [new() { Sid = "scroll_box", Variant = 999, GuardValue = 1000 }];
        string before = JsonSerializer.Serialize(template, JsonExport.Options);
        var catalog = RewardVariantCatalog();
        var errors = TemplateCompatibilityValidator.Validate(template, catalog: catalog)
            .Where(issue => issue.Code == "content.unknownVariant").ToArray();
        Assert.Equal(3, errors.Length);
        Assert.All(errors, issue => Assert.Equal(TemplateIssueSeverity.Error, issue.Severity));
        Assert.Contains(errors, issue => issue.Path == "mandatoryContent[0].content[0].variant");
        Assert.Contains(errors, issue => issue.Path == "contentLists[0].content[0].variant");
        Assert.Contains(errors, issue => issue.Path == "contentPools[0].groups[0].content[0].variant");
        var imported = TemplateCompatibilityValidator.Validate(template, TemplateValidationMode.TolerantImport, catalog);
        Assert.Equal(3, imported.Count(issue => issue.Code == "content.unknownVariant" && issue.Severity == TemplateIssueSeverity.Warning));
        Assert.DoesNotContain(imported, issue => issue.Severity == TemplateIssueSeverity.Error);
        Assert.Equal(before, JsonSerializer.Serialize(template, JsonExport.Options));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    public void Validator_PreservesDefaultsAndAcceptsOnlyActualConfiguredIndicesWhenMetadataExists(int? variant)
    {
        var template = ValidTemplate();
        var mandatoryItem = new ContentItem { Sid = "scroll_box", Variant = variant };
        template.MandatoryContent = [new() { Name = "configured", Content = [mandatoryItem] }];
        var content = new { sid = "scroll_box", variant, weight = 1 };
        template.ContentLists = [JsonSerializer.SerializeToElement(new { name = "list", content = new[] { content } })];
        template.ContentPools = [JsonSerializer.SerializeToElement(new { name = "pool", groups = new[] { new { weight = 1, content = new[] { content } } } })];
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(template, catalog: RewardVariantCatalog()), issue => issue.Severity == TemplateIssueSeverity.Error);
        mandatoryItem.Variant = 1;
        // Metadata may be sparse: index 1 must not be invented from a maximum or count.
        Assert.Contains(TemplateCompatibilityValidator.Validate(template, catalog: RewardVariantCatalog()), issue => issue.Code == "content.unknownVariant");
    }

    [Fact]
    public void Validator_DoesNotGuessRewardVariantsForMissingEmptyOrDifferentExactSidMetadata()
    {
        var template = ValidTemplate();
        var trailingSidItem = new ContentItem { Sid = "scroll_box ", Variant = 999 };
        template.MandatoryContent = [new() { Name = "author", Content = [trailingSidItem, new() { Sid = "empty", Variant = 999 }] }];
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(template), issue => issue.Severity == TemplateIssueSeverity.Error);
        var catalog = new TemplateReferenceCatalog
        {
            ObjectRewardVariantIndices = new Dictionary<string, IReadOnlySet<int>>(StringComparer.Ordinal)
            { ["scroll_box"] = new HashSet<int> { 0, 2 }, ["empty"] = new HashSet<int>() },
        };
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(template, catalog: catalog), issue => issue.Severity == TemplateIssueSeverity.Error);
        trailingSidItem.Sid = "scroll_box";
        Assert.Single(TemplateCompatibilityValidator.Validate(template, catalog: catalog), issue => issue.Code == "content.unknownVariant");
    }

    [Fact]
    public void RawPropertiesDraftCannotIntroduceAnUnknownKnownObjectRewardIndex()
    {
        var template = ValidTemplate();
        var item = new ContentItem { Sid = "scroll_box", Variant = 2 };
        template.MandatoryContent = [new() { Name = "configured", Content = [item] }];
        var draft = new TemplatePropertiesDraft(template, RewardVariantCatalog());
        var candidate = draft.Snapshot();
        candidate["mandatoryContent"]![0]!["content"]![0]!["variant"] = 999;
        Assert.Contains("configured reward variant index 999", Assert.Throws<InvalidOperationException>(() => draft.Commit(candidate, out _)).Message);
        Assert.Equal(2, item.Variant);
    }

    [Fact]
    public void RawPropertiesDraftKeepsAnImportedUnknownRewardIndexFindingAfterAnUnrelatedDescriptionEdit()
    {
        var template = ValidTemplate();
        template.MandatoryContent = [new() { Name = "imported", Content = [new() { Sid = "scroll_box", Variant = 999 }] }];
        string before = JsonSerializer.Serialize(template, JsonExport.Options);
        var draft = new TemplatePropertiesDraft(template, RewardVariantCatalog());
        var candidate = draft.Snapshot();
        candidate["description"] = "Edited author description";
        var committed = draft.Commit(candidate, out var findings);
        Assert.Equal("Edited author description", committed.Description);
        var issue = Assert.Single(findings, finding => finding.Code == "content.unknownVariant");
        Assert.Equal(TemplateIssueSeverity.Error, issue.Severity);
        Assert.Equal("mandatoryContent[0].content[0].variant", issue.Path);
        Assert.Equal(before, JsonSerializer.Serialize(template, JsonExport.Options));
    }

    private static TemplateReferenceCatalog RewardVariantCatalog() => new()
    {
        ObjectRewardVariantIndices = new Dictionary<string, IReadOnlySet<int>>(StringComparer.Ordinal)
        { ["scroll_box"] = new HashSet<int> { 0, 2 } },
        Source = "Exact reward indices from the test configuration",
    };

    private static RmgTemplate ValidTemplate() => new()
    {
        Name = "Validation test", SizeX = 128, SizeZ = 128, GameMode = "Classic",
        GameRules = new() { HeroCountMin = 1, HeroCountMax = 10, WinConditions = new() { Classic = true } },
        Variants = [new()
        {
            Zones = [new() { Name = "A", MainObjects = [new() { Type = "Spawn", Spawn = "Player1" }] },
                     new() { Name = "B", MainObjects = [new() { Type = "Spawn", Spawn = "Player2" }] }],
            Connections = [new() { Name = "AB", From = "A", To = "B", ConnectionType = "Direct" }],
        }],
    };

    private static void AssertNoErrors(RmgTemplate template) =>
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(template), i => i.Severity == TemplateIssueSeverity.Error);

    private static void AssertIssue(RmgTemplate template, string code) =>
        Assert.Contains(TemplateCompatibilityValidator.Validate(template), i => i.Code == code && i.Severity == TemplateIssueSeverity.Error);

    private static void AssertJsonEqual(JsonElement expected, JsonElement actual)
    {
        Assert.Equal(expected.ValueKind, actual.ValueKind);
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var before = expected.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                var after = actual.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                Assert.Equal(before.Select(p => p.Name), after.Select(p => p.Name));
                for (int i = 0; i < before.Length; i++) AssertJsonEqual(before[i].Value, after[i].Value);
                break;
            case JsonValueKind.Array:
                Assert.Equal(expected.GetArrayLength(), actual.GetArrayLength());
                for (int i = 0; i < expected.GetArrayLength(); i++) AssertJsonEqual(expected[i], actual[i]);
                break;
            case JsonValueKind.Number: Assert.Equal(expected.GetDouble(), actual.GetDouble()); break;
            default: Assert.Equal(expected.GetRawText(), actual.GetRawText()); break;
        }
    }
}
