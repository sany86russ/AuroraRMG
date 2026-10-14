using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using Olden_Era___Template_Editor.Services.GameData;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.ContentPools;

public sealed class PoolContentDraft
{
    public string Sid { get; set; } = "";
    public string WeightText { get; set; } = "1";
    public string VariantText { get; set; } = "";
    public string Biome { get; set; } = "";
    internal PoolContentItem? Original { get; init; }
}
public sealed class PoolGroupDraft
{
    public string Label { get; set; } = "";
    public string WeightText { get; set; } = "1";
    public ObservableCollection<string> IncludeLists { get; } = [];
    public ObservableCollection<PoolContentDraft> Content { get; } = [];
    internal PoolGroup? Original { get; init; }
}

public sealed class ContentPoolValidationException(string code, params object[] arguments) : ArgumentException(code)
{
    public string Code { get; } = code;
    public object[] Arguments { get; } = arguments;
}

/// <summary>Exact IDs, relative weights and actual configured variant indices. Counts live in separate RMG rules.</summary>
public static class ContentPoolComposer
{
    public static List<PoolGroupDraft> Drafts(GamePool? pool)
    {
        var result = new List<PoolGroupDraft>();
        foreach (var group in pool?.Groups ?? [])
        {
            if (group is null) throw new ContentPoolValidationException("NullGroup");
            var draft = new PoolGroupDraft { Label = (result.Count + 1).ToString(CultureInfo.InvariantCulture),
                WeightText = group.Weight.ToString("R", CultureInfo.InvariantCulture), Original = Clone(group) };
            foreach (var list in group.IncludeLists ?? []) draft.IncludeLists.Add(list);
            foreach (var item in group.Content ?? [])
            {
                if (item is null) throw new ContentPoolValidationException("NullItem");
                draft.Content.Add(new PoolContentDraft { Sid = item.Sid, WeightText = item.Weight.ToString("R", CultureInfo.InvariantCulture),
                    VariantText = item.Variant?.ToString(CultureInfo.InvariantCulture) ?? "", Biome = item.Biome ?? "", Original = Clone(item) });
            }
            result.Add(draft);
        }
        return result;
    }

    public static GamePool Build(string requestedName, IEnumerable<PoolGroupDraft> groups, GamePool? original = null,
        GameCatalog? catalog = null, CultureInfo? culture = null)
    {
        string name = requestedName.Trim();
        if (name.Length is < 1 or > 128 || name.Any(char.IsControl)) throw new ContentPoolValidationException("Name");
        if (!name.StartsWith("custom_", StringComparison.Ordinal)) name = "custom_" + name;
        var pool = original is null ? new GamePool() : Clone(original);
        pool.Name = name; pool.Groups = [];
        foreach (var draft in groups)
        {
            if (draft is null) throw new ContentPoolValidationException("NullGroup");
            var group = draft.Original is null ? new PoolGroup() : Clone(draft.Original);
            group.Weight = Weight(draft.WeightText, culture);
            group.IncludeLists = draft.IncludeLists.ToList();
            if (group.IncludeLists.Any(string.IsNullOrWhiteSpace)) throw new ContentPoolValidationException("ListName");
            if (group.IncludeLists.Distinct(StringComparer.Ordinal).Count() != group.IncludeLists.Count) throw new ContentPoolValidationException("DuplicateList");
            group.Content = [];
            foreach (var row in draft.Content)
            {
                if (row is null) throw new ContentPoolValidationException("NullItem");
                if (string.IsNullOrWhiteSpace(row.Sid) || row.Sid.Any(char.IsControl)) throw new ContentPoolValidationException("Sid");
                var item = row.Original is null ? new PoolContentItem() : Clone(row.Original);
                item.Sid = row.Sid;
                double weight = Weight(row.WeightText, culture);
                if (row.Original is null || weight != row.Original.Weight) item.Weight = weight;
                item.Biome = string.IsNullOrWhiteSpace(row.Biome) ? null : row.Biome;
                item.Variant = null;
                if (!string.IsNullOrWhiteSpace(row.VariantText))
                {
                    if (!int.TryParse(row.VariantText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int variant) || variant < -1)
                        throw new ContentPoolValidationException("Variant");
                    item.Variant = variant;
                    var known = catalog?.Content.FirstOrDefault(e => e.Sid == item.Sid && e.Kind == GameContentKind.Object);
                    // -1 is the native constructor default, not a configured reward index.
                    if (variant >= 0 && known is not null && known.RewardVariants.Count > 0 && !known.RewardVariants.Any(v => v.Index == variant))
                        throw new ContentPoolValidationException("UnknownVariant", item.Sid, variant);
                }
                group.Content.Add(item);
            }
            if (group.Content.Select(i => (i.Sid, i.Variant, i.Biome)).Distinct().Count() != group.Content.Count)
                throw new ContentPoolValidationException("DuplicateItem");
            if (group.Weight > 0 && group.IncludeLists.Count == 0 && !group.Content.Any(i => i.Weight > 0))
                throw new ContentPoolValidationException("EmptyGroup");
            pool.Groups.Add(group);
        }
        if (pool.Groups.Count == 0 || !pool.Groups.Any(g => g.Weight > 0)) throw new ContentPoolValidationException("ZeroGroups");
        if (!ValidNativeTotal(pool.Groups.Select(g => g.Weight)) || pool.Groups.Any(g => !ValidNativeTotal((g.Content ?? []).Select(i => i.Weight))))
            throw new ContentPoolValidationException("Weight");
        return pool;
    }

    private static double Weight(string text, CultureInfo? culture)
    {
        bool parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
            double.TryParse(text, NumberStyles.Float, culture ?? CultureInfo.CurrentCulture, out value);
        if (!parsed || !double.IsFinite(value) || value > float.MaxValue || !float.IsFinite((float)value) || value < 0 || value > 0 && (float)value == 0 ||
            value == 0 && text.TakeWhile(c => c is not 'e' and not 'E').Any(c => c is >= '1' and <= '9'))
            throw new ContentPoolValidationException("Weight");
        return value;
    }
    private static bool ValidNativeTotal(IEnumerable<double> weights)
    {
        double total = 0; float native = 0;
        foreach (double weight in weights)
        {
            total += weight; native += (float)weight;
            if (!double.IsFinite(total) || total > float.MaxValue || !float.IsFinite(native)) return false;
        }
        return true;
    }
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonExport.Options), JsonExport.Options)
        ?? throw new ContentPoolValidationException("Copy");
}
