using System.Globalization;
using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor.Services.Generation;

public static class TemplateCardBuilder
{
    /// <summary>Uses verified text/spacer/tags blocks without inventing icon or localization SIDs.</summary>
    public static void Refresh(RmgTemplate template, GeneratorSettings settings, AlliancePlan? alliance = null)
    {
        var profile = settings.ProfileOptions;
        alliance ??= AlliancePlan.Resolve(settings.PlayerCount, profile.Alliances, settings.Seed);
        string teams = alliance.Enabled ? string.Join("v", alliance.Groups.Select(g => g.Players.Count)) : "FFA";
        string victory = template.DisplayWinCondition ?? "win_condition_1";
        int victoryIndex = Array.IndexOf(KnownValues.VictoryConditionIds, victory);
        string victoryName = victoryIndex >= 0 ? LocalizationManager.T($"S.Victory.{victoryIndex}") : victory;
        if (template.GameRules?.WinConditions is { } conditions)
        {
            var enabled = new List<int>();
            if (conditions.Classic == true) enabled.Add(0);
            if (conditions.CityHold == true) enabled.Add(1);
            if (conditions.Tournament == true) enabled.Add(2);
            if (conditions.GladiatorArena == true) enabled.Add(3);
            if (conditions.LostStartCity == true) enabled.Add(4);
            if (conditions.LostStartHero == true) enabled.Add(5);
            if (enabled.Count > 0) victoryName = string.Join(" / ", enabled.Select(index => LocalizationManager.T($"S.Victory.{index}")));
        }
        int startingTowns = settings.PlayerStartsWithCastles ? Math.Max(1, settings.ZoneCfg.PlayerZoneCastles) : 1;
        int startingHeroes = template.GameMode == "SingleHero" ? template.GameRules?.HeroCountMin ?? 1
            : Math.Min(template.GameRules?.HeroCountMax ?? int.MaxValue, (template.GameRules?.HeroCountMin ?? 0) + (template.GameRules?.HeroCountIncrement ?? 0) * startingTowns);
        string summary = LocalizationManager.T("S.Patch.Card.Summary", settings.PlayerCount, teams, template.SizeX, template.SizeZ,
            victoryName, startingHeroes, template.GameRules?.HeroCountMax ?? 0,
            LocalizationManager.T($"S.Patch.MapRichness.{profile.Richness}"), LocalizationManager.T($"S.Patch.MapChallenge.{profile.Challenge}"), LocalizationManager.T($"S.Patch.MapTempo.{profile.Tempo}"));
        if (settings.Seed.HasValue) summary += " " + LocalizationManager.T("S.Patch.Card.Provenance", settings.Seed.Value.ToString("X8", CultureInfo.InvariantCulture), profile.ProfileId, profile.AlgorithmVersion);
        var blocks = new List<DescriptionBlock> { new() { Type = "text", Parameters = [summary] } };
        blocks.Add(new() { Type = "text", Parameters = [LocalizationManager.T("S.Patch.Card.Tuning",
            settings.ZoneCfg.ResourceDensityPercent, settings.ZoneCfg.StructureDensityPercent, settings.ZoneCfg.NeutralStackStrengthPercent, settings.ZoneCfg.BorderGuardStrengthPercent)] });
        if (template.GameRules?.WinConditions is { Desertion: true } desertion)
            blocks.Add(new() { Type = "text", Parameters = [LocalizationManager.T("S.Patch.Card.Desertion", desertion.DesertionDay ?? 0, desertion.DesertionValue ?? 0)] });
        if (template.GameRules?.WinConditions is { Tournament: true, TournamentDays: { } offsets, TournamentAnnounceDays: { } announcements })
        {
            var calendar = BattleCalendar.Decode(announcements, offsets);
            blocks.Add(new() { Type = "text", Parameters = [LocalizationManager.T("S.Patch.Card.Tournament", string.Join(", ", calendar.BattleDays), string.Join(", ", announcements),
                LocalizationManager.T(template.GameRules.WinConditions.TournamentSaveArmy == true ? "S.Patch.Yes" : "S.Patch.No"))] });
        }
        if (template.GameRules?.WinConditions?.GladiatorArena == true)
        {
            if (profile.UseNativeFinalBattleSchedule)
                blocks.Add(new() { Type = "text", Parameters = [LocalizationManager.T("S.Patch.Card.FinalDay", profile.FinalBattleDay ?? settings.GladiatorArenaRules.DaysDelayStart)] });
            blocks.Add(new() { Type = "text", Parameters = [LocalizationManager.T("S.Patch.Card.Final", template.GameRules.WinConditions.GladiatorArenaDaysDelayStart ?? 0,
                template.GameRules.WinConditions.GladiatorArenaCountDay ?? 0, profile.UseNativeFinalBattleSchedule ? GenerationProfileOptions.CurrentProfile : GenerationProfileOptions.LegacyProfile)] });
        }
        if (!string.IsNullOrWhiteSpace(profile.AuthorDescription))
        { blocks.Add(new() { Type = "spacer" }); blocks.Add(new() { Type = "text", Parameters = [profile.AuthorDescription] }); }
        var tags = new List<string> { "map_tags_1", "map_tags_2", template.GameMode == "SingleHero" || template.GameRules?.HeroCountMax == 1 ? "map_tags_14" : "map_tags_15" };
        tags.Add(profile.Richness <= MapRichness.Poor ? "map_tags_6" : profile.Richness >= MapRichness.Rich ? "map_tags_8" : "map_tags_7");
        tags.Add(profile.Tempo == MapTempo.EarlyContact ? "map_tags_3" : profile.Tempo == MapTempo.Exploration ? "map_tags_5" : "map_tags_4");
        tags.Add(victory == "win_condition_1" ? "map_tags_12" : "map_tags_11");
        if (alliance.Enabled && alliance.Groups.Any(g => g.Players.Count > 1)) tags.Add("map_tags_16");
        blocks.Add(new() { Type = "tags", Parameters = [string.Join(",", tags)] });
        template.DescBlocks = new() { Blocks = blocks };
        template.Description = summary + (string.IsNullOrWhiteSpace(profile.AuthorDescription) ? "" : "\n" + profile.AuthorDescription);
    }
}
