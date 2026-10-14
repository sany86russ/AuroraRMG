using System.Globalization;
using System.Text.Json;
using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.Generation;

/// <summary>
/// Additive patch profile. Tables provide ratios for Aurora's own budgets, not a reproduction of native
/// geometry or lobby difficulty. No random draws are inserted into the legacy generator's seed stream.
/// </summary>
public static class GenerationProfileApplier
{
    // Default.mrmg.json coefficients are emitted in area mode and used as dimensionless richness
    // ratios for Aurora's absolute budgets. Own density/progression adjustments remain explicit.
    private static readonly int[] StartRichness = [1700, 1900, 2200, 2600, 3500];
    private static readonly int[] BaseRichness = [1900, 2200, 2600, 3100, 4000];
    private static readonly int[] TreasureRichness = [3000, 3600, 4600, 5400, 7000];
    private static readonly int[] MidTreasureRichness = [2000, 2400, 3100, 3900, 5000];
    private static readonly int[] StartUnguarded = [360, 420, 490, 570, 750];
    private static readonly int[] TreasureUnguarded = [680, 740, 820, 920, 1100];
    private static readonly int[] SuperTreasureUnguarded = [810, 920, 1080, 1290, 1600];
    private static readonly int[] StandardResources = [300, 350, 400, 450, 500];
    private static readonly int[] SuperTreasureResources = [350, 450, 550, 650, 750];
    private static readonly double[] GenerationGuardFactors = [1, 1.1, 1.2, 1.3, 1.4, 1.5];
    private static readonly double[] GuardGrowth = [.05, .075, .1, .125, .15, .2];
    // Default.mrmg.json supplies these same richness-selected exact SIDs in all 14 zone classes.
    // They are profile data for 0.81.04; generation does not require an installed game directory.
    private static readonly string[] GuardedPools = ["template_pool_random_maps_t1", "template_pool_random_maps_t2", "template_pool_random_maps_t3", "template_pool_random_maps_t4", "template_pool_random_maps_t5"];
    private static readonly string[] UnguardedPools = ["template_pool_random_maps_unguarded_t1", "template_pool_random_maps_unguarded_t2", "template_pool_random_maps_unguarded_t3", "template_pool_random_maps_unguarded_t4", "template_pool_random_maps_unguarded_t5"];
    private static readonly string[] ResourcePools = ["content_pool_general_resources_start_zone_very_poor", "content_pool_general_resources_start_zone_poor", "content_pool_general_resources_start_zone_medium", "content_pool_general_resources_start_zone_rich", "content_pool_general_resources_start_zone_rich"];

