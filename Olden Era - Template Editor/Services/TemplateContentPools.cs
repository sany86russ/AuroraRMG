using System.Text.Json;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services;

/// <summary>Template-local pool definitions travel with the map instead of relying on the author's PC.</summary>
public static class TemplateContentPools
{
    public static IEnumerable<string> Names(RmgTemplate template) =>
        (template.ContentPools ?? []).Select(NameOf).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!);

    public static IEnumerable<GamePool> Definitions(RmgTemplate template) =>
        (template.ContentPools ?? []).Select(pool => JsonSerializer.Deserialize<GamePool>(
            JsonSerializer.Serialize(pool, JsonExport.Options), JsonExport.Options))
        .Where(pool => pool is not null && !string.IsNullOrWhiteSpace(pool.Name)).Select(pool => pool!);

    private static string? NameOf(object pool)
    {
        if (pool is GamePool typed) return typed.Name;
        var json = pool is JsonElement element ? element : JsonSerializer.SerializeToElement(pool, JsonExport.Options);
        return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("name", out var name)
            && name.ValueKind == JsonValueKind.String ? name.GetString() : null;
    }

    public static void Add(RmgTemplate template, GamePool pool)
    {
        if (string.IsNullOrWhiteSpace(pool.Name) || Names(template).Contains(pool.Name, StringComparer.Ordinal))
            throw new InvalidOperationException(Localization.LocalizationManager.T("S.PC.Duplicate"));
        // Store the serialized definition, preserving unmodelled fields and severing library aliases.
        (template.ContentPools ??= []).Add(JsonSerializer.SerializeToElement(pool, JsonExport.Options));
    }

    public static void EmbedSelected(RmgTemplate template, IEnumerable<string> selected, IEnumerable<GamePool> library)
    {
        var defined = Names(template).ToHashSet(StringComparer.Ordinal);
        var wanted = selected.Where(n => n.StartsWith("custom_", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        foreach (var pool in library)
            if (wanted.Contains(pool.Name) && defined.Add(pool.Name)) Add(template, pool);
    }
}
