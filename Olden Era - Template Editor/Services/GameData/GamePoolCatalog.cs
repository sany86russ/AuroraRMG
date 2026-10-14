using System.IO.Compression;
using System.Text.Json;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.GameData;

public enum PoolReferenceOrigin { TemplateDefinition, LocalDefinition, InstalledGame, Missing, GameUnavailable, InvalidTemplateDefinition }
public sealed record PoolReferenceResolution(string Sid, PoolReferenceOrigin Origin, GamePool? Definition, string? Error = null)
{
    public bool IsResolved => Definition is not null;
}
public sealed record ContentListReferenceResolution(string Sid, PoolReferenceOrigin Origin, ContentListEntry? Definition, string? Error = null)
{
    public bool IsResolved => Definition is not null;
}

public sealed class GamePoolCatalog
{
    public GameInstallation? Source { get; init; }
    public List<GamePool> Pools { get; init; } = [];
    public List<ContentListEntry> ContentLists { get; init; } = [];
    public List<string> Warnings { get; init; } = [];

    public static GamePoolCatalog Read(ZipArchive archive, GameInstallation? source = null)
    {
        var result = new GamePoolCatalog { Source = source };
        var poolNames = new HashSet<string>(StringComparer.Ordinal);
        var listNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            var path = entry.FullName.Replace('\\', '/');
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                if (path.StartsWith("generator/content_pools/", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = entry.Open();
                    foreach (var pool in JsonSerializer.Deserialize<List<GamePool>>(stream, ReadOptions) ?? [])
                        if (pool is not null && !string.IsNullOrWhiteSpace(pool.Name))
                        { if (poolNames.Add(pool.Name)) result.Pools.Add(pool); else result.Warnings.Add("Duplicate game pool: " + pool.Name); }
                }
                else if (path.StartsWith("generator/content_lists/", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = entry.Open();
                    foreach (var list in JsonSerializer.Deserialize<List<ContentListEntry>>(stream, ReadOptions) ?? [])
                        if (list is not null && !string.IsNullOrWhiteSpace(list.Name))
                        { if (listNames.Add(list.Name)) result.ContentLists.Add(list); else result.Warnings.Add("Duplicate game content list: " + list.Name); }
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
            { result.Warnings.Add($"Could not load {path}: {ex.Message}"); }
        }
        return result;
    }

    public PoolReferenceResolution ResolvePool(string sid, RmgTemplate? template = null, IEnumerable<GamePool>? localPools = null)
    {
        var embedded = NamedDefinition<GamePool>(template?.ContentPools, sid);
        if (embedded.Found) return embedded.Value is not null ? new(sid, PoolReferenceOrigin.TemplateDefinition, embedded.Value) :
            new(sid, PoolReferenceOrigin.InvalidTemplateDefinition, null, embedded.Error);
        var local = localPools?.FirstOrDefault(p => p.Name == sid);
        if (local is not null) return new(sid, PoolReferenceOrigin.LocalDefinition, local);
        var installed = Pools.FirstOrDefault(p => p.Name == sid);
        return installed is not null ? new(sid, PoolReferenceOrigin.InstalledGame, installed) :
            new(sid, Source is null ? PoolReferenceOrigin.GameUnavailable : PoolReferenceOrigin.Missing, null);
    }

    public ContentListEntry? ResolveList(string sid, RmgTemplate? template = null)
        => ResolveListReference(sid, template).Definition;

    public ContentListReferenceResolution ResolveListReference(string sid, RmgTemplate? template = null)
    {
        var embedded = NamedDefinition<ContentListEntry>(template?.ContentLists, sid);
        if (embedded.Found) return new(sid, embedded.Value is null ? PoolReferenceOrigin.InvalidTemplateDefinition : PoolReferenceOrigin.TemplateDefinition, embedded.Value, embedded.Error);
        var installed = ContentLists.FirstOrDefault(l => l.Name == sid);
        return new(sid, installed is null ? Source is null ? PoolReferenceOrigin.GameUnavailable : PoolReferenceOrigin.Missing : PoolReferenceOrigin.InstalledGame, installed);
    }

    public IReadOnlyList<string> ValidateReferences(RmgTemplate template, IEnumerable<GamePool>? localPools = null)
    {
        var problems = new List<string>();
        var refs = (template.Variants ?? []).SelectMany(v => v.Zones ?? []).SelectMany(z =>
            (z.GuardedContentPool ?? []).Concat(z.UnguardedContentPool ?? []).Concat(z.ResourcesContentPool ?? []));
        foreach (var sid in refs.Distinct(StringComparer.Ordinal))
        {
            var resolution = ResolvePool(sid, template, localPools);
            if (!resolution.IsResolved)
            {
                problems.Add(resolution.Origin == PoolReferenceOrigin.InvalidTemplateDefinition
                    ? $"Embedded pool {sid} is invalid: {resolution.Error}"
                    : resolution.Origin == PoolReferenceOrigin.GameUnavailable
                    ? $"Pool {sid} cannot be checked without the installed game or an embedded definition."
                    : $"Pool {sid} is missing from this game version and the template; no replacement was guessed.");
                continue;
            }
            foreach (var listSid in (resolution.Definition!.Groups ?? []).Where(g => g is not null).SelectMany(g => g.IncludeLists ?? []).Distinct(StringComparer.Ordinal))
                if (ResolveList(listSid, template) is null)
                    problems.Add($"Pool {sid} references missing content list {listSid}.");
        }
        foreach (var pool in ReadDefinitions<GamePool>(template.ContentPools))
            foreach (var sid in (pool.Groups ?? []).Where(g => g is not null).SelectMany(g => g.IncludeLists ?? []).Distinct(StringComparer.Ordinal))
                if (ResolveList(sid, template) is null) problems.Add($"Embedded pool {pool.Name} references missing content list {sid}.");
        return problems.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Explicit per-template embedding from the user's own install, never app resource bundling.</summary>
    public void EmbedSelected(RmgTemplate template, IEnumerable<string> selected, IEnumerable<GamePool>? localPools = null)
    {
        var pools = ReadDefinitions<GamePool>(template.ContentPools).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var lists = ReadDefinitions<ContentListEntry>(template.ContentLists).Select(l => l.Name).ToHashSet(StringComparer.Ordinal);
        // Preflight the entire transaction before mutating the template.
        var resolved = selected.Distinct(StringComparer.Ordinal).Select(sid => ResolvePool(sid, template, localPools)).ToArray();
        foreach (var r in resolved) if (!r.IsResolved) throw new InvalidOperationException("Cannot embed missing pool: " + r.Sid);
        var dependencies = resolved.SelectMany(r => (r.Definition!.Groups ?? []).Where(g => g is not null).SelectMany(g => g.IncludeLists ?? [])).Distinct(StringComparer.Ordinal)
            .Select(sid => (Sid: sid, Definition: ResolveList(sid, template))).ToArray();
        foreach (var d in dependencies) if (d.Definition is null) throw new InvalidOperationException("Cannot embed missing content list: " + d.Sid);
        foreach (var r in resolved)
            if (pools.Add(r.Sid)) (template.ContentPools ??= []).Add(JsonSerializer.SerializeToElement(r.Definition, ReadOptions));
        foreach (var d in dependencies)
            if (lists.Add(d.Sid)) (template.ContentLists ??= []).Add(JsonSerializer.SerializeToElement(d.Definition, ReadOptions));
    }

    private static IEnumerable<T> ReadDefinitions<T>(IEnumerable<object>? definitions)
    {
        foreach (var definition in definitions ?? [])
        {
            if (definition is T typed) { yield return typed; continue; }
            T? parsed;
            try
            {
                var json = definition is JsonElement element ? element : JsonSerializer.SerializeToElement(definition, ReadOptions);
                parsed = json.Deserialize<T>(ReadOptions);
            }
            catch (JsonException) { continue; }
            if (parsed is not null) yield return parsed;
        }
    }
    private static (bool Found, T? Value, string? Error) NamedDefinition<T>(IEnumerable<object>? definitions, string sid)
    {
        foreach (var definition in definitions ?? [])
        {
            var json = definition is JsonElement element ? element : JsonSerializer.SerializeToElement(definition, ReadOptions);
            if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || name.GetString() != sid) continue;
            try { return (true, json.Deserialize<T>(ReadOptions), null); }
            catch (JsonException ex) { return (true, default, ex.Message); }
        }
        return (false, default, null);
    }
    internal static readonly JsonSerializerOptions ReadOptions = new(JsonExport.Options)
    { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
}