    public static GeneratorSettings Prepare(GeneratorSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var profile = source.ProfileOptions ?? throw new ArgumentException("Generation profile cannot be null.");
        if (profile.Alliances is null || profile.Alliances.Slots is null || profile.TournamentBattleDays is null)
            throw new ArgumentException("Generation profile requires alliance settings, a slot list and a tournament date list. Explicit null values are invalid.");
        if (source.HeroSettings is null || source.ZoneCfg is null || source.ZoneCfg.Advanced is null
            || source.GameEndConditions is null || source.TournamentRules is null || source.GladiatorArenaRules is null)
            throw new ArgumentException("Generator settings are missing hero, zone or victory-rule objects. Explicit null values are invalid.");
        if (profile.ProfileId != GenerationProfileOptions.LegacyProfile && !profile.IsCurrentProfile)
            throw new NotSupportedException($"Unknown generation profile '{profile.ProfileId}'. Select a supported profile before exporting.");
        if (!Enum.IsDefined(profile.Richness) || !Enum.IsDefined(profile.Challenge) || !Enum.IsDefined(profile.Symmetry) || !Enum.IsDefined(profile.Tempo))
            throw new ArgumentException("Generation profile contains an unknown option.");
        if (profile.NeutralTownCount is < 0 || profile.MaximumTownsPerZone is < 0 || profile.InitialUnitIncrement is < 0)
            throw new ArgumentException("Town and initial-unit counts must be nonnegative.");
        if (profile.EnableRivers && (!double.IsFinite(profile.RiverTributarySpacing) || profile.RiverTributarySpacing <= 0))
            throw new ArgumentException("Enabled rivers need positive finite tributary spacing.");
        if (profile.IsCurrentProfile && (source.HeroSettings.HeroCountMin < 1 || source.HeroSettings.HeroCountMax < source.HeroSettings.HeroCountMin || source.HeroSettings.HeroCountIncrement < 0))
            throw new ArgumentException("Hero limits need a positive starting minimum, maximum at least the minimum, and nonnegative per-town increment.");
        int ownedStartingTowns = source.PlayerStartsWithCastles ? Math.Max(1, source.ZoneCfg.PlayerZoneCastles) : 1;
        if (profile.IsCurrentProfile && !source.SingleHeroMode && (long)ownedStartingTowns * source.HeroSettings.HeroCountIncrement > source.HeroSettings.HeroCountMin)
            throw new ArgumentException("The owned starting towns already grant more hero capacity than the requested starting minimum. Increase that minimum or reduce the per-town increment/starting owned towns.");
        bool copied = false;
        if (!source.Seed.HasValue && profile.TemplateSeed.HasValue)
        {
            source = SettingsSnapshotMapper.Clone(source);
            source.Seed = profile.TemplateSeed;
            profile = source.ProfileOptions;
            copied = true;
        }
        var plan = AlliancePlan.Resolve(source.PlayerCount, profile.Alliances, source.Seed);
        if (profile.Symmetry == MapSymmetry.Teams && !plan.Enabled)
            throw new ArgumentException("Enable alliance assignments (FFA is also supported) before selecting mirrored team territories, or choose equal starts.");
        bool tournament = source.TournamentRules.Enabled || source.GameEndConditions.VictoryCondition == "win_condition_6";
        bool finalBattle = source.GladiatorArenaRules.Enabled || source.GameEndConditions.VictoryCondition == "win_condition_4";
        if (profile.IsCurrentProfile && finalBattle && (source.PlayerCount != 2 || plan.Enabled && (plan.Groups.Count != 2 || plan.Groups.Any(g => g.Players.Count != 1))))
            throw new ArgumentException("The verified final-battle profile requires two opposing individual players. Choose another victory condition for teams or additional players.");
        if (tournament && (source.PlayerCount != 2 || plan.Enabled && (plan.Groups.Count != 2 || plan.Groups.Any(g => g.Players.Count != 1))))
            throw new ArgumentException("The supported tournament export requires two opposing individual players. Choose another victory condition for teams.");
        if (profile.TournamentBattleDays.Count > 0 && !tournament)
            throw new ArgumentException("Tournament dates are configured but tournament victory is disabled.");
        if (profile.UseNativeFinalBattleSchedule && source.GameEndConditions.VictoryCondition != "win_condition_4" && !source.GladiatorArenaRules.Enabled)
            throw new ArgumentException("The native final-battle calendar needs final-battle victory enabled.");
        if (!profile.IsCurrentProfile || profile.Tempo is MapTempo.Legacy or MapTempo.EarlyContact || plan.Enabled || tournament) return source;
        // A selected exploration/balanced tempo uses actual neutral buffers. Clone so a generation
        // does not silently change saved UI settings. This is Aurora graph pacing, not AI behavior.
        var prepared = copied ? source : SettingsSnapshotMapper.Clone(source);
        prepared.Topology = MapTopology.Default;
        prepared.NoDirectPlayerConnections = true;
        prepared.MinNeutralZonesBetweenPlayers = Math.Max(source.MinNeutralZonesBetweenPlayers, profile.Tempo == MapTempo.Exploration ? 2 : 1);
        var advanced = prepared.ZoneCfg.Advanced;
        int existing = advanced.Enabled ? advanced.NeutralLowNoCastleCount + advanced.NeutralLowCastleCount
            + advanced.NeutralMediumNoCastleCount + advanced.NeutralMediumCastleCount
            + advanced.NeutralHighNoCastleCount + advanced.NeutralHighCastleCount : prepared.ZoneCfg.NeutralZoneCount;
        int desired = Math.Max(existing, prepared.PlayerCount * prepared.MinNeutralZonesBetweenPlayers);
        if (desired + prepared.PlayerCount > TemplateGenerator.ZoneLetters.Length)
            throw new ArgumentException("The selected tempo and buffer depth exceed 48 zones. Reduce the neutral-zone count or depth.");
        if (advanced.Enabled) advanced.NeutralLowNoCastleCount += desired - existing;
        else prepared.ZoneCfg.NeutralZoneCount = desired;
        return prepared;
    }

