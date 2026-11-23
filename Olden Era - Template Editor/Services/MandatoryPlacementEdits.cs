using System.Globalization;
using System.Text.Json;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor.Services;

/// <summary>Mandatory groups are document-wide, while placement arguments refer to their consuming zone.</summary>
internal static class MandatoryPlacementEdits
{
    internal readonly record struct RuleChange(bool Remove, string? Argument)
    {
        public bool ChangesRule => Remove || Argument is not null;
    }

    private sealed record Edit(Zone Zone, MandatoryContentGroup Original, MandatoryContentGroup Replacement);

    internal static RuleChange ReindexMainObject(ContentPlacementRule rule, int deletedIndex)
    {
        if (rule is not { Type: "MainObject", Args.Count: > 0 }
            || !int.TryParse(rule.Args[0], NumberStyles.None, CultureInfo.InvariantCulture, out int index))
            return default;
        if (index == deletedIndex) return new(true, null);
        return index > deletedIndex ? new(false, (index - 1).ToString(CultureInfo.InvariantCulture)) : default;
    }

    /// <summary>Resolves all affected groups before the caller changes graph or content state.</summary>
    internal static Action Prepare(RmgTemplate? template, IEnumerable<Zone> zones,
        Func<Zone, ContentPlacementRule, RuleChange> decide)
    {
        if (template?.MandatoryContent is not { Count: > 0 } definitions) return static () => { };
        var byName = definitions.Where(group => group is not null).ToLookup(group => group.Name, StringComparer.Ordinal);
        var consumers = (template.Variants ?? []).Where(variant => variant is not null)
            .SelectMany(variant => variant.Zones ?? []).Where(zone => zone is not null).Distinct()
            .SelectMany(zone => (zone.MandatoryContent ?? []).Distinct(StringComparer.Ordinal))
            .Where(name => !string.IsNullOrEmpty(name)).ToLookup(name => name, StringComparer.Ordinal);
        var reserved = byName.Select(group => group.Key).Concat(consumers.Select(group => group.Key)).ToHashSet(StringComparer.Ordinal);
        var plan = new List<Edit>();
        foreach (var zone in zones)
        foreach (string name in (zone.MandatoryContent ?? []).Where(name => !string.IsNullOrEmpty(name)).Distinct(StringComparer.Ordinal))
        {
            var matching = byName[name];
            var changed = matching.FirstOrDefault(group => (group.Content ?? []).Any(item =>
                item?.Rules?.Any(rule => rule is not null && decide(zone, rule).ChangesRule) == true));
            if (changed is null) continue;
            if (matching.Skip(1).Any())
                throw new InvalidOperationException(LocalizationManager.T("S.EC.AmbiguousMandatoryPlacement", name, zone.Name));

            // Only one zone owns a uniquely consumed definition. Otherwise keep the original
            // available to every other zone, including zones in other variants.
            var copy = JsonSerializer.Deserialize<MandatoryContentGroup>(
                JsonSerializer.Serialize(changed, JsonExport.Options), JsonExport.Options)!;
            if (consumers[name].Skip(1).Any())
            {
                string stem = name + "__aurora";
                copy.Name = stem;
                for (int suffix = 2; !reserved.Add(copy.Name); suffix++)
                    copy.Name = stem + "_" + suffix.ToString(CultureInfo.InvariantCulture);
            }
            foreach (var item in copy.Content ?? [])
            {
                if (item?.Rules is not { Count: > 0 } rules) continue;
                rules.RemoveAll(rule =>
                {
                    if (rule is null) return false;
                    var change = decide(zone, rule);
                    if (change.Argument is { } replacement) rule.Args![0] = replacement;
                    return change.Remove;
                });
                if (rules.Count == 0) item.Rules = null;
            }
            plan.Add(new(zone, changed, copy));
        }
        return () =>
        {
            foreach (var edit in plan)
            {
                if (edit.Original.Name == edit.Replacement.Name)
                    definitions[definitions.IndexOf(edit.Original)] = edit.Replacement;
                else
                {
                    definitions.Add(edit.Replacement);
                    var assigned = edit.Zone.MandatoryContent!;
                    for (int i = 0; i < assigned.Count; i++)
                        if (assigned[i] == edit.Original.Name) assigned[i] = edit.Replacement.Name;
                }
            }
        };
    }
}
