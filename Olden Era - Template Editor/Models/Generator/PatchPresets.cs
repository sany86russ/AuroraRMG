using System.Text.Json;
using Olden_Era___Template_Editor.Services;

namespace Olden_Era___Template_Editor.Models;

/// <summary>Explicit patch variants leave the established 43 legacy presets and their settings intact.</summary>
public static class PatchPresets
{
    public sealed record PatchPreset(string Id, string NameRu, string NameEn, string DescriptionRu, string DescriptionEn, SettingsFile Settings);

    public static IReadOnlyList<PatchPreset> Current { get; } = Presets.All.Select((preset, index) =>
    {
        var settings = Clone(preset.Settings);
        settings.ProfileOptions = GenerationProfileOptions.CurrentDefaults();
        // Keep the established topology/pacing while modernizing the confirmed format and metadata.
        settings.ProfileOptions.Tempo = MapTempo.Legacy;
        return new PatchPreset("patch-" + index, preset.Name, preset.ShortNameLocalized(true),
            preset.Description + " Профиль данных 0.81.04; seed относится к шаблону Aurora.",
            preset.DescriptionLocalized(true) + " 0.81.04 data profile; this seed controls the Aurora template.", settings);
    }).ToArray();

    public static IReadOnlyList<PatchPreset> Cooperative { get; } =
    [
        Team("2v2", "2 на 2", "2 vs 2", AllianceLayout.TeamRegions),
        Team("3v3", "3 на 3", "3 vs 3", AllianceLayout.TeamLanes),
        Team("4v4", "4 на 4", "4 vs 4", AllianceLayout.TeamRegions),
        Team("2v2v2", "Три команды по два", "Three teams of two", AllianceLayout.TeamArena),
        Team("2v2v2v2", "Четыре команды по два", "Four teams of two", AllianceLayout.TeamArena),
        Team("2v6", "Два друга против шести", "Two friends vs six", AllianceLayout.TeamRegions),
        Team("3v5", "Три друга против пяти", "Three friends vs five", AllianceLayout.TeamLanes),
        Team("4v4", "Четыре друга против четырёх", "Four friends vs four", AllianceLayout.TeamLanes, suffix: "friends"),
        Team("2v2", "Команды: один герой", "Teams: single hero", AllianceLayout.TeamArena, singleHero: true),
        Team("2v6", "Долгое PvE: два друга", "Long PvE: two friends", AllianceLayout.TeamRegions, longGame: true)
    ];

    private static PatchPreset Team(string composition, string ru, string en, AllianceLayout layout,
        bool singleHero = false, bool longGame = false, string suffix = "")
    {
        int players = composition.Split('v').Select(int.Parse).Sum();
        var options = GenerationProfileOptions.CurrentDefaults();
        options.Symmetry = MapSymmetry.Teams;
        options.Tempo = longGame ? MapTempo.Exploration : MapTempo.Balanced;
        options.Alliances = new() { Enabled = true, Preset = composition, Layout = layout, Locked = false };
        var settings = new SettingsFile
        {
            TemplateName = "Aurora " + composition + (singleHero ? " Single Hero" : longGame ? " Long PvE" : " " + layout),
            MapSize = longGame || players == 8 ? 256 : players >= 6 ? 224 : 192,
            PlayerCount = players, NeutralZoneCount = players * (longGame ? 3 : 2), NeutralZoneCastles = 1,
            PlayerStartsWithCastles = true, PlayerZoneCastles = 1, HubZoneCastles = 1, HubZoneSize = 1.5,
            Topology = MapTopology.HubAndSpoke, NoDirectPlayerConn = true, MinNeutralZonesBetweenPlayers = longGame ? 2 : 1,
            GenerateRoads = true, SingleHeroMode = singleHero, HeroCountMin = singleHero ? 1 : 4,
            HeroCountMax = singleHero ? 1 : 10, HeroCountIncrement = singleHero ? 0 : 1, ProfileOptions = options
        };
        return new("teams-" + composition + "-" + layout + "-" + singleHero + "-" + longGame + suffix, ru, en,
            "Настоящие альянсы " + composition + ". Выберите друзей и ИИ в lobby или hotseat. Баланс структуры не гарантирует одинаковую геометрию игры.",
            "Actual " + composition + " alliances. Select friends and AI in lobby or hotseat. Structural balance does not guarantee identical in-game geometry.", settings);
    }

    public static SettingsFile Clone(SettingsFile settings) => JsonSerializer.Deserialize<SettingsFile>(
        JsonSerializer.Serialize(settings, JsonExport.Options), JsonExport.Options)!;
}