    public static void Apply(RmgTemplate template, GeneratorSettings settings)
    {
        var profile = settings.ProfileOptions;
        var alliance = AlliancePlan.Resolve(settings.PlayerCount, profile.Alliances, settings.Seed);
        if (alliance.Enabled) { template.SpawnInfos = alliance.ToSpawnInfos(); template.MaxAlliancesCount = alliance.MaximumAlliances; }
        var rules = template.GameRules ??= new();
        var win = rules.WinConditions ??= new();
        if (profile.IsCurrentProfile || !profile.ClassicVictoryEnabled) win.Classic = profile.ClassicVictoryEnabled;
        if (settings.GameEndConditions.VictoryCondition == "win_condition_8") win.LostStartHero = true;
        if (profile.ResetCityHoldTimer.HasValue) win.IsResetColdownCityHold = profile.ResetCityHoldTimer;
        if (profile.IsCurrentProfile || profile.DisableFactionLaws) rules.DisableFactionLaws = profile.DisableFactionLaws;
        if (profile.IsCurrentProfile || profile.DisableMagicGuild) rules.DisableMagicGuild = profile.DisableMagicGuild;
        if (profile.IsCurrentProfile || profile.DisableMagicCustomLearning) rules.DisableMagicCustomLearning = profile.DisableMagicCustomLearning;
        if (!string.IsNullOrEmpty(profile.CustomAI)) rules.CustomAI = profile.CustomAI;
        if (profile.TournamentBattleDays.Count > 0)
        {
            var calendar = BattleCalendar.Encode(profile.TournamentBattleDays, profile.TournamentAnnouncementLeadDays ?? (profile.IsCurrentProfile ? 2 : null));
            win.TournamentAnnounceDays = calendar.Announcements.ToList(); win.TournamentDays = calendar.Durations.ToList();
        }
        else if (win.Tournament == true && profile.TournamentAnnouncementLeadDays.HasValue)
        {
            var selected = BattleCalendar.Decode(win.TournamentAnnounceDays!, win.TournamentDays!).BattleDays;
            var calendar = BattleCalendar.Encode(selected, profile.TournamentAnnouncementLeadDays);
            win.TournamentAnnounceDays = calendar.Announcements.ToList(); win.TournamentDays = calendar.Durations.ToList();
        }
        if (profile.UseNativeFinalBattleSchedule) BattleCalendar.ApplyNativeFinalBattle(win, profile.FinalBattleDay ?? settings.GladiatorArenaRules.DaysDelayStart, settings.PlayerCount);

        foreach (var variant in template.Variants ?? [])
        {
            TeamLayoutBuilder.Apply(variant, settings, alliance);
            if (win.GladiatorArena == true && settings.PlayerCount == 2
                && !((variant.Zones ?? []).SelectMany(z => z.MainObjects ?? []).Any(o => o.Type == "GladiatorArena") || (variant.Connections ?? []).Any(c => c.ConnectionType == "GladiatorArena")))
            {
                var starts = (variant.Zones ?? []).Where(z => z.MainObjects?.Any(o => o.Type == "Spawn" && Array.IndexOf(KnownValues.SpawnPlayers, o.Spawn) >= 0) == true).ToList();
                variant.Connections ??= [];
                variant.Connections.Add(new() { Name = "Final-Battle-Arena", From = starts[0].Name, To = starts[1].Name, ConnectionType = "GladiatorArena" });
            }
            if (profile.EnableRivers) variant.River = new() { CreateRiverSystem = true, TributarySpacing = profile.RiverTributarySpacing };
            if (profile.NeutralTownCount.HasValue || profile.MaximumTownsPerZone.HasValue)
            { DistributeNeutralTowns(variant, settings); TeamLayoutBuilder.RebuildConnectionRoads(variant, settings.GenerateRoads); }
            var roles = profile.IsCurrentProfile ? ApplyEconomyAndProgression(variant, settings) : null;
            if (profile.Symmetry is MapSymmetry.EqualStarts or MapSymmetry.Teams) EqualizeStarts(variant);
            if (alliance.Enabled && profile.Alliances.SmallerTeamEconomyMultiplier > 1) ApplySmallerTeamSupport(variant, alliance, profile.Alliances.SmallerTeamEconomyMultiplier);
            foreach (var zone in variant.Zones ?? [])
            {
                if (profile.UseAreaBudgets)
                { zone.GuardedContentValue = 0; zone.UnguardedContentValue = 0; zone.ResourcesValue = 0; }
                if (profile.IsCurrentProfile || profile.EnableWeeklyUnitIncrement.HasValue)
                    zone.RandomHireEnableWeeklyUnitIncrement ??= Enumerable.Repeat(profile.EnableWeeklyUnitIncrement ?? true, 7).ToList();
                if (profile.IsCurrentProfile || profile.InitialUnitIncrement.HasValue)
                    zone.RandomHireInitialUnitIncrement ??= Enumerable.Repeat(profile.InitialUnitIncrement ?? 1, 7).ToList();
                if (profile.IsCurrentProfile)
                {
                    zone.GuardReactionDistribution = GuardReaction.NativeDistribution(roles![zone.Name], settings.MonsterAggression);
                }
                if (profile.EnableWeeklyUnitIncrement is { } weekly && zone.RandomHireEnableWeeklyUnitIncrement is { } weeklyArray)
                    zone.RandomHireEnableWeeklyUnitIncrement = Enumerable.Repeat(weekly, weeklyArray.Count).ToList();
                if (profile.InitialUnitIncrement is { } initial && zone.RandomHireInitialUnitIncrement is { } initialArray)
                    zone.RandomHireInitialUnitIncrement = Enumerable.Repeat(initial, initialArray.Count).ToList();
                foreach (var obj in zone.MainObjects ?? [])
                {
                    if (obj.Type is "Spawn" or "City" or "AbandonedOutpost")
                    {
                        if (profile.EnableWeeklyUnitIncrement.HasValue) obj.EnableWeeklyUnitIncrement = profile.EnableWeeklyUnitIncrement;
                        if (profile.WeekOneOnlyModificatorGrowth.HasValue) obj.WeekOneOnlyModificatorGrowth = profile.WeekOneOnlyModificatorGrowth;
                        if (profile.InitialUnitIncrement.HasValue) obj.InitialUnitIncrement = profile.InitialUnitIncrement;
                    }
                    if (obj.Type == "Spawn" && (profile.IsCurrentProfile || settings.GameEndConditions.VictoryCondition == "win_condition_8")
                        && (win.LostStartCity == true || win.LostStartHero == true)) obj.IsKeyObject = true;
                }
            }
            foreach (var connection in variant.Connections ?? [])
            {
                if (connection.ConnectionType == "Portal")
                { connection.PortalFromEnabled = profile.PortalFromEnabled; connection.PortalToEnabled = profile.PortalToEnabled; }
                if (!string.IsNullOrEmpty(profile.BorderGuardReaction)) connection.GuardReaction = profile.BorderGuardReaction;
            }
        }
        if (profile.IncludeTemplateCard) TemplateCardBuilder.Refresh(template, settings, alliance);
    }

