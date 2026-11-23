using System.Globalization;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.Generation;

internal static class ChampionSpirePlacement
{
    public const string SelectedRule = "Selected";
    public const string SpireSid = "gladiator_spire";

    public static bool HasLinkedSpire(RmgTemplate template, Variant variant)
    {
        var groups = (variant.Zones ?? []).Where(zone => zone is not null)
            .SelectMany(zone => zone.MandatoryContent ?? []).ToHashSet(StringComparer.Ordinal);
        return (template.MandatoryContent ?? []).Any(group => group is not null && groups.Contains(group.Name)
            && group.Content?.Any(item => item?.Sid == SpireSid) == true);
    }

    /// <summary>Fills variants that lack an authored spire. Existing placement is preserved.</summary>
    public static void AddToStarts(RmgTemplate template)
    {
        var names = (template.MandatoryContent ?? []).Where(group => group is not null)
            .Select(group => group.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var (variant, variantIndex) in (template.Variants ?? []).Select((variant, index) => (variant, index)))
        {
            if (variant is null || HasLinkedSpire(template, variant)) continue;
            foreach (var zone in variant.Zones ?? [])
            {
                if (zone?.MainObjects is not { } objects) continue;
                for (int index = 0; index < objects.Count; index++)
                {
                    if (objects[index] is not { Type: "Spawn", Spawn: { } player }
                        || !KnownValues.SpawnPlayers.Contains(player, StringComparer.Ordinal)) continue;
                    string stem = $"aurora_champion_spire_{variantIndex + 1}_{player}";
                    string group = stem;
                    for (int suffix = 2; !names.Add(group); suffix++) group = $"{stem}_{suffix}";
                    var placement = new ContentPlacementRule { Type = "MainObject", Args = [index.ToString(CultureInfo.InvariantCulture)],
                        TargetMin = 0.05, TargetMax = 0.05, Weight = 1 };
                    (template.MandatoryContent ??= []).Add(new()
                    {
                        Name = group,
                        Content = [new() { Name = group, Sid = SpireSid, IsGuarded = false, SoloEncounter = true, Rules = [placement] }]
                    });
                    (zone.MandatoryContent ??= []).Add(group);
                }
            }
        }
    }
}
