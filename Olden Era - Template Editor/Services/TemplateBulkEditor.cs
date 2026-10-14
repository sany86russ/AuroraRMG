using System.Text.Json.Nodes;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services;

/// <summary>Atomic edits of selected zones keep unselected variants and every unknown JSON field.</summary>
public static class TemplateBulkEditor
{
    public static readonly string[] ZoneProperties = ["guardReactionDistribution", "randomHireEnableWeeklyUnitIncrement",
        "randomHireEnableModificatorWeeklyUnitIncrement", "randomHireInitialUnitIncrement", "guardMultiplier",
        "guardWeeklyIncrement", "diplomacyModifier", "guardedContentValue", "unguardedContentValue", "resourcesValue",
        "guardedContentValuePerArea", "unguardedContentValuePerArea", "resourcesValuePerArea", "mandatoryContent", "contentCountLimits"];

    public static RmgTemplate Apply(RmgTemplate source, int variantIndex, IReadOnlyCollection<string> names, string property, string jsonValue)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(names);
        if (!ZoneProperties.Contains(property, StringComparer.Ordinal)) throw new ArgumentException("Unsupported bulk property.", nameof(property));
        if (names.Count == 0 || names.Distinct(StringComparer.Ordinal).Count() != names.Count) throw new ArgumentException("Select distinct zones.", nameof(names));
        var draft = new TemplatePropertiesDraft(source);
        var candidate = draft.Snapshot();
        var variants = candidate["variants"] as JsonArray ?? throw new ArgumentException("The template has no variants.");
        if (variantIndex < 0 || variantIndex >= variants.Count) throw new ArgumentOutOfRangeException(nameof(variantIndex));
        var zones = variants[variantIndex]?["zones"] as JsonArray ?? throw new ArgumentException("The variant has no zones.");
        var wanted = names.ToHashSet(StringComparer.Ordinal);
        var targets = zones.OfType<JsonObject>().Where(z => wanted.Contains(z["name"]?.GetValue<string>() ?? "")).ToArray();
        if (targets.Length != names.Count) throw new ArgumentException("Some selected zone names are missing or duplicated.");
        var value = JsonNode.Parse(jsonValue);
        foreach (var zone in targets)
        {
            if (value is null) zone.Remove(property);
            else zone[property] = value.DeepClone();
        }
        return draft.Commit(candidate, out _);
    }
}