    private static Dictionary<string, GuardRole> ApplyEconomyAndProgression(Variant variant, GeneratorSettings settings)
    {
        var profile = settings.ProfileOptions;
        var distance = DistancesFromStarts(variant);
        var roles = new Dictionary<string, GuardRole>(StringComparer.Ordinal);
        double challenge = GenerationGuardFactors[(int)profile.Challenge] / GenerationGuardFactors[(int)MapChallenge.Normal];
        double tempo = profile.Tempo == MapTempo.EarlyContact ? .75 : profile.Tempo == MapTempo.Exploration ? 1.25 : 1;
        foreach (var zone in variant.Zones ?? [])
        {
            bool start = zone.MainObjects?.Any(o => o.Type == "Spawn" && Array.IndexOf(KnownValues.SpawnPlayers, o.Spawn) >= 0) == true;
            bool treasure = zone.Layout?.Contains("treasure", StringComparison.OrdinalIgnoreCase) == true;
            bool super = (zone.GuardedContentPool ?? []).Any(pool => pool.Contains("_t4", StringComparison.OrdinalIgnoreCase) || pool.Contains("_t5", StringComparison.OrdinalIgnoreCase));
            roles.Add(zone.Name, start ? GuardRole.StartZone : super ? GuardRole.NeutralDangerous : GuardRole.NeutralStandard);
            int[] table = start ? StartRichness : super ? TreasureRichness : treasure ? MidTreasureRichness : BaseRichness;
            double richness = table[(int)profile.Richness] / (double)table[(int)MapRichness.Normal];
            double progression = start ? 1 : 1 + Math.Min(3, Math.Max(0, distance.GetValueOrDefault(zone.Name, 1) - 1)) * .15;
            ScaleEconomy(zone, richness * progression);
            if (profile.UseAreaBudgets)
            {
                // Native area is the engine's eligible-free-cell count. Emit coefficients and let it
                // measure that area; never derive an area from map size or claim equivalent totals.
                int rich = (int)profile.Richness;
                zone.GuardedContentValuePerArea = Scale(table[rich], settings.ZoneCfg.StructureDensityPercent / 100.0 * progression);
                zone.UnguardedContentValuePerArea = Scale((super ? SuperTreasureUnguarded : treasure ? TreasureUnguarded : StartUnguarded)[rich], settings.ZoneCfg.StructureDensityPercent / 100.0 * progression);
                zone.ResourcesValuePerArea = Scale((super ? SuperTreasureResources : StandardResources)[rich], settings.ZoneCfg.ResourceDensityPercent / 100.0 * progression);
            }
            zone.GuardMultiplier = Math.Round((zone.GuardMultiplier ?? 1) * challenge * progression, 4);
            zone.GuardWeeklyIncrement = GuardGrowth[(int)profile.Challenge];
            foreach (var obj in zone.MainObjects ?? [])
            { obj.GuardValue = Scale(obj.GuardValue, challenge * progression); obj.GuardWeeklyIncrement = GuardGrowth[(int)profile.Challenge]; }
            // Pool tier represents richness in the new native generator. Capture the authored zone
            // role above first, so a very-rich t5 pool does not relabel every zone as dangerous.
            zone.GuardedContentPool = [GuardedPools[(int)profile.Richness]];
            zone.UnguardedContentPool = [UnguardedPools[(int)profile.Richness]];
            zone.ResourcesContentPool = [ResourcePools[(int)profile.Richness]];
        }
        foreach (var edge in variant.Connections ?? [])
        { edge.GuardValue = Scale(edge.GuardValue, challenge * tempo); if ((edge.GuardValue ?? 0) > 0) edge.GuardWeeklyIncrement = GuardGrowth[(int)profile.Challenge]; }
        return roles;
    }

