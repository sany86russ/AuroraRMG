using System.Text.Json;
using System.Text.Json.Nodes;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Models.NativeSchema;
using Olden_Era___Template_Editor.Services.NativeSchema;

namespace Olden_Era___Template_Editor.Tests;

public sealed class NativeConfigurationImportTests
{
    [Fact]
    public void CompactImportRetainsHeroLimitsAndUnknownRawDoesNotMutateTheCaller()
    {
        var current = new GeneratorSettings { MapSize = 160, HeroSettings = new() { HeroCountMin = 3, HeroCountMax = 7 } };
        var input = NativeConfigurationCodec.Decode(Convert.ToBase64String([4, 0, 0x88, 0x32]));
        var imported = NativeConfigurationImporter.Import(input, current);
        Assert.Equal(128, imported.Settings.MapSize); Assert.Equal(4, imported.Settings.PlayerCount);
        Assert.Equal(3, imported.Settings.HeroSettings.HeroCountMin); Assert.Equal(7, imported.Settings.HeroSettings.HeroCountMax);
        Assert.Equal(MapRichness.Normal, imported.ProfileOptions.Richness); Assert.Equal(MapChallenge.Unfair, imported.ProfileOptions.Challenge);
        Assert.Equal(GenerationProfileOptions.CurrentProfile, imported.ProfileOptions.ProfileId);
        Assert.Contains(imported.Warnings, item => item.Code == "HeroLimitsRetained");
        imported.Settings.HeroSettings.HeroCountMin = 1; imported.RawConfiguration["size"] = 256;
        Assert.Equal(3, current.HeroSettings.HeroCountMin); Assert.Equal(160, current.MapSize); Assert.Equal(128, input.MapSize);
    }

    [Fact]
    public void NativeDatesAreTranslatedToHumanDatesWithoutAnAdditionalFinalBattleOffset()
    {
        var final = NativeConfigurationImporter.Import(NativeConfigurationCodec.ParseJson(
            """{"flags":64,"size":128,"spawnCount":2,"finalBattleDay":16}"""), new());
        Assert.Equal(17, final.ProfileOptions.FinalBattleDay); Assert.Equal(17, final.Settings.GladiatorArenaRules.DaysDelayStart);
        Assert.Equal(1, final.Settings.GladiatorArenaRules.CountDay); Assert.False(final.ProfileOptions.ClassicVictoryEnabled);
        var tournament = NativeConfigurationImporter.Import(NativeConfigurationCodec.ParseJson(
            """{"flags":128,"size":128,"spawnCount":2,"tournamentDays":[1,8,15]}"""), new());
        Assert.Equal([2, 9, 16], tournament.ProfileOptions.TournamentBattleDays);
        Assert.True(tournament.Settings.TournamentRules.Enabled); Assert.Equal("win_condition_6", tournament.QuickOptions.VictoryCondition);
    }

    [Theory]
    [InlineData(8, "win_condition_3")]
    [InlineData(16, "win_condition_5")]
    [InlineData(32, "win_condition_8")]
    public void NativeVictoryFlagsSelectOnlySupportedWorkingAuroraModes(int flags, string expected)
    {
        var config = NativeConfigurationCodec.ParseJson($$"""{"flags":{{flags}},"size":128,"spawnCount":2}""");
        var result = NativeConfigurationImporter.Import(config, new());
        Assert.Equal(expected, result.Settings.GameEndConditions.VictoryCondition);
        if (flags == 8) { Assert.True(result.Settings.GameEndConditions.LostStartCity); Assert.Equal(3, result.Settings.GameEndConditions.LostStartCityDay); }
        if (flags == 16) { Assert.True(result.Settings.GameEndConditions.CityHold); Assert.Equal(3, result.Settings.GameEndConditions.CityHoldDays); }
        if (flags == 32) Assert.True(result.Settings.GameEndConditions.LostStartHero);
    }

    [Fact]
    public void ExplicitHeroBoundsSingleHeroAndUnknownFieldsArePreservedSeparately()
    {
        var input = NativeConfigurationCodec.ParseJson("""{"flags":7,"size":256,"spawnCount":8,"richness":4,"difficulty":5,"heroCountMin":1,"heroCountMax":10,"seed":45,"disableMagicGuild":true,"newOption":{"id":"Tail "}}""");
        var current = new GeneratorSettings { Seed = 27, ProfileOptions = new() { DisableMagicGuild = false } };
        var result = NativeConfigurationImporter.Import(input, current);
        Assert.True(result.Settings.SingleHeroMode); Assert.Equal("SingleHero", result.Settings.GameMode);
        Assert.Equal(1, result.Settings.HeroSettings.HeroCountMin); Assert.Equal(10, result.Settings.HeroSettings.HeroCountMax);
        Assert.Equal(MapSymmetry.EqualStarts, result.ProfileOptions.Symmetry);
        Assert.Equal(27, result.Settings.Seed); Assert.False(result.ProfileOptions.DisableMagicGuild);
        Assert.Equal("Tail ", result.RawConfiguration["newOption"]!["id"]!.GetValue<string>());
        Assert.Contains(result.Warnings, item => item.Code == "UnmappedInput" && item.Field == "seed");
        Assert.Contains(result.Warnings, item => item.Code == "UnmappedInput" && item.Field == "disableMagicGuild");
    }

