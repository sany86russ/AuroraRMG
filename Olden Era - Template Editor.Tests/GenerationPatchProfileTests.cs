using System.Text.Json;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Analysis;
using Olden_Era___Template_Editor.Services.Generation;
using Olden_Era___Template_Editor.Services.Localization;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public class GenerationPatchProfileTests
{
    [Theory]
    [InlineData("1v7", 8, 2)]
    [InlineData("2v1", 3, 2)]
    [InlineData("3v3", 6, 2)]
    [InlineData("2v2v2v2", 8, 4)]
    public void ArbitraryTeams_ExportActualDenseOneBasedAssignments(string composition, int players, int teams)
    {
        var settings = Settings(players);
        settings.ProfileOptions.Alliances = new() { Enabled = true, Preset = composition, Locked = true };
        var template = TemplateGenerator.Generate(settings);
        Assert.Equal(players, template.SpawnInfos!.Count);
        Assert.Equal(teams, template.MaxAlliancesCount);
        Assert.Equal(Enumerable.Range(1, teams), template.SpawnInfos.Select(s => s.AllianceIndex!.Value).Distinct().Order());
        Assert.All(template.SpawnInfos, slot => Assert.True(slot.IsAllianceLocked));
        Assert.Equal(Enumerable.Range(1, players).Select(p => $"Player{p}"), template.SpawnInfos.Select(s => s.Spawn));
        AssertNoErrors(template);
    }

    [Fact]
    public void IndividualPlayersAreSeparateTeamsAndManualIndicesRetainMembership()
    {
        var plan = AlliancePlan.Resolve(4, new()
        {
            Enabled = true, Slots = [new() { Player = 1, Alliance = 7 }, new() { Player = 2, Alliance = 7 }, new() { Player = 3, Alliance = -1 }, new() { Player = 4, Alliance = -1 }],
        });
        Assert.Equal(3, plan.MaximumAlliances);
        Assert.True(plan.AreAllies("Player1", "Player2"));
        Assert.False(plan.AreAllies("Player3", "Player4"));
        Assert.Equal(new int?[] { 1, 1, -1, -1 }, plan.ToSpawnInfos().Select(s => s.AllianceIndex));
        Assert.Throws<ArgumentException>(() => AlliancePlan.Resolve(3, new() { Enabled = true, Preset = "2v2" }));
        Assert.Throws<ArgumentException>(() => AlliancePlan.Resolve(2, new() { Enabled = true, Slots = [new() { Player = 1 }, new() { Player = 1 }] }));
    }

    [Theory]
    [InlineData(AllianceLayout.TeamRegions)]
    [InlineData(AllianceLayout.TeamLanes)]
    [InlineData(AllianceLayout.TeamArena)]
    public void TeamLayouts_KeepAlliesConnectedWithoutDirectEnemyStartsAcrossSeeds(AllianceLayout layout)
    {
        for (int seed = 0; seed < 12; seed++)
        {
            var settings = Settings(3, seed);
            settings.ProfileOptions.Alliances = new() { Enabled = true, Preset = "2v1", Layout = layout };
            settings.NoDirectPlayerConnections = true;
            var template = TemplateGenerator.Generate(settings);
            var variant = template.Variants![0];
            var starts = variant.Zones!.Where(z => z.MainObjects?.Any(o => o.Type == "Spawn") == true).ToDictionary(z => z.Name, z => z.MainObjects!.First(o => o.Type == "Spawn").Spawn!);
            var plan = AlliancePlan.Resolve(3, settings.ProfileOptions.Alliances);
            Assert.DoesNotContain(variant.Connections!, c => starts.TryGetValue(c.From, out string? a) && starts.TryGetValue(c.To, out string? b) && !plan.AreAllies(a, b));
            Assert.Contains(variant.Connections!, c => c.GuardValue == 0 && starts.TryGetValue(c.From, out string? a) && starts.TryGetValue(c.To, out string? b) && plan.AreAllies(a, b));
            AssertNoErrors(template);
            var balance = TemplateBalanceReport.Analyze(template);
            Assert.Equal(2, balance.Teams.Count);
            Assert.All(balance.Players, p => Assert.True(p.NearestOpponentHops >= 3));
        }
    }

    [Fact]
    public void SimpleTeam_CurrentOddPlayerCountIsPreservedAndManualTournamentConflictIsExplicit()
    {
        var options = new QuickGenerateOptions { Seed = 19, PlayerCount = 3, GameType = QuickGameType.Team, ProfileOptions = GenerationProfileOptions.CurrentDefaults() };
        var settings = RandomTemplateBuilder.Build(options);
        Assert.Equal(3, settings.PlayerCount);
        Assert.True(settings.ProfileOptions.Alliances.Enabled);
        Assert.Equal(3, TemplateGenerator.Generate(settings).SpawnInfos!.Count);
        options.VictoryCondition = "win_condition_6";
        options.ProfileOptions.Alliances = new() { Enabled = true, Preset = "2v1" };
        Assert.Throws<ArgumentException>(() => RandomTemplateBuilder.Build(options));
    }

    [Fact]
    public void CurrentHugeMapsUseSupported256AndKeepSeedDeterminism()
    {
        var options = new QuickGenerateOptions { Seed = 808, Scale = QuickMapScale.Huge, ProfileOptions = GenerationProfileOptions.CurrentDefaults() };
        var first = RandomTemplateBuilder.Build(options);
        var second = RandomTemplateBuilder.Build(options);
        Assert.Equal(256, first.MapSize);
        Assert.Equal(JsonSerializer.Serialize(first, JsonExport.Options), JsonSerializer.Serialize(second, JsonExport.Options));
        Assert.Equal(JsonSerializer.Serialize(TemplateGenerator.Generate(first), JsonExport.Options), JsonSerializer.Serialize(TemplateGenerator.Generate(second), JsonExport.Options));
    }

    [Fact]
    public void RichnessIsMonotonicAndChallengeDoesNotIncreaseTheEconomy()
    {
        var settings = Settings();
        settings.ProfileOptions.Richness = MapRichness.VeryPoor;
        var poor = TemplateGenerator.Generate(settings);
        settings.ProfileOptions.Richness = MapRichness.VeryRich;
        var rich = TemplateGenerator.Generate(settings);
        Assert.True(Start(rich).GuardedContentValue > Start(poor).GuardedContentValue);
        settings.ProfileOptions.Challenge = MapChallenge.Easy;
        var easy = TemplateGenerator.Generate(settings);
        settings.ProfileOptions.Challenge = MapChallenge.Apocalypse;
        var hard = TemplateGenerator.Generate(settings);
        Assert.Equal(Start(easy).GuardedContentValue, Start(hard).GuardedContentValue);
        Assert.Equal(Start(easy).ResourcesValue, Start(hard).ResourcesValue);
        Assert.True(Start(hard).GuardMultiplier > Start(easy).GuardMultiplier);
        Assert.True(hard.Variants![0].Connections!.Max(c => c.GuardValue) > easy.Variants![0].Connections!.Max(c => c.GuardValue));
    }

    [Fact]
    public void NativeAreaBudgetUsesRealCoefficientsWithoutGuessingAnAreaOrAddingAbsoluteBudget()
    {
        var settings = Settings();
        settings.ProfileOptions.UseAreaBudgets = true;
        var start = Start(TemplateGenerator.Generate(settings));
        Assert.Equal(0, start.GuardedContentValue); Assert.Equal(0, start.UnguardedContentValue); Assert.Equal(0, start.ResourcesValue);
        Assert.Equal(2200, start.GuardedContentValuePerArea);
        Assert.Equal(490, start.UnguardedContentValuePerArea);
        Assert.Equal(400, start.ResourcesValuePerArea);
        settings.ProfileOptions.Richness = MapRichness.VeryRich;
        Assert.Equal(3500, Start(TemplateGenerator.Generate(settings)).GuardedContentValuePerArea);
    }

    [Fact]
    public void NeutralTownCountsAreIndependentOfRichnessAndRespectPerZoneCap()
    {
        var settings = Settings();
        settings.ZoneCfg.NeutralZoneCount = 4;
        settings.ProfileOptions.NeutralTownCount = 3;
        settings.ProfileOptions.MaximumTownsPerZone = 1;
        foreach (MapRichness richness in Enum.GetValues<MapRichness>())
        {
            settings.ProfileOptions.Richness = richness;
            var template = TemplateGenerator.Generate(settings);
            var neutral = template.Variants![0].Zones!.Where(z => !z.MainObjects!.Any(o => o.Type == "Spawn")).ToList();
            Assert.Equal(3, neutral.Sum(z => z.MainObjects!.Count(o => o.Type == "City")));
            Assert.All(neutral, z => Assert.True(z.MainObjects!.Count(o => o.Type == "City") <= 1));
            AssertNoErrors(template);
        }
        settings.GameEndConditions.VictoryCondition = "win_condition_5";
        settings.ProfileOptions.NeutralTownCount = 0;
        Assert.Throws<ArgumentException>(() => TemplateGenerator.Generate(settings));
    }

    [Fact]
    public void GuardianMagicRiversGrowthAndPortalOptionsFlowToSupportedFields()
    {
        var settings = Settings();
        settings.GameEndConditions.VictoryCondition = "win_condition_8";
        settings.RandomPortals = true;
        settings.ProfileOptions.EnableRivers = true; settings.ProfileOptions.RiverTributarySpacing = 23.5;
        settings.ProfileOptions.EnableWeeklyUnitIncrement = false; settings.ProfileOptions.WeekOneOnlyModificatorGrowth = true; settings.ProfileOptions.InitialUnitIncrement = 4;
        settings.ProfileOptions.DisableFactionLaws = true; settings.ProfileOptions.DisableMagicGuild = true; settings.ProfileOptions.DisableMagicCustomLearning = true;
        settings.ProfileOptions.PortalFromEnabled = false; settings.ProfileOptions.PortalToEnabled = true; settings.ProfileOptions.BorderGuardReaction = "Friendly";
        var template = TemplateGenerator.Generate(settings);
        Assert.True(template.GameRules!.WinConditions!.LostStartHero);
        Assert.True(template.GameRules.DisableFactionLaws); Assert.True(template.GameRules.DisableMagicGuild); Assert.True(template.GameRules.DisableMagicCustomLearning);
        Assert.Equal(23.5, template.Variants![0].River!.TributarySpacing);
        Assert.All(template.Variants[0].Zones!, zone =>
        {
            Assert.Equal(7, zone.RandomHireEnableWeeklyUnitIncrement!.Count);
            Assert.All(zone.RandomHireEnableWeeklyUnitIncrement, value => Assert.False(value));
            Assert.All(zone.RandomHireInitialUnitIncrement!, value => Assert.Equal(4, value));
            Assert.All(zone.MainObjects!.Where(o => o.Type == "Spawn"), spawn =>
            { Assert.True(spawn.IsKeyObject); Assert.False(spawn.EnableWeeklyUnitIncrement); Assert.True(spawn.WeekOneOnlyModificatorGrowth); Assert.Equal(4, spawn.InitialUnitIncrement); });
        });
        Assert.All(template.Variants[0].Connections!, c => Assert.Equal("Friendly", c.GuardReaction));
        Assert.All(template.Variants[0].Connections!.Where(c => c.ConnectionType == "Portal"), c => { Assert.False(c.PortalFromEnabled); Assert.True(c.PortalToEnabled); });
    }

    [Fact]
    public void TournamentCalendarPreservesSelectedDatesAndUsesActualSaveArmySetting()
    {
        var settings = Settings();
        settings.GameEndConditions.VictoryCondition = "win_condition_6";
        settings.TournamentRules.SaveArmy = false;
        settings.ProfileOptions.TournamentBattleDays = [2, 3, 9];
        settings.ProfileOptions.TournamentAnnouncementLeadDays = 1;
        var template = TemplateGenerator.Generate(settings);
        var win = template.GameRules!.WinConditions!;
        Assert.False(win.TournamentSaveArmy);
        Assert.Equal(new[] { 2, 3, 9 }, BattleCalendar.Decode(win.TournamentAnnounceDays!, win.TournamentDays!).BattleDays);
        AssertNoErrors(template);
        Assert.Throws<ArgumentException>(() => BattleCalendar.Encode([1, 7]));
        Assert.Throws<ArgumentException>(() => BattleCalendar.Encode([7, 7]));
        var legacy = BattleCalendar.Encode([8, 15, 22]);
        Assert.Equal(new[] { 1, 9, 16 }, legacy.Announcements);
        Assert.Equal(new[] { 7, 6, 6 }, legacy.Durations);
    }

    [Fact]
    public void NativeFinalBattleConvertsActualDayWithoutOffByOneAndDoesNotReplaceLegacySchedule()
    {
        var settings = Settings();
        settings.GameEndConditions.VictoryCondition = "win_condition_4";
        settings.ProfileOptions.UseNativeFinalBattleSchedule = true;
        settings.ProfileOptions.FinalBattleDay = 17;
        var win = TemplateGenerator.Generate(settings).GameRules!.WinConditions!;
        Assert.Equal(17, win.GladiatorArenaDaysDelayStart); Assert.Equal(1, win.GladiatorArenaCountDay);
        Assert.True(win.GladiatorArenaRegistrationStartWork); Assert.True(win.GladiatorArenaRegistrationStartFight);
        settings.ProfileOptions.UseNativeFinalBattleSchedule = false;
        win = TemplateGenerator.Generate(settings).GameRules!.WinConditions!;
        Assert.Equal(settings.GladiatorArenaRules.DaysDelayStart, win.GladiatorArenaDaysDelayStart);
        Assert.Equal(settings.GladiatorArenaRules.CountDay, win.GladiatorArenaCountDay);
        settings.PlayerCount = 3; settings.ProfileOptions.UseNativeFinalBattleSchedule = true;
        Assert.Throws<ArgumentException>(() => TemplateGenerator.Generate(settings));
    }

    [Fact]
    public void MultiTownHeroBaselinePreservesRequestedArithmeticAndLegacyLimitsRemainUnchanged()
    {
        var settings = Settings();
        settings.PlayerStartsWithCastles = true; settings.ZoneCfg.PlayerZoneCastles = 3;
        settings.HeroSettings = new() { HeroCountMin = 6, HeroCountMax = 12, HeroCountIncrement = 1 };
        var rules = TemplateGenerator.Generate(settings).GameRules!;
        Assert.Equal(3, rules.HeroCountMin); Assert.Equal(12, rules.HeroCountMax);
        settings.ProfileOptions = new();
        rules = TemplateGenerator.Generate(settings).GameRules!;
        Assert.Equal(5, rules.HeroCountMin); Assert.Equal(12, rules.HeroCountMax);
    }

    [Fact]
    public void SmallerTeamSupportIsExplicitAndTemplateCardContainsActualTeamsRulesAndProvenance()
    {
        var settings = Settings(3);
        settings.ProfileOptions.Alliances = new() { Enabled = true, Preset = "1v2", SmallerTeamEconomyMultiplier = 2 };
        settings.ProfileOptions.AuthorDescription = "Authored scenario";
        var template = TemplateGenerator.Generate(settings);
        var report = TemplateBalanceReport.Analyze(template);
        Assert.Equal(report.Teams[0].StartingWealth, report.Teams[1].StartingWealth);
        Assert.Contains("1v2", template.Description);
        Assert.Contains(settings.Seed!.Value.ToString("X8"), template.Description);
        Assert.Contains("Authored scenario", template.Description);
        Assert.Contains(template.DescBlocks!.Blocks!, b => b.Type == "tags" && b.Parameters![0].Contains("map_tags_16"));
    }

    [Fact]
    public void LegacyDefaultOmitsAllNewOptionsAndCurrentGenerationDoesNotMutateSavedSettings()
    {
        var settings = Settings(); settings.ProfileOptions = new();
        var legacy = TemplateGenerator.Generate(settings);
        Assert.Null(legacy.SpawnInfos); Assert.Null(legacy.DescBlocks); Assert.Null(legacy.Variants![0].River);
        Assert.Null(legacy.GameRules!.DisableFactionLaws); Assert.Null(Start(legacy).RandomHireInitialUnitIncrement);
        settings.ProfileOptions = GenerationProfileOptions.CurrentDefaults();
        string before = JsonSerializer.Serialize(settings, JsonExport.Options);
        TemplateGenerator.Generate(settings);
        Assert.Equal(before, JsonSerializer.Serialize(settings, JsonExport.Options));
    }

    [Fact]
    public void ExistingTopologyDoesNotSilentlyViolateAnExplicitEnemyBufferDepth()
    {
        var settings = Settings(4);
        settings.NoDirectPlayerConnections = true;
        settings.MinNeutralZonesBetweenPlayers = 3;
        settings.ProfileOptions.Alliances = new() { Enabled = true, Preset = "2v2", Layout = AllianceLayout.ExistingTopology };
        var error = Assert.Throws<ArgumentException>(() => TemplateGenerator.Generate(settings));
        Assert.Contains("guarantee", error.Message);
        settings.ProfileOptions.Alliances.Layout = AllianceLayout.TeamRegions;
        var generated = TemplateGenerator.Generate(settings);
        Assert.All(TemplateBalanceReport.Analyze(generated).Players, p => Assert.True(p.NearestOpponentHops >= 4));
        AssertNoErrors(generated);
    }

    [Fact]
    public void ZeroBorderStrengthIsPreservedWhenRebuildingTeamFronts()
    {
        var settings = Settings(4);
        settings.ZoneCfg.BorderGuardStrengthPercent = 0;
        settings.ProfileOptions.Alliances = new() { Enabled = true, Preset = "2v2" };
        Assert.All(TemplateGenerator.Generate(settings).Variants![0].Connections!, c => Assert.Equal(0, c.GuardValue));
    }

    [Fact]
    public void AreaOnlyBalanceComparesCoefficientsAndDoesNotClaimKnownEconomicTotals()
    {
        var template = new RmgTemplate
        {
            Name = "Density", SizeX = 128, SizeZ = 128,
            Variants = [new()
            {
                Zones = [new() { Name = "A", GuardedContentValuePerArea = 100, MainObjects = [new() { Type = "Spawn", Spawn = "Player1" }] },
                         new() { Name = "B", GuardedContentValuePerArea = 500, MainObjects = [new() { Type = "Spawn", Spawn = "Player2" }] }],
                Connections = [new() { From = "A", To = "B", ConnectionType = "Direct" }],
            }],
        };
        var report = TemplateBalanceReport.Analyze(template);
        Assert.False(report.EconomyComparisonAvailable);
        Assert.True(report.Score < 100); Assert.True(report.TeamScore < 100);
        Assert.Equal(new long[] { 100, 500 }, report.Players.Select(p => p.StartingAreaCoefficient));
        Assert.All(report.Players, p => Assert.Equal(0, p.StartWealth));
        Assert.DoesNotContain(report.Findings, f => f.Key == "S.Bal.Find.WellBalanced");
        Assert.Contains(report.Findings, f => f.Key == "S.Bal.Find.AreaBudgets");
    }

    [Theory]
    [InlineData(AllianceLayout.TeamRegions)]
    [InlineData(AllianceLayout.TeamLanes)]
    [InlineData(AllianceLayout.TeamArena)]
    public void TeamSymmetryMirrorsMixedRolesAndKeepsOddTownTotalInCommonTerritory(AllianceLayout layout)
    {
        var settings = Settings(4);
        settings.ProfileOptions.Symmetry = MapSymmetry.Teams;
        settings.ProfileOptions.Alliances = new() { Enabled = true, Preset = "2v2", Layout = layout };
        settings.ZoneCfg.Advanced = new() { Enabled = true, NeutralLowNoCastleCount = 1, NeutralLowCastleCount = 1, NeutralHighCastleCount = 3, NeutralMediumNoCastleCount = 1 };
        settings.GameEndConditions.VictoryCondition = "win_condition_5";
        settings.ProfileOptions.NeutralTownCount = 5; settings.ProfileOptions.MaximumTownsPerZone = 1;
        var template = TemplateGenerator.Generate(settings);
        var zones = template.Variants![0].Zones!;
        var branches = zones.Where(z => z.GeneratorBranchDepth.HasValue).GroupBy(z => z.GeneratorBranchDepth).ToArray();
        Assert.NotEmpty(branches);
        foreach (var branch in branches)
        {
            var pair = branch.ToArray(); Assert.Equal(2, pair.Length);
            Assert.Equal(pair[0].Layout, pair[1].Layout); Assert.Equal(pair[0].Size, pair[1].Size);
            Assert.Equal(pair[0].GuardedContentValue, pair[1].GuardedContentValue);
            Assert.Equal(pair[0].ResourcesValue, pair[1].ResourcesValue); Assert.Equal(pair[0].GuardMultiplier, pair[1].GuardMultiplier);
            Assert.Equal(pair[0].GuardedContentPool, pair[1].GuardedContentPool);
            Assert.Equal(pair[0].MainObjects!.Count(o => o.Type == "City"), pair[1].MainObjects!.Count(o => o.Type == "City"));
        }
        Assert.Equal(5, zones.Sum(z => z.MainObjects!.Count(o => o.Type == "City")));
        var objective = Assert.Single(zones, z => z.MainObjects!.Any(o => o.HoldCityWinCon == true));
        Assert.Equal("contested", objective.GeneratorAllianceKey);
        AssertNoErrors(template);
    }

    [Fact]
    public void ExplicitLegacyHireFlagsMaterializeTheSupportedSevenTierArrays()
    {
        var settings = Settings(); settings.ProfileOptions = new() { EnableWeeklyUnitIncrement = false, InitialUnitIncrement = 3 };
        Assert.All(TemplateGenerator.Generate(settings).Variants![0].Zones!, zone =>
        {
            Assert.Equal(7, zone.RandomHireEnableWeeklyUnitIncrement!.Count);
            Assert.All(zone.RandomHireEnableWeeklyUnitIncrement, value => Assert.False(value));
            Assert.All(zone.RandomHireInitialUnitIncrement!, value => Assert.Equal(3, value));
        });
    }

    [Fact]
    public void SingleHeroCardUsesEffectiveLimitsAndImpossibleMultiTownMinimumIsRejected()
    {
        var settings = Settings(); settings.SingleHeroMode = true;
        var template = TemplateGenerator.Generate(settings);
        Assert.Contains("1–1", template.Description);
        settings.SingleHeroMode = false; settings.PlayerStartsWithCastles = true;
        settings.ZoneCfg.PlayerZoneCastles = 3; settings.HeroSettings.HeroCountMin = 2;
        Assert.Contains("starting towns", Assert.Throws<ArgumentException>(() => TemplateGenerator.Generate(settings)).Message);
    }

    [Fact]
    public void RandomTeamAssignmentsAreSeededWithoutChangingTeamCompositionOrManualSlots()
    {
        var options = new AllianceSettings { Enabled = true, Preset = "2v2v2", RandomizePlayerAssignment = true };
        var first = AlliancePlan.Resolve(6, options, 177).ToSpawnInfos();
        Assert.Equal(first.Select(s => s.AllianceIndex), AlliancePlan.Resolve(6, options, 177).ToSpawnInfos().Select(s => s.AllianceIndex));
        Assert.Equal(new[] { 2, 2, 2 }, first.GroupBy(s => s.AllianceIndex).Select(g => g.Count()));
        Assert.True(Enumerable.Range(0, 12).Select(seed => string.Join(",", AlliancePlan.Resolve(6, options, seed).ToSpawnInfos().Select(s => s.AllianceIndex))).Distinct().Count() > 1);
        Assert.Empty(options.Slots);
        options.Slots = Enumerable.Range(1, 6).Select(p => new AllianceSlotSettings { Player = p, Alliance = p <= 3 ? 1 : 2 }).ToList();
        Assert.Throws<ArgumentException>(() => AlliancePlan.Resolve(6, options, 177));
        options.RandomizePlayerAssignment = false;
        Assert.Equal(new int?[] { 1, 1, 1, 2, 2, 2 }, AlliancePlan.Resolve(6, options, 177).ToSpawnInfos().Select(s => s.AllianceIndex));
    }

    [Fact]
    public void ExplicitNullProfilePartsProduceActionableErrorsInsteadOfNullReferenceFailures()
    {
        var settings = Settings(); settings.ProfileOptions.Alliances = null!;
        Assert.Throws<ArgumentException>(() => TemplateGenerator.Generate(settings));
        settings = Settings(); settings.ZoneCfg.Advanced = null!;
        Assert.Throws<ArgumentException>(() => TemplateGenerator.Generate(settings));
        settings = Settings(); settings.ProfileOptions.TournamentBattleDays = null!;
        Assert.Throws<ArgumentException>(() => TemplateGenerator.Generate(settings));
    }

    [Fact]
    public void MixedAreaAndAbsoluteBudgetsKeepBothMetricsAndDoNotClaimFullEconomyKnown()
    {
        var settings = Settings();
        var report = TemplateBalanceReport.Analyze(TemplateGenerator.Generate(settings));
        Assert.False(report.EconomyComparisonAvailable);
        Assert.All(report.Players, p => { Assert.True(p.StartWealth > 0); Assert.True(p.StartingAreaCoefficient > 0); });
        Assert.DoesNotContain(report.Findings, f => f.Key == "S.Bal.Find.WellBalanced");
    }

    [Fact]
    public void CardReportsCombinedRulesAndManualDensityOverridesFromActualExport()
    {
        var settings = Settings();
        settings.GameEndConditions.VictoryCondition = "win_condition_5";
        settings.GameEndConditions.LostStartHero = true;
        settings.ZoneCfg.ResourceDensityPercent = 175;
        settings.ZoneCfg.StructureDensityPercent = 65;
        var template = TemplateGenerator.Generate(settings);
        Assert.Contains(LocalizationManager.T("S.Victory.0"), template.Description);
        Assert.Contains(LocalizationManager.T("S.Victory.1"), template.Description);
        Assert.Contains(LocalizationManager.T("S.Victory.5"), template.Description);
        Assert.Contains(template.DescBlocks!.Blocks!, b => b.Parameters?.Any(p => p.Contains("175") && p.Contains("65")) == true);
    }

    [Theory]
    [InlineData(MapRichness.VeryPoor, "template_pool_random_maps_t1", "template_pool_random_maps_unguarded_t1", "content_pool_general_resources_start_zone_very_poor")]
    [InlineData(MapRichness.Poor, "template_pool_random_maps_t2", "template_pool_random_maps_unguarded_t2", "content_pool_general_resources_start_zone_poor")]
    [InlineData(MapRichness.Normal, "template_pool_random_maps_t3", "template_pool_random_maps_unguarded_t3", "content_pool_general_resources_start_zone_medium")]
    [InlineData(MapRichness.Rich, "template_pool_random_maps_t4", "template_pool_random_maps_unguarded_t4", "content_pool_general_resources_start_zone_rich")]
    [InlineData(MapRichness.VeryRich, "template_pool_random_maps_t5", "template_pool_random_maps_unguarded_t5", "content_pool_general_resources_start_zone_rich")]
    public void CurrentProfileUsesVerifiedRichnessPoolTablesWithoutRelabelingDanger(MapRichness richness, string guarded, string unguarded, string resources)
    {
        var settings = Settings(); settings.ProfileOptions.Richness = richness;
        settings.ZoneCfg.Advanced = new() { Enabled = true, NeutralLowNoCastleCount = 1, NeutralHighNoCastleCount = 1 };
        var template = TemplateGenerator.Generate(settings);
        Assert.All(template.Variants![0].Zones!, zone =>
        {
            Assert.Equal(new[] { guarded }, zone.GuardedContentPool);
            Assert.Equal(new[] { unguarded }, zone.UnguardedContentPool);
            Assert.Equal(new[] { resources }, zone.ResourcesContentPool);
        });
        var referenceSettings = Settings(); referenceSettings.ZoneCfg.Advanced = settings.ZoneCfg.Advanced;
        var reference = TemplateGenerator.Generate(referenceSettings);
        Assert.Equal(reference.Variants![0].Zones!.Select(z => string.Join(",", z.GuardReactionDistribution!)),
            template.Variants[0].Zones!.Select(z => string.Join(",", z.GuardReactionDistribution!)));
        settings.ProfileOptions = new();
        Assert.DoesNotContain(TemplateGenerator.Generate(settings).Variants![0].Zones!,
            zone => zone.GuardedContentPool!.Any(pool => pool.StartsWith("template_pool_random_maps_", StringComparison.Ordinal)));
        AssertNoErrors(template);
    }

    [Fact]
    public void CurrentAreaProfileAppliesUserDensityAndStrengthOnlyOnce()
    {
        var settings = Settings(); settings.ProfileOptions.UseAreaBudgets = true;
        settings.ZoneCfg.StructureDensityPercent = 150; settings.ZoneCfg.ResourceDensityPercent = 200;
        settings.ZoneCfg.NeutralStackStrengthPercent = 200; settings.ProfileOptions.Challenge = MapChallenge.Hard;
        Zone start = Start(TemplateGenerator.Generate(settings));
        Assert.Equal(3300, start.GuardedContentValuePerArea);
        Assert.Equal(735, start.UnguardedContentValuePerArea);
        Assert.Equal(800, start.ResourcesValuePerArea);
        Assert.Equal(Math.Round(2 * 1.2 / 1.1, 4), start.GuardMultiplier);
        Assert.Equal(.1, start.GuardWeeklyIncrement);
    }

    [Theory]
    [InlineData(AllianceLayout.TeamRegions)]
    [InlineData(AllianceLayout.TeamLanes)]
    [InlineData(AllianceLayout.TeamArena)]
    public void MirroredTeamPortalsPreserveRoleDistancesAndTheRequestedBuffersAcrossSeeds(AllianceLayout layout)
    {
        for (int seed = 0; seed < 12; seed++)
        {
            var settings = Settings(4, seed); settings.RandomPortals = true;
            settings.NoDirectPlayerConnections = true; settings.MinNeutralZonesBetweenPlayers = 3;
            settings.ProfileOptions.Symmetry = MapSymmetry.Teams;
            settings.ProfileOptions.Alliances = new() { Enabled = true, Preset = "2v2", Layout = layout };
            var template = TemplateGenerator.Generate(settings);
            var variant = template.Variants![0];
            var connections = Assert.IsType<List<Connection>>(variant.Connections);
            var portals = connections.Where(c => c.ConnectionType == "Portal").ToArray();
            Assert.NotEmpty(portals); Assert.True(portals.Length <= settings.MaxPortalConnections);
            Assert.All(portals, portal => Assert.Contains(connections, direct => direct.ConnectionType == "Direct"
                && (direct.From == portal.From && direct.To == portal.To || direct.From == portal.To && direct.To == portal.From)));
            var portalRoles = portals.GroupBy(p => variant.Zones!.Single(z => z.Name == p.From).GeneratorBranchDepth);
            Assert.All(portalRoles, group => Assert.Equal(2, group.Count()));
            var report = TemplateBalanceReport.Analyze(template);
            Assert.All(report.Players, p => Assert.True(p.NearestOpponentHops >= 4));
            Assert.Single(report.Players.Select(p => p.NearestOpponentHops).Distinct());
            var exported = JsonSerializer.Serialize(template, JsonExport.Options);
            Assert.Equal(exported, JsonSerializer.Serialize(TemplateGenerator.Generate(settings), JsonExport.Options));
            AssertNoErrors(template);
        }
        var invalid = Settings(4); invalid.RandomPortals = true; invalid.MaxPortalConnections = 1;
        invalid.ProfileOptions.Symmetry = MapSymmetry.Teams;
        invalid.ProfileOptions.Alliances = new() { Enabled = true, Preset = "2v2", Layout = layout };
        Assert.Contains("portal limit", Assert.Throws<ArgumentException>(() => TemplateGenerator.Generate(invalid)).Message);
    }

    [Theory]
    [InlineData(MapTempo.Balanced)]
    [InlineData(MapTempo.Exploration)]
    public void PreparedTempoCopiesBonusInputsAndUsesProfileSeedWithoutMutatingSource(MapTempo tempo)
    {
        var settings = Settings(); settings.Seed = null;
        settings.Topology = MapTopology.Lanes;
        settings.ProfileOptions.TemplateSeed = 361; settings.ProfileOptions.Tempo = tempo;
        settings.RandomPortals = true;
        settings.Bonuses = [new() { PresetType = BonusPresetType.StartingGold, Param = "1250", Param2 = "0" }];
        string before = JsonSerializer.Serialize(settings, SettingsSnapshotMapper.SnapshotOptions);
        var prepared = GenerationProfileApplier.Prepare(settings);
        Assert.Equal(MapTopology.Default, prepared.Topology);
        Assert.Equal(MapTopology.Lanes, settings.Topology);
        Assert.NotSame(settings, prepared); Assert.Equal(361, prepared.Seed);
        Assert.NotSame(settings.Bonuses[0], prepared.Bonuses[0]); Assert.Equal("1250", prepared.Bonuses[0].Param);
        Assert.Null(settings.Seed);
        var generated = TemplateGenerator.Generate(settings);
        Assert.Contains(generated.GameRules!.Bonuses!, bonus => bonus.Sid == "add_bonus_res" && bonus.Parameters?.Contains("1250") == true);
        Assert.Equal(JsonSerializer.Serialize(generated, JsonExport.Options), JsonSerializer.Serialize(TemplateGenerator.Generate(settings), JsonExport.Options));
        Assert.Equal(before, JsonSerializer.Serialize(settings, SettingsSnapshotMapper.SnapshotOptions));
        settings.Seed = 7;
        Assert.Equal(7, GenerationProfileApplier.Prepare(settings).Seed);
        Assert.Contains("00000007", TemplateGenerator.Generate(settings).Description);
    }

    [Fact]
    public void GuardedContentMetricsIncludeAreaCoefficientsAndKeepUnreachableTargetsUnknown()
    {
        var template = new RmgTemplate { Variants = [new()
        {
            Zones = [new() { Name = "A", MainObjects = [new() { Type = "Spawn", Spawn = "Player1" }] },
                     new() { Name = "B", MainObjects = [new() { Type = "Spawn", Spawn = "Player2" }] },
                     new() { Name = "T", GuardedContentValuePerArea = 1 }, new() { Name = "M" },
                     new() { Name = "C", MainObjects = [new() { Type = "City" }] }],
            Connections = [new() { From = "A", To = "T", ConnectionType = "Direct", GuardValue = 90 },
                           new() { From = "A", To = "M", ConnectionType = "Direct", GuardValue = 2 },
                           new() { From = "M", To = "T", ConnectionType = "Direct", GuardValue = 3 },
                           new() { From = "B", To = "C", ConnectionType = "Direct", GuardValue = 4 }],
        }] };
        var report = TemplateBalanceReport.Analyze(template);
        var first = Assert.Single(report.Players, player => player.SpawnId == "Player1");
        var second = Assert.Single(report.Players, player => player.SpawnId == "Player2");
        Assert.Equal(1, first.TreasureHops); Assert.Equal(5L, first.ClosestTreasureGuardBudget);
        Assert.Null(second.TreasureHops); Assert.Null(second.ClosestTreasureGuardBudget);
        Assert.Null(first.CastleHops); Assert.Null(first.ClosestCastleGuardBudget);
        Assert.Equal(1, second.CastleHops); Assert.Equal(4L, second.ClosestCastleGuardBudget);
    }

    [Fact]
    public void BalanceExcludesScriptedAiFromLobbyPlayersAndCopiesNullablePortalFlags()
    {
        var firstPortal = new Connection { Name = "P1", From = "A", To = "B", ConnectionType = "Portal", PortalFromEnabled = null, PortalToEnabled = true, GuardValue = 20 };
        var secondPortal = new Connection { Name = "P2", From = "B", To = "A", ConnectionType = "Portal", PortalFromEnabled = false, PortalToEnabled = null, GuardValue = 30 };
        var template = new RmgTemplate { Variants = [new()
        {
            Zones = [new() { Name = "A", GuardedContentValue = 100, MainObjects = [new() { Type = "Spawn", Spawn = "Player1" }] },
                     new() { Name = "B", GuardedContentValue = 100, MainObjects = [new() { Type = "Spawn", Spawn = "Player2" }] },
                     new() { Name = "S", MainObjects = [new() { Type = "Spawn", Spawn = "AIPlayer1" }] }],
            Connections = [firstPortal, secondPortal, new() { From = "A", To = "S", ConnectionType = "Direct" }, new() { From = "B", To = "S", ConnectionType = "Direct" }],
        }] };
        var report = TemplateBalanceReport.Analyze(template);
        Assert.Equal(new[] { "Player1", "Player2" }, report.Players.Select(player => player.SpawnId));
        Assert.Equal(2, report.Teams.Count); Assert.Equal(100, report.Score);
        Assert.Equal(new PortalBalance("P1", "A", "B", null, true, 20), report.Portals[0]);
        Assert.Equal(new PortalBalance("P2", "B", "A", false, null, 30), report.Portals[1]);
        firstPortal.PortalToEnabled = false;
        Assert.True(report.Portals[0].ToEnabled); Assert.Null(report.Portals[0].FromEnabled);
    }

    [Fact]
    public void StartingCityCountsExcludeScriptedSpawnsAndOtherPlayersWithinTheSameZone()
    {
        var template = new RmgTemplate { Variants = [new()
        {
            Zones = [new() { Name = "A", MainObjects = [new() { Type = "Spawn", Spawn = "Player1" }, new() { Type = "Spawn", Spawn = "AIPlayer1" },
                         new() { Type = "City", Owner = "Player1" }, new() { Type = "City", Owner = "AIPlayer1" }] },
                     new() { Name = "B", MainObjects = [new() { Type = "Spawn", Spawn = "Player2" }] }],
            Connections = [new() { From = "A", To = "B", ConnectionType = "Direct" }],
        }] };
        var report = TemplateBalanceReport.Analyze(template);
        Assert.Equal(2, Assert.Single(report.Players, player => player.SpawnId == "Player1").StartingCities);
        Assert.Equal(1, Assert.Single(report.Players, player => player.SpawnId == "Player2").StartingCities);
        Assert.Equal(2, Assert.Single(report.Teams, team => team.AllianceKey == "Player1").StartingCities);
        var zone = Assert.IsType<List<Zone>>(Assert.Single(template.Variants!).Zones)[0];
        zone.MainObjects!.Add(new() { Type = "Spawn", Spawn = "Player2" });
        Assert.Equal(2, Assert.Single(TemplateBalanceReport.Analyze(template).Players, player => player.SpawnId == "Player1").StartingCities);
    }

    private static GeneratorSettings Settings(int players = 2, int seed = 17) => new()
    {
        TemplateName = "Patch profile", PlayerCount = players, Seed = seed, MapSize = 256, ProfileOptions = GenerationProfileOptions.CurrentDefaults(),
        Topology = MapTopology.Default, ZoneCfg = new() { NeutralZoneCount = 4, NeutralZoneCastles = 1, ResourceDensityPercent = 100, StructureDensityPercent = 100 },
    };
    private static Zone Start(RmgTemplate template) => template.Variants![0].Zones!.First(z => z.MainObjects!.Any(o => o.Spawn == "Player1"));
    private static void AssertNoErrors(RmgTemplate template)
    {
        var errors = TemplateCompatibilityValidator.Validate(template).Where(i => i.Severity == TemplateIssueSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join("\n", errors.Select(e => e.Path + " " + e.Message)));
    }
}
