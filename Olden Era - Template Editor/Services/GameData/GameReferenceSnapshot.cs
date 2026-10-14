using System.IO.Compression;
using System.Text.Json;
using Olden_Era___Template_Editor.Services.Analysis;
using Olden_Era___Template_Editor.Services.NativeSchema;

namespace Olden_Era___Template_Editor.Services.GameData;

/// <summary>Serializable exact identifiers; schema-local definitions remain separate from global .rmg references.</summary>
public sealed class GameReferenceSnapshot
{
    public List<string> Pools { get; set; } = [];
    public List<string> Lists { get; set; } = [];
    public List<string> Layouts { get; set; } = [];
    public List<string> Objects { get; set; } = [];
    public List<string> NativeLayouts { get; set; } = [];
    public List<string> NativeMandatory { get; set; } = [];
    public List<string> NativeCountLimits { get; set; } = [];
    public List<string>? CustomAI { get; set; }
    public string AiEvidence { get; set; } = "AI identifiers have not been read; no behavior has been verified.";
    public bool CoreReadComplete { get; set; }
    public TemplateReferenceCatalog ToReferenceCatalog(GameInstallation? source, bool includeNativeSchemaDefinitions = false,
        IReadOnlyDictionary<string, IReadOnlySet<int>>? objectRewardVariantIndices = null) => new()
    {
        Pools = source is null ? null : Pools.ToHashSet(StringComparer.Ordinal),
        Lists = source is null ? null : Lists.ToHashSet(StringComparer.Ordinal),
        Layouts = source is null ? null : Layouts.Concat(includeNativeSchemaDefinitions ? NativeLayouts : []).ToHashSet(StringComparer.Ordinal),
        Mandatory = source is null ? null : (includeNativeSchemaDefinitions ? NativeMandatory.AsEnumerable() : Enumerable.Empty<string>()).ToHashSet(StringComparer.Ordinal),
        CountLimits = source is null ? null : (includeNativeSchemaDefinitions ? NativeCountLimits.AsEnumerable() : Enumerable.Empty<string>()).ToHashSet(StringComparer.Ordinal),
        Objects = source is null ? null : Objects.ToHashSet(StringComparer.Ordinal),
        CustomAI = source is null ? null : CustomAI?.ToHashSet(StringComparer.Ordinal),
        ObjectRewardVariantIndices = source is null ? null : objectRewardVariantIndices,
        IsComplete = source is not null && CoreReadComplete,
        Source = source is null ? "No installed-game reference data" : $"Core.zip {source.SourceFingerprint}; build {source.SteamBuildId ?? "unknown"}"
    };
}

public static class GameReferenceSnapshotBuilder
{
    private static readonly JsonDocumentOptions Options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
    public static GameReferenceSnapshot Build(ZipArchive core, GameInstallation? source = null, ICollection<string>? warnings = null)
    {
        var snapshot = new GameReferenceSnapshot { CoreReadComplete = true };
        var pools = new HashSet<string>(StringComparer.Ordinal);
        var lists = new HashSet<string>(StringComparer.Ordinal);
        var layouts = new HashSet<string>(StringComparer.Ordinal);
        var objects = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in core.Entries)
        {
            string path = entry.FullName.Replace('\\', '/');
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            HashSet<string>? target = null; string property = "name";
            if (path.StartsWith("generator/content_pools/", StringComparison.Ordinal)) target = pools;
            else if (path.StartsWith("generator/content_lists/", StringComparison.Ordinal)) target = lists;
            else if (path.StartsWith("generator/zone_layouts/", StringComparison.Ordinal)) target = layouts;
            else if (path.StartsWith("DB/map/objects/", StringComparison.Ordinal) || path.StartsWith("DB/objects_logic/", StringComparison.Ordinal)) { target = objects; property = "id"; }
            else if (path == "generator/generator_config.json") target = objects;
            if (target is null) continue;
            try
            {
                using var stream = entry.Open(); using var doc = JsonDocument.Parse(stream, Options);
                if (path == "generator/generator_config.json")
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("metaObjects", out var meta) && meta.ValueKind == JsonValueKind.Array) AddRows(meta, "sid", objects);
                    else { snapshot.CoreReadComplete = false; warnings?.Add("Unrecognized generator meta-object reference shape: " + path); }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Array) AddRows(doc.RootElement, property, target);
                else if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("array", out var array) && array.ValueKind == JsonValueKind.Array) AddRows(array, property, target);
                else { snapshot.CoreReadComplete = false; warnings?.Add("Unrecognized reference data shape: " + path); }
            }
            catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
            { snapshot.CoreReadComplete = false; warnings?.Add($"Reference data could not be read from {path}: {ex.Message}"); }
        }
        snapshot.Pools = pools.Order(StringComparer.Ordinal).ToList(); snapshot.Lists = lists.Order(StringComparer.Ordinal).ToList();
        snapshot.Layouts = layouts.Order(StringComparer.Ordinal).ToList(); snapshot.Objects = objects.Order(StringComparer.Ordinal).ToList();
        try
        {
            bool hasAiDomain = core.Entries.Any(entry =>
            {
                string path = entry.FullName.Replace('\\', '/');
                return path.StartsWith("DB/ai_world/ai/", StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains("/campaign/", StringComparison.Ordinal);
            });
            if (!hasAiDomain)
            {
                snapshot.AiEvidence = "No recognized non-campaign AI database is present; accepted AI identifiers are unverified.";
                warnings?.Add(snapshot.AiEvidence);
            }
            else
            {
                var ai = NativeAiCatalog.Read(core, includeCampaign: false);
                snapshot.CustomAI = ai.Select(row => row.Id).Order(StringComparer.Ordinal).ToList();
                snapshot.AiEvidence = "Exact non-campaign AI identifiers read from Core.zip. Roles and action names do not verify passive or cooperative behavior.";
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
        {
            snapshot.AiEvidence = "AI identifiers are unavailable: " + ex.Message;
            warnings?.Add(snapshot.AiEvidence);
        }
        if (source is not null && File.Exists(source.SchemaPath))
            try
            {
                using var stream = File.OpenRead(source.SchemaPath); using var doc = JsonDocument.Parse(stream, Options);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    snapshot.NativeLayouts = Names(doc.RootElement, "zoneLayouts");
                    snapshot.NativeMandatory = Names(doc.RootElement, "mandatoryContent");
                    snapshot.NativeCountLimits = Names(doc.RootElement, "contentCountLimits");
                }
                else warnings?.Add("Native schema has an unrecognized root shape; its local references are unavailable.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            { warnings?.Add("Native schema references could not be read: " + ex.Message); }
        return snapshot;
    }
    private static List<string> Names(JsonElement root, string property)
    { var names = new HashSet<string>(StringComparer.Ordinal); if (root.TryGetProperty(property, out var array)) AddRows(array, "name", names); return names.Order(StringComparer.Ordinal).ToList(); }
    private static void AddRows(JsonElement array, string property, HashSet<string> target)
    {
        if (array.ValueKind != JsonValueKind.Array) return;
        foreach (var row in array.EnumerateArray())
            if (row.ValueKind == JsonValueKind.Object && row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } sid)
                target.Add(sid); // deliberately preserve trailing spaces and case
    }
}