    [Fact]
    public void UnsupportedRandomTeamsRetainExplicitRosterAndDoNotInventTeams()
    {
        var settings = new GeneratorSettings { ProfileOptions = new() { Alliances = new() { Enabled = true, Slots = [new() { Player = 1, Alliance = 1 }] } } };
        var result = NativeConfigurationImporter.Import(NativeConfigurationCodec.ParseJson("""{"flags":260,"size":128,"spawnCount":4}"""), settings);
        Assert.True(result.ProfileOptions.Alliances.Enabled); Assert.Single(result.ProfileOptions.Alliances.Slots);
        Assert.Contains(result.Warnings, item => item.Code == "RandomTeamsUnmapped");
        Assert.Contains(result.Warnings, item => item.Code == "ExistingAlliances");
        result.ProfileOptions.Alliances.Slots[0].Alliance = 9;
        Assert.Equal(1, settings.ProfileOptions.Alliances.Slots[0].Alliance);
    }

    [Fact]
    public void QuickImportPreservesItsSizeBandAndSeedWhileExplainingItsLimits()
    {
        var quick = new QuickGenerateOptions { GameType = QuickGameType.Duel, Scale = QuickMapScale.Small, Seed = 51 };
        var result = NativeConfigurationImporter.Import(NativeConfigurationCodec.ParseJson("""{"flags":4,"size":192,"spawnCount":4}"""), new(), quick);
        Assert.Equal(192, result.Settings.MapSize); Assert.Equal(QuickMapScale.Small, result.QuickOptions.Scale);
        Assert.Equal(51, result.QuickOptions.Seed); Assert.Equal(4, result.QuickOptions.PlayerCount);
        Assert.Equal(QuickGameType.FreeForAll, result.QuickOptions.GameType); Assert.Equal(QuickGameType.Duel, quick.GameType);
        Assert.Contains(result.Warnings, item => item.Code == "QuickUsesSizeBand");
    }

    [Theory]
    [InlineData("{\"flags\":68,\"size\":128,\"spawnCount\":2,\"finalBattleDay\":16}")]
    [InlineData("{\"flags\":192,\"size\":128,\"spawnCount\":2,\"finalBattleDay\":16,\"tournamentDays\":[16]}")]
    [InlineData("{\"flags\":128,\"size\":128,\"spawnCount\":4,\"tournamentDays\":[16]}")]
    [InlineData("{\"flags\":0,\"size\":128,\"spawnCount\":2}")]
    public void InvalidNativeModesFailBeforeChangingCurrentSettings(string json)
    {
        var settings = new GeneratorSettings { MapSize = 160 };
        Assert.Throws<NotSupportedException>(() => NativeConfigurationImporter.Import(NativeConfigurationCodec.ParseJson(json), settings));
        Assert.Equal(160, settings.MapSize);
    }

    [Fact]
    public void SchemaImportsOnlyUniformVerifiedGrowthAndRiverFields()
    {
        const string source = """
            {"river":{"createRiverSystem":true,"tributarySpacing":24,"later":7},
             "zoneClasses":[{"name":"Zone ","randomHireEnableWeeklyUnitIncrement":[{"fallback":true,"value":[true,true,true,true,true,true,true]}],
             "randomHireInitialUnitIncrement":[{"fallback":true,"value":[2,2,2,2,2,2,2]}]}],
             "mainObjectClasses":[{"name":"Town","type":"City","enableWeeklyUnitIncrement":[{"fallback":true,"value":true}],
             "weekOneOnlyModificatorGrowth":[{"fallback":true,"value":false}],"initialUnitIncrement":[{"fallback":true,"value":2}]}],"future":{"raw":true}}
            """;
        var schema = NativeSchemaDocument.Parse(source);
        var result = NativeConfigurationImporter.Import(NativeConfigurationCodec.ParseJson("""{"flags":4,"size":128,"spawnCount":2}"""), new(), schema: schema);
        Assert.True(result.ProfileOptions.EnableRivers); Assert.Equal(24, result.ProfileOptions.RiverTributarySpacing);
        Assert.True(result.ProfileOptions.EnableWeeklyUnitIncrement); Assert.False(result.ProfileOptions.WeekOneOnlyModificatorGrowth);
        Assert.Equal(2, result.ProfileOptions.InitialUnitIncrement); Assert.True(result.RawSchema!["future"]!["raw"]!.GetValue<bool>());
        result.RawSchema["future"]!["raw"] = false; Assert.True(schema.Root["future"]!["raw"]!.GetValue<bool>());
    }

    [Fact]
    public void RoleDependentGrowthIsNotFlattenedOrGuessed()
    {
        var schema = NativeSchemaDocument.Parse("""{"zoneClasses":[],"mainObjectClasses":[{"name":"A","type":"City","weekOneOnlyModificatorGrowth":[{"fallback":true,"value":true}]},{"name":"B","type":"City","weekOneOnlyModificatorGrowth":[{"fallback":true,"value":false}]}]}""");
        var result = NativeConfigurationImporter.Import(NativeConfigurationCodec.ParseJson("""{"flags":4,"size":128,"spawnCount":2}"""),
            new() { ProfileOptions = new() { WeekOneOnlyModificatorGrowth = false } }, schema: schema);
        Assert.False(result.ProfileOptions.WeekOneOnlyModificatorGrowth);
        Assert.Contains(result.Warnings, item => item.Code == "SchemaGrowthNotUniform" && item.Field == "weekOneOnlyModificatorGrowth");
    }

    [Fact]
    public void DirectConfigurationCannotTruncateUnknownFlagsViaUnderlyingUshort()
        => Assert.Throws<NotSupportedException>(() => NativeConfigurationCodec.Validate(new(new JsonObject { ["flags"] = 65536, ["size"] = 128, ["spawnCount"] = 2 }, false)));
}
