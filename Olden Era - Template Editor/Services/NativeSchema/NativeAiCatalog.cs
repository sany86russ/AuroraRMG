using System.IO.Compression;
using System.IO;
using System.Text.Json;
using Olden_Era___Template_Editor.Models.NativeSchema;

namespace Olden_Era___Template_Editor.Services.NativeSchema;

/// <summary>Reads actual installed AI identifiers; names alone make no promise about their game behavior.</summary>
public static class NativeAiCatalog
{
    public static IReadOnlyList<NativeAiProfile> Read(string coreZipPath, bool includeCampaign = false)
    {
        using var stream = new FileStream(coreZipPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        return Read(archive, includeCampaign);
    }

    /// <summary>Does not dispose the caller's already-open archive.</summary>
    public static IReadOnlyList<NativeAiProfile> Read(ZipArchive archive, bool includeCampaign = false)
    {
        ArgumentNullException.ThrowIfNull(archive);
        var rows = new List<NativeAiProfile>();
        foreach (var entry in archive.Entries.OrderBy(entry => entry.FullName, StringComparer.Ordinal))
        {
            var path = entry.FullName.Replace('\\', '/');
            if (!path.StartsWith("DB/ai_world/ai/", StringComparison.Ordinal)
                || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            var isCampaign = path.Contains("/campaign/", StringComparison.Ordinal);
            if (isCampaign && !includeCampaign) continue;
            using var stream = entry.Open();
            using var json = JsonDocument.Parse(stream);
            if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("array", out var values) || values.ValueKind != JsonValueKind.Array)
                throw new JsonException($"AI database entry '{path}' has no array.");
            foreach (var value in values.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("id", out var identifier)
                    || identifier.ValueKind != JsonValueKind.String || identifier.GetString() is not { Length: > 0 } id)
                    throw new JsonException($"AI database entry '{path}' has an invalid identifier.");
                var roles = new HashSet<string>(StringComparer.Ordinal);
                var actions = new HashSet<string>(StringComparer.Ordinal);
                if (value.TryGetProperty("strategies", out var strategies))
                {
                    if (strategies.ValueKind != JsonValueKind.Array) throw new JsonException($"AI strategies in '{path}' must be an array.");
                    foreach (var strategy in strategies.EnumerateArray())
                    {
                        if (strategy.ValueKind != JsonValueKind.Object) throw new JsonException($"AI strategy in '{path}' must be an object.");
                        if (strategy.TryGetProperty("roles", out var roleArray))
                        {
                            if (roleArray.ValueKind != JsonValueKind.Array) throw new JsonException($"AI roles in '{path}' must be an array.");
                            foreach (var role in roleArray.EnumerateArray())
                                if (role.ValueKind == JsonValueKind.String && role.GetString() is string roleId) roles.Add(roleId);
                                else throw new JsonException($"AI role in '{path}' must be a string.");
                        }
                        foreach (var property in new[] { "actionsBefore", "actionsOnSelectHero", "actionsAfter" })
                            if (strategy.TryGetProperty(property, out var actionArray))
                            {
                                if (actionArray.ValueKind != JsonValueKind.Array) throw new JsonException($"AI actions in '{path}' must be an array.");
                                foreach (var action in actionArray.EnumerateArray())
                                    if (action.ValueKind == JsonValueKind.Object && action.TryGetProperty("sid", out var sid)
                                        && sid.ValueKind == JsonValueKind.String && sid.GetString() is string actionId)
                                        actions.Add(actionId);
                                    else throw new JsonException($"AI action in '{path}' has an invalid SID.");
                            }
                    }
                }
                rows.Add(new(id, path, isCampaign, id.StartsWith("custom_ai_", StringComparison.Ordinal),
                    roles.Order(StringComparer.Ordinal).ToArray(), actions.Order(StringComparer.Ordinal).ToArray()));
            }
        }
        var duplicate = rows.GroupBy(row => row.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new JsonException($"Duplicate AI identifier '{duplicate.Key}'.");
        return rows;
    }
}
