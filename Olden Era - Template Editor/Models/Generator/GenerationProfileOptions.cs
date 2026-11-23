using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Olden_Era___Template_Editor.Models;

public enum MapRichness { VeryPoor, Poor, Normal, Rich, VeryRich }
public enum MapChallenge { Easy, Normal, Hard, Unfair, Impossible, Apocalypse }
public enum MapSymmetry { Legacy, Asymmetric, EqualStarts, Teams }
public enum MapTempo { Legacy, EarlyContact, Balanced, Exploration }
public enum AllianceLayout { ExistingTopology, TeamRegions, TeamLanes, TeamArena }

/// <summary>A player slot is independent of its lobby human/AI selection. -1 is an individual FFA side.</summary>
public sealed class AllianceSlotSettings
{
    public int Player { get; set; }
    public int Alliance { get; set; } = -1;
    public bool Locked { get; set; }
}

public sealed class AllianceSettings
{
    public bool Enabled { get; set; }
    public string Preset { get; set; } = "auto";
    public bool Locked { get; set; }
    public AllianceLayout Layout { get; set; } = AllianceLayout.TeamRegions;
    public bool RandomizePlayerAssignment { get; set; }
    public List<AllianceSlotSettings> Slots { get; set; } = [];
    /// <summary>Optional explicit resource support for the smaller team; 1 preserves equal per-player budgets.</summary>
    public double SmallerTeamEconomyMultiplier { get; set; } = 1;
}

/// <summary>Versioned additions. The legacy default preserves historical generator seeds and presets.</summary>
public sealed partial class GenerationProfileOptions
{
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    public System.Text.Json.Nodes.JsonObject? ImportedNativeConfiguration { get; set; }
    private JsonElement? _importedNativeSchema;
    public JsonElement? ImportedNativeSchema
    {
        get => _importedNativeSchema;
        set => _importedNativeSchema = value?.ValueKind switch
        {
            null or JsonValueKind.Null => null,
            JsonValueKind.Object => value!.Value.Clone(),
            _ => throw new JsonException("An imported native schema must be a JSON object.")
        };
    }
    public const string LegacyProfile = "aurora-1.11";
    public const string PreviousProfile = "olden-era-0.81.04";
    public const string CurrentGameVersion = "0.82.10";
    public const string CurrentProfile = "olden-era-" + CurrentGameVersion;
    public int SchemaVersion { get; set; } = 1;
    public string ProfileId { get; set; } = LegacyProfile;
    public string AlgorithmVersion { get; set; } = "aurora-generator-2";
    public int? TemplateSeed { get; set; }
    /// <summary>Recorded for the game setup notes; never used by Aurora's template RNG.</summary>
    public int? GameSeed { get; set; }
    public MapRichness Richness { get; set; } = MapRichness.Normal;
    public MapChallenge Challenge { get; set; } = MapChallenge.Normal;
    public MapSymmetry Symmetry { get; set; } = MapSymmetry.Legacy;
    public MapTempo Tempo { get; set; } = MapTempo.Legacy;
    public AllianceSettings Alliances { get; set; } = new();
    public int? NeutralTownCount { get; set; }
    public int? MaximumTownsPerZone { get; set; }
    public bool UseAreaBudgets { get; set; }
    public bool EnableRivers { get; set; }
    public double RiverTributarySpacing { get; set; } = 20;
    public bool? EnableWeeklyUnitIncrement { get; set; }
    public bool? WeekOneOnlyModificatorGrowth { get; set; }
    public int? InitialUnitIncrement { get; set; }
    public bool DisableFactionLaws { get; set; }
    public bool DisableMagicGuild { get; set; }
    public bool DisableMagicCustomLearning { get; set; }
    public bool? ResetCityHoldTimer { get; set; }
    public bool IncludeTemplateCard { get; set; }
    public bool ClassicVictoryEnabled { get; set; } = true;
    public List<int> TournamentBattleDays { get; set; } = [];
    public int? TournamentAnnouncementLeadDays { get; set; }
    public bool UseNativeFinalBattleSchedule { get; set; }
    public int? FinalBattleDay { get; set; }
    public bool? PortalFromEnabled { get; set; }
    public bool? PortalToEnabled { get; set; }
    public string? BorderGuardReaction { get; set; }
    public string? CustomAI { get; set; }
    public string AuthorDescription { get; set; } = "";

    [JsonIgnore] public bool UsesVersionedRules => UsesVersionedRulesFor(ProfileId);
    public static bool UsesVersionedRulesFor(string? profileId) => profileId is PreviousProfile or CurrentProfile;
    public static GenerationProfileOptions CurrentDefaults() => new()
    {
        ProfileId = CurrentProfile,
        IncludeTemplateCard = true,
        Symmetry = MapSymmetry.EqualStarts,
        Tempo = MapTempo.Balanced
    };

    private static readonly JsonSerializerOptions CloneOptions = CreateCloneOptions();

    internal static JsonSerializerOptions CreateCloneOptions(JsonSerializerOptions? baseline = null)
    {
        var options = baseline is null ? new JsonSerializerOptions() : new JsonSerializerOptions(baseline);
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(GenerationProfileOptions)) return;
            var schema = info.Properties.Single(property => property.Name == nameof(ImportedNativeSchema));
            schema.ShouldSerialize = static (_, _) => false;
        });
        options.TypeInfoResolver = resolver;
        return options;
    }

    public GenerationProfileOptions Clone()
    {
        var copy = JsonSerializer.Deserialize<GenerationProfileOptions>(JsonSerializer.SerializeToUtf8Bytes(this, CloneOptions), CloneOptions)
            ?? throw new InvalidOperationException("Generation options could not be copied.");
        copy._importedNativeSchema = _importedNativeSchema;
        return copy;
    }
}
