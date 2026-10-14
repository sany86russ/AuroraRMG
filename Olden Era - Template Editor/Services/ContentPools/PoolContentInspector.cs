using Olden_Era___Template_Editor.Services.GameData;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.ContentPools;

public sealed record PoolDependencyInfo(string Name, PoolReferenceOrigin Origin, int ItemCount, string? Error);
public sealed record PoolGroupInspection(int Index, double Weight, int DirectItemCount, IReadOnlyList<PoolDependencyInfo> Dependencies);
public sealed record PoolContentInspection(IReadOnlyList<PoolItemInfo> Items, IReadOnlyList<PoolGroupInspection> Groups, IReadOnlyList<string> Warnings);

/// <summary>Inspect exact list resolution and configured weights without simulating game placement.</summary>
public static class PoolContentInspector
{
    public static PoolContentInspection Inspect(GamePool pool, GamePoolCatalog catalog, RmgTemplate? template = null)
    {
        ArgumentNullException.ThrowIfNull(pool); ArgumentNullException.ThrowIfNull(catalog);
        var items = new List<PoolItemInfo>(); var groups = new List<PoolGroupInspection>(); var warnings = new List<string>();
        var sourceGroups = pool.Groups ?? [];
        double? groupTotal = Total(sourceGroups.Select(g => g?.Weight ?? double.NaN));
        if (groupTotal is null && sourceGroups.Count > 0) warnings.Add("Pool group weights are invalid or have no positive total; relative shares are unavailable.");
        for (int index = 0; index < sourceGroups.Count; index++)
        {
            var group = sourceGroups[index];
            if (group is null) { warnings.Add($"Pool group {index + 1} is null."); continue; }
            var dependencies = new List<PoolDependencyInfo>();
            foreach (var listName in group.IncludeLists ?? [])
            {
                if (listName is null) { warnings.Add($"Pool group {index + 1} contains a null list identifier."); continue; }
                var resolved = catalog.ResolveListReference(listName, template);
                var content = resolved.Definition?.Content;
                dependencies.Add(new(listName, resolved.Origin, content?.Count ?? 0, resolved.Error));
                if (content is null)
                {
                    items.Add(new() { GroupIndex = index + 1, ListName = listName, Sid = "", GroupWeight = group.Weight, IsMissing = true,
                        RelativeGroupShare = Share(group.Weight, groupTotal) });
                    warnings.Add($"Content list '{listName}' cannot be resolved ({resolved.Origin}).");
                }
                else AddContent(content, listName, index + 1, group.Weight, groupTotal, items, warnings);
            }
            if (group.Content is { } direct) AddContent(direct, "", index + 1, group.Weight, groupTotal, items, warnings);
            groups.Add(new(index + 1, group.Weight, group.Content?.Count ?? 0, dependencies));
        }
        return new(items, groups, warnings);
    }

    private static void AddContent(IReadOnlyList<PoolContentItem> content, string listName, int groupIndex, double groupWeight,
        double? groupTotal, List<PoolItemInfo> rows, List<string> warnings)
    {
        double? total = Total(content.Select(item => item?.Weight ?? double.NaN));
        if (total is null && content.Count > 0) warnings.Add($"Pool group {groupIndex}, list '{listName}' has invalid content weights; relative shares are unavailable.");
        foreach (var item in content)
        {
            if (item is null) { warnings.Add($"Pool group {groupIndex}, list '{listName}' contains a null content item."); continue; }
            rows.Add(new() { GroupIndex = groupIndex, ListName = listName, Sid = item.Sid, Weight = item.Weight, Variant = item.Variant,
                Biome = item.Biome, GroupWeight = groupWeight, RelativeGroupShare = Share(groupWeight, groupTotal), RelativeWithinListShare = Share(item.Weight, total) });
        }
    }
    private static double? Total(IEnumerable<double> values)
    {
        double total = 0;
        foreach (double value in values)
        { if (!double.IsFinite(value) || value < 0 || value > float.MaxValue) return null; total += value; if (!double.IsFinite(total) || total > float.MaxValue) return null; }
        return total > 0 ? total : null;
    }
    private static double? Share(double value, double? total) => total.HasValue ? value / total.Value : null;
}
