using System.Globalization;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.Generation;

internal static class ChampionSpirePlacement
{
    public const string SelectedRule = "Selected";
    public const string SpireSid = "gladiator_spire";

    public static bool HasLinkedSpire(RmgTemplate template, Variant variant) =>
        LinkedSpireZones(template, variant).Count > 0;

    private static HashSet<string> LinkedSpireZones(RmgTemplate template, Variant variant)
    {
        var groups = (template.MandatoryContent ?? []).Where(group => group?.Content?.Any(item => item?.Sid == SpireSid) == true)
            .Select(group => group.Name).ToHashSet(StringComparer.Ordinal);
        return (variant.Zones ?? []).Where(zone => zone?.MandatoryContent?.Any(groups.Contains) == true)
            .Select(zone => zone.Name).ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> ZonesWithSpireAccess(Variant variant, HashSet<string> spireZones)
    {
        if (spireZones.Count == 0) return spireZones;
        var zoneNames = (variant.Zones ?? []).Where(zone => zone is not null).Select(zone => zone.Name).ToHashSet(StringComparer.Ordinal);
        // A final-battle link does not establish that either player can explore through it
        // before choosing a champion. Proximity is already excluded by AllowsTravel.
        var adjacent = (variant.Connections ?? []).Where(connection => connection is not null
                && connection.ConnectionType != "GladiatorArena" && ConnectionRules.AllowsTravel(connection)
                && zoneNames.Contains(connection.From) && zoneNames.Contains(connection.To))
            .SelectMany(connection => new[] { (From: connection.From, To: connection.To), (From: connection.To, To: connection.From) })
            .ToLookup(edge => edge.From, edge => edge.To, StringComparer.Ordinal);
        var queue = new Queue<string>(spireZones);
        while (queue.TryDequeue(out var current))
            foreach (var neighbour in adjacent[current])
                if (spireZones.Add(neighbour)) queue.Enqueue(neighbour);
        return spireZones;
    }

    public static IReadOnlyList<string> StartsWithoutSpireAccess(RmgTemplate template, Variant variant)
    {
        var accessible = ZonesWithSpireAccess(variant, LinkedSpireZones(template, variant));
        return (variant.Zones ?? []).Where(zone => zone is not null && !accessible.Contains(zone.Name)
                && zone.MainObjects?.Any(IsPlayerStart) == true)
            .Select(zone => zone.Name).ToArray();
    }

    private static bool IsPlayerStart(MainObject? obj) => obj is { Type: "Spawn", Spawn: { } player }
        && KnownValues.SpawnPlayers.Contains(player, StringComparer.Ordinal);

    /// <summary>Preserves authored spires and fills starts that lack access in isolated tournament regions.</summary>
    public static void AddToStarts(RmgTemplate template)
    {
        bool tournament = template.GameRules?.WinConditions?.Tournament == true;
        var names = (template.MandatoryContent ?? []).Where(group => group is not null)
            .Select(group => group.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var (variant, variantIndex) in (template.Variants ?? []).Select((variant, index) => (variant, index)))
        {
            if (variant is null) continue;
            var accessible = LinkedSpireZones(template, variant);
            if (tournament) ZonesWithSpireAccess(variant, accessible);
            else if (accessible.Count != 0) continue;
            foreach (var zone in variant.Zones ?? [])
            {
                if (zone?.MainObjects is not { } objects || accessible.Contains(zone.Name)) continue;
                for (int index = 0; index < objects.Count; index++)
                {
                    if (!IsPlayerStart(objects[index])) continue;
                    string player = objects[index].Spawn!;
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
