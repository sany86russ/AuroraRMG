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
    public List<string> Skills { get; set; } = [];
    public bool SkillCompleteness { get; set; }
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
        Skills = source is null || Skills.Count == 0 && !SkillCompleteness ? null : Skills.ToHashSet(StringComparer.Ordinal),
        SkillCompleteness = source is not null && SkillCompleteness,
        ObjectRewardVariantIndices = source is null ? null : objectRewardVariantIndices,
        IsComplete = source is not null && CoreReadComplete,
        Source = source is null ? "No installed-game reference data" : $"Core.zip {source.SourceFingerprint}; build {source.SteamBuildId ?? "unknown"}"
    };
}

public static class GameReferenceSnapshotBuilder
{
    internal const string SkillsPath = "DB/heroes_skills/skills/skills.json";
    private static readonly JsonDocumentOptions Options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
    public static GameReferenceSnapshot Build(ZipArchive core, GameInstallation? source = null, ICollection<string>? warnings = null)
    {
        var snapshot = new GameReferenceSnapshot { CoreReadComplete = true };
        var pools = new HashSet<string>(StringComparer.Ordinal);
        var lists = new HashSet<string>(StringComparer.Ordinal);
        var layouts = new HashSet<string>(StringComparer.Ordinal);
        var mapObjects = new HashSet<string>(StringComparer.Ordinal);
        var objectLogic = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        const string poolsPrefix = "generator/content_pools/";
        const string listsPrefix = "generator/content_lists/";
        const string layoutsPrefix = "generator/zone_layouts/";
        const string mapObjectsPrefix = "DB/map/objects/";
        const string objectLogicPrefix = "DB/objects_logic/";
        const string generatorConfig = "generator/generator_config.json";
        if (core.GetEntry(SkillsPath) is { } skillsEntry) ReadSkills(skillsEntry, snapshot, warnings);
        foreach (var entry in core.Entries)
        {
            string path = entry.FullName.Replace('\\', '/');
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            HashSet<string>? target = null; string property = "name";
            if (path.StartsWith(poolsPrefix, StringComparison.Ordinal)) target = pools;
            else if (path.StartsWith(listsPrefix, StringComparison.Ordinal)) target = lists;
            else if (path.StartsWith(layoutsPrefix, StringComparison.Ordinal)) target = layouts;
            else if (path.StartsWith(mapObjectsPrefix, StringComparison.Ordinal)) { target = mapObjects; property = "id"; }
            else if (path.StartsWith(objectLogicPrefix, StringComparison.Ordinal)) { target = objectLogic; property = "id"; }
            else if (path == generatorConfig) target = aliases;
            if (target is null) continue;
            try
            {
                using var stream = entry.Open(); using var doc = JsonDocument.Parse(stream, Options);
                if (path == generatorConfig)
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("metaObjects", out var meta) && meta.ValueKind == JsonValueKind.Array) AddRows(meta, "sid", aliases);
                    else { snapshot.CoreReadComplete = false; warnings?.Add("Unrecognized generator meta-object reference shape: " + path); }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Array) AddRows(doc.RootElement, property, target);
                else if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("array", out var array) && array.ValueKind == JsonValueKind.Array) AddRows(array, property, target);
                else { snapshot.CoreReadComplete = false; warnings?.Add("Unrecognized reference data shape: " + path); }
            }
            catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
            { snapshot.CoreReadComplete = false; warnings?.Add($"Reference data could not be read from {path}: {ex.Message}"); }
        }
        (string Path, HashSet<string> Ids)[] required =
        [
            (poolsPrefix, pools), (listsPrefix, lists), (layoutsPrefix, layouts), (mapObjectsPrefix, mapObjects),
            (objectLogicPrefix, objectLogic), (generatorConfig, aliases)
        ];
        foreach (var domain in required)
        {
            if (domain.Ids.Count > 0) continue;
            snapshot.CoreReadComplete = false;
            warnings?.Add("The reference domain is missing or empty: " + domain.Path);
        }
        snapshot.Pools = pools.Order(StringComparer.Ordinal).ToList(); snapshot.Lists = lists.Order(StringComparer.Ordinal).ToList();
        snapshot.Layouts = layouts.Order(StringComparer.Ordinal).ToList();
        snapshot.Objects = mapObjects.Union(objectLogic).Union(aliases).Order(StringComparer.Ordinal).ToList();
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
        if (source is not null && (source.SchemaFingerprint.Length > 0 || File.Exists(source.SchemaPath)))
            try
            {
                using var stream = GameInstallationService.OpenVerifiedDataFile(source.SchemaPath, source.SchemaFingerprint);
                using var doc = JsonDocument.Parse(stream, Options);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    snapshot.NativeLayouts = Names(doc.RootElement, "zoneLayouts");
                    snapshot.NativeMandatory = Names(doc.RootElement, "mandatoryContent");
                    snapshot.NativeCountLimits = Names(doc.RootElement, "contentCountLimits");
                }
                else warnings?.Add("Native schema has an unrecognized root shape; its local references are unavailable.");
            }
            catch (Exception ex) when (ex is not InstalledGameDataChangedException && ex is (IOException or UnauthorizedAccessException or JsonException))
            { warnings?.Add("Native schema references could not be read: " + ex.Message); }
        return snapshot;
    }
    private static void ReadSkills(ZipArchiveEntry entry, GameReferenceSnapshot snapshot, ICollection<string>? warnings)
    {
        try
        {
            using var stream = entry.Open();
            using var document = JsonDocument.Parse(stream, Options);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("array", out var rows) && rows.ValueKind == JsonValueKind.Array)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                AddRows(rows, "id", ids);
                snapshot.Skills = ids.Order(StringComparer.Ordinal).ToList();
                snapshot.SkillCompleteness = ids.Count > 0 && ids.Count == rows.GetArrayLength();
            }
            if (!snapshot.SkillCompleteness)
                warnings?.Add("The normal-game skill database is empty, incomplete or unrecognized: " + entry.FullName);
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
        {
            warnings?.Add($"Skill identifiers could not be read from {entry.FullName}: {ex.Message}");
        }
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
