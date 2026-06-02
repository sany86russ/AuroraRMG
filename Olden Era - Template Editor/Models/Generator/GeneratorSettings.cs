using OldenEraTemplateEditor.Models;
namespace Olden_Era___Template_Editor.Models
{
    public class TournamentRules
    {
        public bool Enabled { get; set; } = false;
        public int FirstTournamentDay { get; set; } = 14;
        public int Interval { get; set; } = 7;
        public int PointsToWin { get; set; } = 2;
        public bool SaveArmy { get; set; } = true;
    }
    public class GladiatorArenaRules
    {
        public bool Enabled { get; set; } = false;
        public int DaysDelayStart { get; set; } = 30;
        public int CountDay { get; set; } = 3;
    }

    public class GameEndConditions
    {
        public string VictoryCondition { get; set; } = "win_condition_1";
        public bool LostStartCity { get; set; } = false;
        public int LostStartCityDay { get; set; } = 3;
        public bool LostStartHero { get; set; } = false;
        public bool CityHold { get; set; } = false;
        public int CityHoldDays { get; set; } = 6;
        /// <summary>Highlight enemy players on the minimap (engine <c>heroLighting</c>). Default on.</summary>
        public bool HeroLighting { get; set; } = true;
        /// <summary>First in-game day the minimap highlight starts (engine <c>heroLightingDay</c>).</summary>
        public int HeroLightingDay { get; set; } = 1;
    }

    public class HeroSettings
    {
        public int HeroCountMin { get; set; } = 4;
        public int HeroCountMax { get; set; } = 8;
        public int HeroCountIncrement { get; set; } = 1;
        /// <summary>Ban hiring additional heroes (engine <c>heroHireBan</c>). Forced on in single-hero mode.</summary>
        public bool HeroHireBan { get; set; } = false;
    }

    public class AdvancedSettings
    {
        public bool Enabled { get; set; } = false;
        public int NeutralLowNoCastleCount { get; set; } = 0;
        public int NeutralLowCastleCount { get; set; } = 0;
        public int NeutralMediumNoCastleCount { get; set; } = 0;
        public int NeutralMediumCastleCount { get; set; } = 0;
        public int NeutralHighNoCastleCount { get; set; } = 0;
        public int NeutralHighCastleCount { get; set; } = 0;
        public double PlayerZoneSize { get; set; } = 1.0;
        public double NeutralZoneSize { get; set; } = 1.0;
        public double GuardRandomization { get; set; } = 0.05;
    }
    public class ZoneConfiguration
    {
        public int NeutralZoneCount { get; set; } = 0;
        public int PlayerZoneCastles { get; set; } = 1;
        public int NeutralZoneCastles { get; set; } = 1;
        public int ResourceDensityPercent { get; set; } = 100;
        public int StructureDensityPercent { get; set; } = 100;
        public int NeutralStackStrengthPercent { get; set; } = 100;
        public int BorderGuardStrengthPercent { get; set; } = 100;
        public double HubZoneSize { get; set; } = 1.0;
        public int HubZoneCastles { get; set; } = 0;
        public AdvancedSettings Advanced { get; set; } = new AdvancedSettings();
    }

    public class GeneratorSettings
    {
        public string TemplateName { get; set; } = "Custom Template";
        public string GameMode { get; set; } = "Classic";
        public bool SingleHeroMode { get; set; } = false;
        public int PlayerCount { get; set; } = 2;
        public int MapSize { get; set; } = 160;
        /// <summary>
        /// Optional deterministic seed. When set, <see cref="Services.TemplateGenerator.Generate"/>
        /// seeds its internal placement RNG so the same settings reproduce the same map (the basis of
        /// Simple Mode's shareable seeds). Null = time-based RNG, i.e. the legacy non-deterministic
        /// behaviour used by the manual/advanced path.
        /// </summary>
        public int? Seed { get; set; }
        public HeroSettings HeroSettings { get; set; } = new HeroSettings();
        
        public bool NoDirectPlayerConnections { get; set; } = false;
        public bool RandomPortals { get; set; } = false;
        public int MaxPortalConnections { get; set; } = 32;
        public bool SpawnRemoteFootholds { get; set; } = true;
        public bool GenerateRoads { get; set; } = true;
        public bool MatchPlayerCastleFactions { get; set; } = false;
        public bool PlayerStartsWithCastles { get; set; } = false;
        public int MinNeutralZonesBetweenPlayers { get; set; } = 0;
        public MapTopology Topology { get; set; } = MapTopology.Balanced;
        public ZoneConfiguration ZoneCfg { get; set; } = new ZoneConfiguration();
        public int FactionLawsExpPercent { get; set; } = 100;
        public int AstrologyExpPercent { get; set; } = 100;

        // ── Environment & encounter tuning ───────────────────────────────────────
        /// <summary>Terrain (biome) theme applied across the map.</summary>
        public TerrainTheme Terrain { get; set; } = TerrainTheme.FactionBased;
        /// <summary>How aggressively neutral guards react to the player.</summary>
        public MonsterAggression MonsterAggression { get; set; } = MonsterAggression.Normal;
        /// <summary>Diplomacy modifier applied to every zone (<c>-1.0</c> = never join, <c>0.5</c> = eager). Engine default is <c>-0.5</c>.</summary>
        public double NeutralDiplomacyModifier { get; set; } = -0.5;
        /// <summary>Enables "encounter holes" — guards that can be sneaked past.</summary>
        public bool EncounterHoles { get; set; } = false;
        /// <summary>Amount of water placed on zone borders.</summary>
        public WaterLevel WaterLevel { get; set; } = WaterLevel.None;
        /// <summary>Scales obstacle (rough terrain) density in every zone, in percent. 100 = engine default.</summary>
        public int TerrainRoughnessPercent { get; set; } = 100;
        /// <summary>Scales lake coverage in every zone, in percent. 100 = engine default.</summary>
        public int LakeAmountPercent { get; set; } = 100;
        public string BannedItems { get; set; } = "";
        public string BannedMagics { get; set; } = "";
        public string BannedHeroes { get; set; } = "";
        public string ValueOverridesText { get; set; } = "";
        public System.Collections.Generic.List<OldenEraTemplateEditor.Models.BonusEntry> Bonuses { get; set; } = [];
        public List<ContentItem> PlayerZoneMandatoryContent { get; set; } = new List<ContentItem>();
        public List<ContentItem> LowNeutralMandatoryContent { get; set; } = new List<ContentItem>();
        public List<ContentItem> MediumNeutralMandatoryContent { get; set; } = new List<ContentItem>();
        public List<ContentItem> HighNeutralMandatoryContent { get; set; } = new List<ContentItem>();
        public List<ContentItem> HubZoneMandatoryContent { get; set; } = new List<ContentItem>();
        public GameEndConditions GameEndConditions { get; set; } = new GameEndConditions();
        public GladiatorArenaRules GladiatorArenaRules { get; set; } = new GladiatorArenaRules();
        public TournamentRules TournamentRules { get; set; } = new TournamentRules();
    }

    public enum NeutralZoneQuality
    {
        Low,
        Medium,
        High
    }
}
