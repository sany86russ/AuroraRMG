using System.Text.Json;
using System.Text.Json.Nodes;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Export;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public class SettingsSnapshotMapperTests
{
    [Fact]
    public void ActualGeneratorInputRoundTripsThroughPersistedSettingsWithoutUiProjectionLoss()
    {
        var original = Input();
        var file = SettingsSnapshotMapper.FromGeneratorSettings(original);
        var persisted = JsonSerializer.Serialize(file, JsonExport.Options);
        var restored = Presets.ToGeneratorSettings(JsonSerializer.Deserialize<SettingsFile>(persisted, JsonExport.Options)!);
        AssertEquivalent(original, restored);
        Assert.Equal("Arena", file.GameMode); Assert.True(file.MatchSpawnTerrainToFaction);
        Assert.Equal(78, file.BorderGuardStrengthPercent); Assert.Equal("Human", file.NeutralCastleFaction);
        Assert.DoesNotContain("DotBrush", persisted); Assert.DoesNotContain("DisplayName", persisted);
        Assert.DoesNotContain("ReceiverLabel", persisted);
        restored.PlayerZoneMandatoryContent[0].Rules![0].Args![0] = "changed";
        restored.ProfileOptions.ImportedNativeSchema!["future"] = 99;
        restored.Bonuses[0].Param = "changed";
        Assert.Equal("original", original.PlayerZoneMandatoryContent[0].Rules![0].Args![0]);
        Assert.Equal(0.75, original.ProfileOptions.ImportedNativeSchema!["future"]!.GetValue<double>());
        Assert.Equal("1250", original.Bonuses[0].Param);
    }

    [Fact]
    public void UnchangedUiBaselinePreservesOpaqueRulesPoolReferencesVariantsAndNativeSource()
    {
        var original = Input(); var ui = SettingsSnapshotMapper.Clone(original);
        ui.PlayerZoneMandatoryContent = [new ContentItem { Sid = "scroll_box", IsGuarded = true }];
        ui.ProfileOptions.ImportedNativeConfiguration = null;
        var merged = GeneratorSettingsOverlay.Merge(original, ui, SettingsSnapshotMapper.Clone(ui));
        AssertEquivalent(original, merged);
        merged.PlayerZoneMandatoryContent[0].Variant = 77;
        Assert.Equal(2, original.PlayerZoneMandatoryContent[0].Variant);
    }

    [Fact]
    public void ChangedUiGroupsReplaceOnlyThoseGroupsWithoutMutatingAnyInput()
    {
        var original = Input(); var baseline = SettingsSnapshotMapper.Clone(original); var current = SettingsSnapshotMapper.Clone(baseline);
        baseline.PlayerZoneMandatoryContent = []; current.PlayerZoneMandatoryContent = [];
        current.ZoneCfg.Advanced.NeutralHighCastleCount = 8;
        current.BannedItems = "new_ban";
        current.HeroSettings.HeroCountMax = 10;
        var merged = GeneratorSettingsOverlay.Merge(original, baseline, current);
        Assert.Equal(8, merged.ZoneCfg.Advanced.NeutralHighCastleCount);
        Assert.Equal("new_ban", merged.BannedItems); Assert.Equal(10, merged.HeroSettings.HeroCountMax);
        Assert.Equal(2, merged.PlayerZoneMandatoryContent[0].Variant);
        Assert.Equal(3, original.ZoneCfg.Advanced.NeutralHighCastleCount);
        Assert.Equal(3, baseline.ZoneCfg.Advanced.NeutralHighCastleCount);
        merged.ZoneCfg.Advanced.NeutralHighCastleCount = 15;
        Assert.Equal(8, current.ZoneCfg.Advanced.NeutralHighCastleCount);
    }

    [Fact]
    public void LegacyUnsetPlayerRowsRetainFreshMinesButExplicitEmptyIsEmpty()
    {
        var file = new SettingsFile();
        var a = Presets.ToGeneratorSettings(file); var b = Presets.ToGeneratorSettings(file);
        Assert.Equal(3, a.PlayerZoneMandatoryContent.Count);
        a.PlayerZoneMandatoryContent[0].Sid = "changed";
        Assert.NotEqual("changed", b.PlayerZoneMandatoryContent[0].Sid);
        file.PlayerZoneContentRows = [];
        Assert.Empty(Presets.ToGeneratorSettings(file).PlayerZoneMandatoryContent);
    }

    [Fact]
    public void LegacyHeadlessConversionIncludesBansBonusesAndAllMandatoryRowKinds()
    {
        var file = new SettingsFile
        {
            GameMode = "Arena", MatchSpawnTerrainToFaction = true,
            BannedItems = "artifact", BannedMagics = "spell", BannedHeroes = "hero", ValueOverridesText = "object 1.5",
            BonusesJson = "StartingGold|start_hero|2000|0\n2|all_heroes|1.25|0",
            PlayerZoneContentRows = [new() { Sid = "exact_sid ", Count = 2, IsMine = true, IsGuarded = true, NearCastle = true, RoadDistance = "Near" }],
            LowNeutralContentRows = [new() { Sid = "pool_list", IsGroup = true, Count = 3, RoadDistance = "Far" }],
            MediumNeutralContentRows = [new() { Sid = "medium" }], HighNeutralContentRows = [new() { Sid = "high" }], HubZoneContentRows = [new() { Sid = "hub" }],
        };
        var settings = Presets.ToGeneratorSettings(file);
        Assert.Equal("Arena", settings.GameMode); Assert.True(settings.MatchSpawnTerrainToFaction);
        Assert.Equal("artifact", settings.BannedItems); Assert.Equal("spell", settings.BannedMagics); Assert.Equal("hero", settings.BannedHeroes);
        Assert.Equal(file.ValueOverridesText, settings.ValueOverridesText); Assert.Equal(2, settings.Bonuses.Count);
        Assert.Equal(2, settings.PlayerZoneMandatoryContent.Count); Assert.Equal("exact_sid ", settings.PlayerZoneMandatoryContent[0].Sid);
        Assert.Equal(2, settings.PlayerZoneMandatoryContent[0].Rules!.Count);
        Assert.Equal(3, settings.LowNeutralMandatoryContent.Count); Assert.Equal("pool_list", settings.LowNeutralMandatoryContent[0].IncludeLists![0]);
        Assert.Single(settings.MediumNeutralMandatoryContent); Assert.Single(settings.HighNeutralMandatoryContent); Assert.Single(settings.HubZoneMandatoryContent);
        settings.PlayerZoneMandatoryContent[0].Rules![0].Args![0] = "changed";
        Assert.Equal("0", settings.PlayerZoneMandatoryContent[1].Rules![0].Args![0]);
    }

    [Theory]
    [InlineData("not-a-bonus")]
    [InlineData("999|start_hero|1|0")]
    public void InvalidSavedBonusIsReportedInsteadOfSilentlyDropped(string bonus)
    { Assert.Throws<JsonException>(() => Presets.ToGeneratorSettings(new SettingsFile { BonusesJson = bonus })); }

    [Fact]
    public void InvalidOrUnsupportedSnapshotCannotSilentlyUseDifferentDefaults()
    {
        var file = new SettingsFile { GeneratorSnapshot = JsonSerializer.SerializeToElement(new[] { 1 }) };
        Assert.Throws<JsonException>(() => Presets.ToGeneratorSettings(file));
        file.GeneratorSnapshot = JsonSerializer.SerializeToElement(new GeneratorSettings()); file.GeneratorSnapshotVersion = 99;
        Assert.Throws<JsonException>(() => Presets.ToGeneratorSettings(file));
    }

    [Fact]
    public void BatchCloneCapturesBonusInputsWithoutSerializingWpfOutputs()
    { var original = Input(); var clone = TemplateBatchService.CloneSettings(original); AssertEquivalent(original, clone); clone.Bonuses[0].Param = "changed"; Assert.Equal("1250", original.Bonuses[0].Param); }

    [Fact]
    public void MissingSeedFallsBackToProfileSeedInDetachedCloneAndPersistedSnapshot()
    {
        var original = new GeneratorSettings { Seed = null, ProfileOptions = new() { TemplateSeed = 42 } };
        var clone = SettingsSnapshotMapper.Clone(original);
        Assert.Equal(42, clone.Seed); Assert.Null(original.Seed);
        var file = SettingsSnapshotMapper.FromGeneratorSettings(original);
        Assert.Equal(42, file.Seed); Assert.Equal(42, file.GeneratorSnapshot!.Value.GetProperty("Seed").GetInt32());
        Assert.Equal(42, Presets.ToGeneratorSettings(file).Seed); Assert.Null(original.Seed);
    }

    [Fact]
    public void ExplicitSeedRemainsAuthoritativeAndCloneDoesNotRequireANonnullProfile()
    {
        var original = new GeneratorSettings { Seed = 7, ProfileOptions = new() { TemplateSeed = 42 } };
        Assert.Equal(7, SettingsSnapshotMapper.Clone(original).Seed); Assert.Equal(42, original.ProfileOptions.TemplateSeed);
        original.ProfileOptions = null!;
        Assert.Equal(7, SettingsSnapshotMapper.Clone(original).Seed); Assert.Null(original.ProfileOptions);
    }

    [Theory]
    [InlineData("{\"ProfileOptions\":null}")]
    [InlineData("{\"ZoneCfg\":null}")]
    [InlineData("{\"Bonuses\":[null]}")]
    [InlineData("{\"PlayerZoneMandatoryContent\":[null]}")]
    [InlineData("{\"ProfileOptions\":{\"Alliances\":{\"Slots\":[null]}}}")]
    public void NullSnapshotContainersAreReportedBeforeUiProjection(string json)
    {
        var file = new SettingsFile { GeneratorSnapshot = JsonSerializer.Deserialize<JsonElement>(json) };
        Assert.Throws<JsonException>(() => SettingsSnapshotMapper.RestoreSnapshot(file));
    }

    private static void AssertEquivalent(GeneratorSettings expected, GeneratorSettings actual) =>
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected, SettingsSnapshotMapper.SnapshotOptions), JsonSerializer.SerializeToNode(actual, SettingsSnapshotMapper.SnapshotOptions)));

    private static GeneratorSettings Input() => new()
    {
        TemplateName = "Снимок", GameMode = "Arena", Seed = 12345, MapSize = 128, PlayerCount = 3,
        MatchSpawnTerrainToFaction = true, MatchPlayerCastleFactions = true, NeutralCastleFaction = "Human", PlayerStartsWithCastles = true,
        MinNeutralZonesBetweenPlayers = 2, NoDirectPlayerConnections = true, GenerateRoads = false,
        RandomPortals = true, MaxPortalConnections = 7, SpawnRemoteFootholds = false,
        HeroSettings = new() { HeroCountMin = 1, HeroCountMax = 6, HeroCountIncrement = 0, HeroHireBan = true },
        ZoneCfg = new() { BorderGuardStrengthPercent = 78, ResourceDensityPercent = 140, StructureDensityPercent = 75, Advanced = new() { Enabled = false, NeutralHighCastleCount = 3, PlayerZoneSize = 1.75, GuardRandomization = 0.125 } },
        BannedItems = "artifact", BannedMagics = "spell", BannedHeroes = "hero", ValueOverridesText = "object=1.75",
        Bonuses = [new() { PresetType = BonusPresetType.StartingGold, Param = "1250" }],
        ProfileOptions = new() { ProfileId = "future-profile", TemplateSeed = 12345, ImportedNativeConfiguration = new JsonObject { ["future"] = "keep" }, ImportedNativeSchema = new JsonObject { ["future"] = 0.75 }, Alliances = new() { Enabled = true, Preset = "custom", Slots = [new() { Player = 1, Alliance = 0, Locked = true }] } },
        PlayerZoneMandatoryContent = [JsonSerializer.Deserialize<ContentItem>("{\"sid\":\"scroll_box\",\"variant\":2,\"isGuarded\":true,\"future\":{\"weight\":0.125},\"rules\":[{\"type\":\"FuturePlacement\",\"args\":[\"original\"],\"weight\":0.25,\"futureRule\":true}]}", JsonExport.Options)!],
        LowNeutralMandatoryContent = [new() { IncludeLists = ["custom_list", "other"], SoloEncounter = true }],
        GameEndConditions = new() { VictoryCondition = "win_condition_3", LostStartCity = true, LostStartCityDay = 4, CityHold = true, CityHoldDays = 9, HeroLighting = false },
        GladiatorArenaRules = new() { Enabled = true, DaysDelayStart = 35, CountDay = 5 }, TournamentRules = new() { Enabled = true, FirstTournamentDay = 22, Interval = 9, PointsToWin = 4, SaveArmy = false },
    };
}
