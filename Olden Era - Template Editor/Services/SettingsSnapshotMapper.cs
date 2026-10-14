using System.Text.Json;
using System.Text.Json.Serialization;
using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;
using OldenEraTemplateEditor.Services.ContentManagement;

namespace Olden_Era___Template_Editor.Services;

/// <summary>Captures actual generator inputs independently of UI controls, preserving native and future fields.</summary>
public static class SettingsSnapshotMapper
{
    public const int SnapshotVersion = 1;
    // BonusEntry also exposes localized labels and a WPF brush. Those are UI outputs, not generator inputs.
    internal static readonly JsonSerializerOptions SnapshotOptions = new(JsonExport.Options)
    {
        IncludeFields = true,
        IgnoreReadOnlyProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static GeneratorSettings Clone(GeneratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var clone = JsonSerializer.Deserialize<GeneratorSettings>(JsonSerializer.SerializeToUtf8Bytes(settings, SnapshotOptions), SnapshotOptions)
            ?? throw new JsonException("Generator settings could not be copied.");
        clone.Seed ??= clone.ProfileOptions?.TemplateSeed;
        return clone;
    }

    public static GeneratorSettings? RestoreSnapshot(SettingsFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.GeneratorSnapshot is not { } snapshot) return null;
        if (file.GeneratorSnapshotVersion != SnapshotVersion)
            throw new JsonException($"Unsupported generator snapshot version: {file.GeneratorSnapshotVersion}.");
        if (snapshot.ValueKind != JsonValueKind.Object) throw new JsonException("The generator snapshot must be an object.");
        var settings = snapshot.Deserialize<GeneratorSettings>(SnapshotOptions)
            ?? throw new JsonException("The generator snapshot could not be restored.");
        EnsureContainers(settings);
        return settings;
    }

    public static SettingsFile FromGeneratorSettings(GeneratorSettings settings)
    {
        var s = Clone(settings);
        EnsureContainers(s);
        var z = s.ZoneCfg; var a = z.Advanced; var h = s.HeroSettings; var end = s.GameEndConditions;
        return new SettingsFile
        {
            GeneratorSnapshotVersion = SnapshotVersion,
            GeneratorSnapshot = JsonSerializer.SerializeToElement(s, SnapshotOptions),
            Seed = s.Seed, ProfileOptions = s.ProfileOptions.Clone(), TemplateName = s.TemplateName,
            GameMode = s.GameMode, MapSize = s.MapSize, PlayerCount = s.PlayerCount,
            SingleHeroMode = s.SingleHeroMode, HeroCountMin = h.HeroCountMin, HeroCountMax = h.HeroCountMax,
            HeroCountIncrement = h.HeroCountIncrement, HeroHireBan = h.HeroHireBan,
            NeutralZoneCount = z.NeutralZoneCount, PlayerZoneCastles = z.PlayerZoneCastles, NeutralZoneCastles = z.NeutralZoneCastles,
            AdvancedMode = a.Enabled, NeutralLowNoCastleCount = a.NeutralLowNoCastleCount, NeutralLowCastleCount = a.NeutralLowCastleCount,
            NeutralMediumNoCastleCount = a.NeutralMediumNoCastleCount, NeutralMediumCastleCount = a.NeutralMediumCastleCount,
            NeutralHighNoCastleCount = a.NeutralHighNoCastleCount, NeutralHighCastleCount = a.NeutralHighCastleCount,
            PlayerZoneSize = a.PlayerZoneSize, NeutralZoneSize = a.NeutralZoneSize, GuardRandomization = a.GuardRandomization,
            HubZoneSize = z.HubZoneSize, HubZoneCastles = z.HubZoneCastles,
            ResourceDensityPercent = z.ResourceDensityPercent, StructureDensityPercent = z.StructureDensityPercent,
            NeutralStackStrengthPercent = z.NeutralStackStrengthPercent, BorderGuardStrengthPercent = z.BorderGuardStrengthPercent,
            MatchPlayerCastleFactions = s.MatchPlayerCastleFactions, MatchSpawnTerrainToFaction = s.MatchSpawnTerrainToFaction,
            PlayerStartsWithCastles = s.PlayerStartsWithCastles, NeutralCastleFaction = s.NeutralCastleFaction,
            MinNeutralZonesBetweenPlayers = s.MinNeutralZonesBetweenPlayers, Topology = s.Topology,
            NoDirectPlayerConn = s.NoDirectPlayerConnections, RandomPortals = s.RandomPortals,
            MaxPortalConnections = s.MaxPortalConnections, SpawnRemoteFootholds = s.SpawnRemoteFootholds, GenerateRoads = s.GenerateRoads,
            Terrain = s.Terrain, MonsterAggression = s.MonsterAggression, NeutralDiplomacyModifier = s.NeutralDiplomacyModifier,
            WaterLevel = s.WaterLevel, EncounterHoles = s.EncounterHoles,
            TerrainRoughnessPercent = s.TerrainRoughnessPercent, LakeAmountPercent = s.LakeAmountPercent,
            FactionLawsExpPercent = s.FactionLawsExpPercent, AstrologyExpPercent = s.AstrologyExpPercent,
            VictoryCondition = end.VictoryCondition, LostStartCity = end.LostStartCity, LostStartCityDay = end.LostStartCityDay,
            LostStartHero = end.LostStartHero, CityHold = end.CityHold, CityHoldDays = end.CityHoldDays,
            HeroLighting = end.HeroLighting, HeroLightingDay = end.HeroLightingDay,
            GladiatorArena = s.GladiatorArenaRules.Enabled, GladiatorArenaDaysDelayStart = s.GladiatorArenaRules.DaysDelayStart,
            GladiatorArenaCountDay = s.GladiatorArenaRules.CountDay, Tournament = s.TournamentRules.Enabled,
            TournamentFirstTournamentDay = s.TournamentRules.FirstTournamentDay, TournamentInterval = s.TournamentRules.Interval,
            TournamentPointsToWin = s.TournamentRules.PointsToWin, TournamentSaveArmy = s.TournamentRules.SaveArmy,
            BannedItems = s.BannedItems, BannedMagics = s.BannedMagics, BannedHeroes = s.BannedHeroes, ValueOverridesText = s.ValueOverridesText,
            BonusesJson = string.Join("\n", s.Bonuses.Select(bonus => bonus.ToString())),
            PlayerZoneContentRows = ProjectRows(s.PlayerZoneMandatoryContent), LowNeutralContentRows = ProjectRows(s.LowNeutralMandatoryContent),
            MediumNeutralContentRows = ProjectRows(s.MediumNeutralMandatoryContent), HighNeutralContentRows = ProjectRows(s.HighNeutralMandatoryContent),
            HubZoneContentRows = ProjectRows(s.HubZoneMandatoryContent),
        };
    }