    private static Dictionary<string, int> DistancesFromStarts(Variant variant)
    {
        var names = (variant.Zones ?? []).Select(z => z.Name).ToHashSet(StringComparer.Ordinal);
        var adjacency = names.ToDictionary(n => n, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var edge in variant.Connections ?? [])
            if (ConnectionRules.AllowsTravel(edge) && adjacency.ContainsKey(edge.From) && adjacency.ContainsKey(edge.To))
            { adjacency[edge.From].Add(edge.To); adjacency[edge.To].Add(edge.From); }
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        foreach (var zone in variant.Zones ?? [])
            if (zone.MainObjects?.Any(o => o.Type == "Spawn" && Array.IndexOf(KnownValues.SpawnPlayers, o.Spawn) >= 0) == true)
            { result[zone.Name] = 0; queue.Enqueue(zone.Name); }
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (var neighbor in adjacency[current]) if (result.TryAdd(neighbor, result[current] + 1)) queue.Enqueue(neighbor);
        }
        return result;
    }

    private static void EqualizeStarts(Variant variant)
    {
        var starts = (variant.Zones ?? []).Where(z => z.MainObjects?.Any(o => o.Type == "Spawn" && Array.IndexOf(KnownValues.SpawnPlayers, o.Spawn) >= 0) == true).ToList();
        if (starts.Count == 0) return;
        Zone baseline = starts[0];
        foreach (var zone in starts.Skip(1))
        {
            zone.Size = baseline.Size;
            zone.GuardedContentValue = baseline.GuardedContentValue; zone.GuardedContentValuePerArea = baseline.GuardedContentValuePerArea;
            zone.UnguardedContentValue = baseline.UnguardedContentValue; zone.UnguardedContentValuePerArea = baseline.UnguardedContentValuePerArea;
            zone.ResourcesValue = baseline.ResourcesValue; zone.ResourcesValuePerArea = baseline.ResourcesValuePerArea;
            zone.GuardMultiplier = baseline.GuardMultiplier; zone.GuardRandomization = baseline.GuardRandomization;
        }
    }

    private static void ApplySmallerTeamSupport(Variant variant, AlliancePlan alliance, double multiplier)
    {
        int largest = alliance.Groups.Max(g => g.Players.Count);
        foreach (var group in alliance.Groups.Where(g => g.Players.Count < largest))
            foreach (var zone in variant.Zones ?? [])
                if (zone.MainObjects?.Any(o => o.Spawn is { } spawn && group.Players.Any(p => spawn == $"Player{p}")) == true) ScaleEconomy(zone, multiplier);
    }

    private static void ScaleEconomy(Zone zone, double multiplier)
    {
        zone.GuardedContentValue = Scale(zone.GuardedContentValue, multiplier); zone.GuardedContentValuePerArea = Scale(zone.GuardedContentValuePerArea, multiplier);
        zone.UnguardedContentValue = Scale(zone.UnguardedContentValue, multiplier); zone.UnguardedContentValuePerArea = Scale(zone.UnguardedContentValuePerArea, multiplier);
        zone.ResourcesValue = Scale(zone.ResourcesValue, multiplier); zone.ResourcesValuePerArea = Scale(zone.ResourcesValuePerArea, multiplier);
    }

    private static int? Scale(int? value, double multiplier)
    {
        if (!value.HasValue) return null;
        double scaled = Math.Round(value.Value * multiplier);
        if (!double.IsFinite(scaled) || scaled > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(multiplier), "Content or guard budget exceeds the supported integer range.");
        return Math.Max(0, (int)scaled);
    }

    private static void DistributeNeutralTowns(Variant variant, GeneratorSettings settings)
    {
        var profile = settings.ProfileOptions;
        var zones = variant.Zones ?? [];
        var neutral = zones.Where(z => z.MainObjects?.Any(o => o.Spawn is { } spawn && Array.IndexOf(KnownValues.SpawnPlayers, spawn) >= 0) != true).ToList();
        int maximum = profile.MaximumTownsPerZone ?? Math.Max(1, profile.NeutralTownCount ?? neutral.Select(z => z.MainObjects?.Count(o => o.Type == "City") ?? 0).DefaultIfEmpty(0).Max());
        int desired = profile.NeutralTownCount ?? neutral.Sum(z => Math.Min(maximum, z.MainObjects?.Count(o => o.Type == "City") ?? 0));
        if (desired > (long)neutral.Count * maximum) throw new ArgumentException("The requested neutral towns do not fit the per-zone maximum. Add neutral zones or increase the maximum.");
        int required = neutral.Sum(z => z.MainObjects?.Count(o => o.Type == "City" && o.HoldCityWinCon == true) ?? 0);
        if (desired < required || maximum == 0 && required > 0) throw new ArgumentException("City-hold victory requires its marked town; the chosen town limit would remove the objective.");
        foreach (var zone in neutral)
            for (int i = (zone.MainObjects?.Count ?? 0) - 1; i >= 0; i--)
                if (zone.MainObjects![i].Type == "City" && zone.MainObjects[i].HoldCityWinCon != true) RemoveObject(zones, zone, i);
        int remaining = desired - required;
        MainObject NewCity() => new()
        {
            Type = "City", GuardChance = 1, GuardValue = Scale(12000, settings.ZoneCfg.NeutralStackStrengthPercent / 100.0), GuardWeeklyIncrement = .1,
            BuildingsConstructionSid = "medium_buildings_construction", Placement = "Uniform", PlacementArgs = ["false", "-0.8", "3"],
            Faction = new() { Type = "FromList", Args = string.IsNullOrEmpty(settings.NeutralCastleFaction) ? [] : [settings.NeutralCastleFaction] },
        };
        var branchGroups = neutral.Where(z => z.GeneratorAllianceKey is { } key && key != "contested")
            .GroupBy(z => z.GeneratorAllianceKey!, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
        if (profile.Symmetry == MapSymmetry.Teams && branchGroups.Count > 1)
        {
            // Allocate complete mirrored bundles first. Any non-divisible remainder belongs in
            // common territory, so an odd town total does not quietly favor one team's rear.
            var levels = branchGroups.SelectMany(g => g.Select(z => z.GeneratorBranchDepth!.Value)).Distinct().Order().ToArray();
            bool progress;
            do
            {
                progress = false;
                foreach (int level in levels)
                {
                    var bundle = branchGroups.Select(g => g.Single(z => z.GeneratorBranchDepth == level)).ToArray();
                    if (remaining < bundle.Length || bundle.Any(z => (z.MainObjects?.Count(o => o.Type == "City") ?? 0) >= maximum)) continue;
                    foreach (var zone in bundle) { zone.MainObjects ??= []; zone.MainObjects.Add(NewCity()); remaining--; }
                    progress = true;
                }
            } while (progress && remaining >= branchGroups.Count);
            var common = neutral.Where(z => z.GeneratorAllianceKey == "contested").ToList();
            while (remaining > 0)
            {
                var target = common.FirstOrDefault(z => (z.MainObjects?.Count(o => o.Type == "City") ?? 0) < maximum);
                if (target is null)
                {
                    if (maximum == 0 || common.Count == 0 || zones.Count >= TemplateGenerator.ZoneLetters.Length)
                        throw new ArgumentException("This town total cannot fit symmetric branches and common territory within 48 zones and the per-zone cap. Change the total, cap or symmetry.");
                    var anchor = common[0];
                    target = JsonSerializer.Deserialize<Zone>(JsonSerializer.Serialize(anchor, JsonExport.Options), JsonExport.Options)!;
                    int serial = 1; while (zones.Any(z => z.Name == $"Alliance-Common-{serial}")) serial++;
                    target.Name = $"Alliance-Common-{serial}"; target.MainObjects = []; target.MandatoryContent = null; target.Roads = null;
                    target.GeneratorAllianceKey = "contested"; target.GeneratorBranchDepth = null;
                    target.ZoneBiome = new() { Type = "FromList", Args = ["Grass"] };
                    target.ContentBiome = new() { Type = "MatchZone", Args = [target.Name] };
                    target.MetaObjectsBiome = new() { Type = "MatchZone", Args = [target.Name] };
                    zones.Add(target); neutral.Add(target); common.Add(target);
                    variant.Connections ??= [];
                    variant.Connections.Add(new()
                    {
                        Name = $"Alliance-Common-Link-{serial}", From = anchor.Name, To = target.Name, ConnectionType = "Direct",
                        GuardZone = anchor.Name, GuardValue = settings.ZoneCfg.BorderGuardStrengthPercent == 0 ? 0 : Scale(10000, settings.ZoneCfg.BorderGuardStrengthPercent / 100.0),
                        GuardWeeklyIncrement = .15, GuardMatchGroup = "alliance-front", Road = settings.GenerateRoads,
                    });
                }
                target.MainObjects ??= []; target.MainObjects.Add(NewCity()); remaining--;
            }
            return;
        }
        while (remaining > 0)
        {
            bool progress = false;
            foreach (var zone in neutral)
            {
                zone.MainObjects ??= [];
                if (zone.MainObjects.Count(o => o.Type == "City") >= maximum || remaining == 0) continue;
                zone.MainObjects.Add(NewCity());
                remaining--; progress = true;
            }
            if (!progress) throw new InvalidOperationException("Could not distribute the requested neutral towns within the selected limits.");
        }
    }

    private static void RemoveObject(List<Zone> zones, Zone zone, int removedIndex)
    {
        foreach (var current in zones)
            foreach (var obj in current.MainObjects ?? []) FactionSelectors.RemoveSource(obj.Faction, current.Name, zone.Name, removedIndex);
        zone.Roads = (zone.Roads ?? []).Where(road => !new[] { road.From, road.To }.Any(endpoint => endpoint?.Type == "MainObject" && endpoint.Args?.FirstOrDefault() == removedIndex.ToString(CultureInfo.InvariantCulture))).ToList();
        foreach (var endpoint in zone.Roads.SelectMany(road => new[] { road.From, road.To }))
            if (endpoint?.Type == "MainObject" && endpoint.Args is { Count: > 0 } args && int.TryParse(args[0], out int index) && index > removedIndex) args[0] = (index - 1).ToString(CultureInfo.InvariantCulture);
        foreach (var biome in new[] { zone.ZoneBiome, zone.ContentBiome, zone.MetaObjectsBiome })
            if (biome?.Type == "MatchMainObject" && biome.Args is { Count: > 0 } args && int.TryParse(args[0], out int index))
                if (index == removedIndex) { biome.Type = "FromList"; biome.Args = ["Grass"]; }
                else if (index > removedIndex) args[0] = (index - 1).ToString(CultureInfo.InvariantCulture);
        zone.MainObjects!.RemoveAt(removedIndex);
    }
}
