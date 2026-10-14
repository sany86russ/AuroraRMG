using System.IO.Compression;
using System.Text.Json;

namespace Olden_Era___Template_Editor.Services.GameData;

public enum GameContentKind { Object, Spell, Artifact }

public sealed class GameRewardVariant
{
    /// <summary>Actual zero-based configuration variant, not an invented object level.</summary>
    public int Index { get; set; }
    public double? RollChance { get; set; }
    public double? Value { get; set; }
    public List<string> RewardTypes { get; set; } = [];
    public List<string> RewardParameters { get; set; } = [];
    public List<GameRewardSpell> Spells { get; set; } = [];
}

public sealed record GameRewardSpell(string Sid, string NameRu, string NameEn, int? Rank);

public sealed class GameContentEntry
{
    public string Sid { get; set; } = "";
    public GameContentKind Kind { get; set; }
    public string NameRu { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Category { get; set; } = "";
    public string Faction { get; set; } = "";
    public int? Level { get; set; }
    public string SourceEntry { get; set; } = "";
    public bool HasGeneratorConfig { get; set; }
    public bool ReferencedByRmgContent { get; set; }
    // No parser-only observation can set this to true. It needs a separate game-validation receipt.
    public bool PlacementVerified { get; set; }
    public List<GameRewardVariant> RewardVariants { get; set; } = [];
    public List<string> GrantedSpells { get; set; } = [];
    public string DisplayName(string language) => language == "russian" ? NameRu : NameEn;
    public bool Matches(string? text) => string.IsNullOrWhiteSpace(text) ||
        new[] { Sid, NameRu, NameEn, Category, Faction, Level?.ToString() ?? "" }
            .Any(value => value.Contains(text, StringComparison.OrdinalIgnoreCase)) || RewardVariants.SelectMany(v => v.Spells)
            .Any(s => new[] { s.Sid, s.NameRu, s.NameEn, s.Rank?.ToString() ?? "" }.Any(v => v.Contains(text!, StringComparison.OrdinalIgnoreCase)));
}

/// <summary>Current installed configuration IDs, names and reward variants. No bundled game resources.</summary>
public static class GameContentCatalogBuilder
{
    private static readonly JsonDocumentOptions Options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    public static List<GameContentEntry> Build(ZipArchive core, ICollection<string>? warnings = null)
    {
        var namesRu = ReadNames(core, "russian", warnings);
        var namesEn = ReadNames(core, "english", warnings);
        var rmgSids = ReadRmgSids(core, warnings);
        var entries = new Dictionary<(GameContentKind Kind, string Sid), GameContentEntry>();
        foreach (var entry in core.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            string path = entry.FullName.Replace('\\', '/');
            GameContentKind kind;
            if (path.StartsWith("DB/magics/", StringComparison.Ordinal)) kind = GameContentKind.Spell;
            else if (path.StartsWith("DB/items/items/", StringComparison.Ordinal)) kind = GameContentKind.Artifact;
            else if (path.StartsWith("DB/map/objects/", StringComparison.Ordinal) || path.StartsWith("DB/objects_logic/", StringComparison.Ordinal)) kind = GameContentKind.Object;
            else continue;
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            using var doc = TryRead(entry, warnings);
            if (doc is null) continue;
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("array", out var array) || array.ValueKind != JsonValueKind.Array)
            { warnings?.Add("Unrecognized game content shape: " + path); continue; }
            foreach (var row in array.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) continue;
                string sid = Text(row, "id");
                if (sid.Length == 0) continue;
                var key = (kind, sid);
                bool existed = entries.TryGetValue(key, out var item);
                item ??= new() { Sid = sid, Kind = kind, ReferencedByRmgContent = rmgSids.Contains(sid) };
                string nameKey = Text(row, "name");
                if (nameKey.Length == 0) nameKey = sid + "_name";
                string ru = ResolveName(namesRu, namesEn, nameKey, sid);
                string en = ResolveName(namesEn, namesRu, nameKey, sid);
                if (!existed || item.NameRu == sid || item.NameRu == Prettify(sid)) item.NameRu = ru;
                if (!existed || item.NameEn == sid || item.NameEn == Prettify(sid)) item.NameEn = en;
                item.SourceEntry = path;
                item.Faction = Text(row, "fraction");
                item.Category = kind switch
                {
                    GameContentKind.Spell => Text(row, "school_"),
                    GameContentKind.Artifact => Text(row, "rarity"),
                    _ => path.StartsWith("DB/objects_logic/", StringComparison.Ordinal) ? path.Split('/')[2] : Text(row, "tag")
                };
                item.Level = kind == GameContentKind.Spell ? Integer(row, "rank") : Integer(row, "maxLevel");
                item.HasGeneratorConfig |= row.TryGetProperty("generatorConfig", out var config) && config.ValueKind == JsonValueKind.Object;
                if (kind == GameContentKind.Artifact && row.TryGetProperty("bonuses", out var bonuses) && bonuses.ValueKind == JsonValueKind.Array)
                    foreach (var bonus in bonuses.EnumerateArray())
                        if (bonus.ValueKind == JsonValueKind.Object && Text(bonus, "type") == "heroMagicAddition" &&
                            bonus.TryGetProperty("parameters", out var spellParameters) && spellParameters.ValueKind == JsonValueKind.Array)
                            item.GrantedSpells.AddRange(spellParameters.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.String).Select(p => p.GetString()!));
                if (row.TryGetProperty("variants", out var variants) && variants.ValueKind == JsonValueKind.Array)
                {
                    item.RewardVariants = [];
                    int index = 0;
                    foreach (var variant in variants.EnumerateArray())
                    {
                        if (variant.ValueKind != JsonValueKind.Object) { index++; continue; }
                        var reward = new GameRewardVariant { Index = index++, RollChance = Number(variant, "rollChance"), Value = Number(variant, "value") };
                        if (variant.TryGetProperty("rewardSet", out var set) && set.ValueKind == JsonValueKind.Object &&
                            set.TryGetProperty("rewards", out var rewards) && rewards.ValueKind == JsonValueKind.Array)
                            foreach (var r in rewards.EnumerateArray())
                            {
                                if (r.ValueKind != JsonValueKind.Object) continue;
                                string type = Text(r, "rewardType");
                                if (type.Length > 0) reward.RewardTypes.Add(type);
                                if (r.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Array)
                                    reward.RewardParameters.AddRange(parameters.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.String).Select(p => p.GetString()!));
                            }
                        item.RewardVariants.Add(reward);
                    }
                }
                entries[key] = item;
            }
        }
        // Meta objects are actual generator aliases, not necessarily ordinary map-object DB rows.
        var generatorConfig = core.GetEntry("generator/generator_config.json");
        if (generatorConfig is not null)
        {
            using var doc = TryRead(generatorConfig, warnings);
            if (doc is not null && doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("metaObjects", out var meta) && meta.ValueKind == JsonValueKind.Array)
                foreach (var row in meta.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Object || Text(row, "sid") is not { Length: > 0 } sid) continue;
                    string category = Text(row, "type");
                    int? level = null;
                    if (category == "RandomHire" && row.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Array && args.GetArrayLength() > 0 &&
                        args[0].ValueKind == JsonValueKind.String && int.TryParse(args[0].GetString(), out int tier)) level = tier;
                    entries.TryAdd((GameContentKind.Object, sid), new GameContentEntry
                    {
                        Sid = sid, Kind = GameContentKind.Object, NameRu = ResolveName(namesRu, namesEn, sid + "_name", sid),
                        NameEn = ResolveName(namesEn, namesRu, sid + "_name", sid), Category = category, Level = level,
                        HasGeneratorConfig = true, ReferencedByRmgContent = rmgSids.Contains(sid), SourceEntry = generatorConfig.FullName
                    });
                }
        }
        foreach (var variant in entries.Values.SelectMany(e => e.RewardVariants))
        {
            var spellIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in variant.RewardParameters)
            {
                if (entries.TryGetValue((GameContentKind.Artifact, parameter), out var artifact)) spellIds.UnionWith(artifact.GrantedSpells);
                if (entries.ContainsKey((GameContentKind.Spell, parameter))) spellIds.Add(parameter);
            }
            foreach (var sid in spellIds)
                if (entries.TryGetValue((GameContentKind.Spell, sid), out var spell))
                    variant.Spells.Add(new(sid, spell.NameRu, spell.NameEn, spell.Level));
        }
        return entries.Values.OrderBy(e => e.Kind).ThenBy(e => e.Sid, StringComparer.Ordinal).ToList();
    }

    private static Dictionary<string, string> ReadNames(ZipArchive core, string language, ICollection<string>? warnings)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string file in new[] { "mapObjects", "artifacts", "magic", "cities" })
        {
            var entry = core.GetEntry($"Lang/{language}/texts/{file}.json");
            if (entry is null) continue;
            using var doc = TryRead(entry, warnings);
            if (doc is null) continue;
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("tokens", out var tokens) || tokens.ValueKind != JsonValueKind.Array)
            { warnings?.Add("Unrecognized game localization shape: " + entry.FullName); continue; }
            foreach (var token in tokens.EnumerateArray())
                if (token.ValueKind == JsonValueKind.Object && Text(token, "sid") is { Length: > 0 } sid && Text(token, "text") is { Length: > 0 } text)
                    names[sid] = text;
        }
        return names;
    }

    private static HashSet<string> ReadRmgSids(ZipArchive core, ICollection<string>? warnings)
    {
        var sids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in core.Entries.Where(e => e.FullName.StartsWith("generator/content_lists/", StringComparison.Ordinal) || e.FullName.StartsWith("generator/content_pools/", StringComparison.Ordinal)))
        {
            if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            using var doc = TryRead(entry, warnings);
            if (doc is not null) CollectSids(doc.RootElement, sids);
        }
        return sids;
    }

    private static void CollectSids(JsonElement value, HashSet<string> sids)
    {
        if (value.ValueKind == JsonValueKind.Array) foreach (var child in value.EnumerateArray()) CollectSids(child, sids);
        else if (value.ValueKind == JsonValueKind.Object)
            foreach (var p in value.EnumerateObject())
            { if (p.Name == "sid" && p.Value.ValueKind == JsonValueKind.String) sids.Add(p.Value.GetString()!); else CollectSids(p.Value, sids); }
    }

    private static JsonDocument? TryRead(ZipArchiveEntry entry, ICollection<string>? warnings)
    {
        try { using var stream = entry.Open(); return JsonDocument.Parse(stream, Options); }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
        { warnings?.Add($"Could not read {entry.FullName}: {ex.Message}"); return null; }
    }
    private static string Text(JsonElement value, string key) => value.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
    private static double? Number(JsonElement value, string key) => value.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d) ? d : null;
    private static int? Integer(JsonElement value, string key) => value.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i) ? i : null;
    private static string ResolveName(Dictionary<string, string> first, Dictionary<string, string> fallback, string key, string sid)
        => first.TryGetValue(key, out var name) ? name : fallback.TryGetValue(key, out name) ? name : Prettify(sid);
    private static string Prettify(string sid) => sid.Replace('_', ' ');
}