    internal static List<ContentItem> ExpandRows(IEnumerable<ZoneContentRowSave>? rows)
    {
        var items = new List<ContentItem>();
        foreach (var row in rows ?? [])
        {
            if (row is null || string.IsNullOrWhiteSpace(row.Sid)) throw new JsonException("A content row needs an exact SID.");
            if (row.Count is < 0 or > 10000) throw new JsonException("Content row count must be between 0 and 10000.");
            var rules = new List<ContentPlacementRule>();
            if (row.NearCastle && !row.IsGroup) rules.Add(RulePresets.NearCastle());
            if (row.RoadDistance != "Any") rules.Add(RulePresets.RoadDistance(Distance(row.RoadDistance)));
            for (int i = 0; i < row.Count; i++)
                items.Add(new ContentItem
                {
                    Sid = row.IsGroup ? null : row.Sid, IncludeLists = row.IsGroup ? [row.Sid] : null,
                    IsGuarded = row.IsGuarded, IsMine = row.IsMine && !row.IsGroup ? true : null,
                    Rules = rules.Count == 0 ? null : rules.Select(rule => new ContentPlacementRule
                    { Type = rule.Type, Args = rule.Args is null ? null : [..rule.Args], Weight = rule.Weight, TargetMin = rule.TargetMin, TargetMax = rule.TargetMax }).ToList(),
                });
        }
        return items;
    }

    internal static List<BonusEntry> ReadBonuses(string? text)
    {
        var result = new List<BonusEntry>();
        foreach (string line in (text ?? "").Split('\n'))
        {
            string value = line.TrimEnd('\r'); if (string.IsNullOrWhiteSpace(value)) continue;
            var bonus = BonusEntry.FromString(value);
            if (bonus is null || !Enum.IsDefined(bonus.PresetType)) throw new JsonException($"Invalid saved bonus record at position {result.Count + 1}.");
            result.Add(bonus);
        }
        return result;
    }

    private static void EnsureContainers(GeneratorSettings settings)
    {
        if (settings.ProfileOptions?.Alliances?.Slots is null || settings.ProfileOptions.Alliances.Slots.Any(slot => slot is null) || settings.ProfileOptions.TournamentBattleDays is null ||
            settings.ZoneCfg?.Advanced is null || settings.HeroSettings is null || settings.GameEndConditions is null ||
            settings.GladiatorArenaRules is null || settings.TournamentRules is null || settings.Bonuses is null || settings.Bonuses.Any(bonus => bonus is null))
            throw new JsonException("Required generator settings groups and collections cannot be null.");
        foreach (var items in new[] { settings.PlayerZoneMandatoryContent, settings.LowNeutralMandatoryContent,
            settings.MediumNeutralMandatoryContent, settings.HighNeutralMandatoryContent, settings.HubZoneMandatoryContent })
            if (items is null || items.Any(item => item is null || item.Rules?.Any(rule => rule is null) == true))
                throw new JsonException("Mandatory content collections, items and rules cannot contain null entries.");
    }

    private static List<ZoneContentRowSave> ProjectRows(IEnumerable<ContentItem> items) => items.SelectMany(item =>
    {
        var ids = item.Sid is { Length: > 0 } sid ? new[] { sid } : item.IncludeLists?.ToArray() ?? [];
        return ids.Select(id => new ZoneContentRowSave
        {
            Sid = id, Count = 1, IsGroup = item.Sid is null, IsGuarded = item.IsGuarded == true, IsMine = item.IsMine == true,
            NearCastle = item.Rules?.Any(rule => rule.Type == "MainObject" && rule.Args?.Contains("0") == true) == true,
            RoadDistance = ProjectDistance(item.Rules?.FirstOrDefault(rule => rule.Type == "Road")),
        });
    }).ToList();

    private static DistanceVariation Distance(string label) => label switch
    {
        "Next To" => DistancePresets.NextTo, "Near" => DistancePresets.Near, "Medium" => DistancePresets.Medium,
        "Far" => DistancePresets.Far, "Very Far" => DistancePresets.VeryFar,
        _ => throw new JsonException("Unknown road-distance label: " + label),
    };
    private static string ProjectDistance(ContentPlacementRule? rule)
    {
        if (rule is null) return "Any";
        foreach (string label in new[] { "Next To", "Near", "Medium", "Far", "Very Far" })
        { var distance = Distance(label); if (rule.TargetMin == distance.Min && rule.TargetMax == distance.Max) return label; }
        return "Any"; // the canonical snapshot retains a nonstandard placement rule exactly
    }
}
