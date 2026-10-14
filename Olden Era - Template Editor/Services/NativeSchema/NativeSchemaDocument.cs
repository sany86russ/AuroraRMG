using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Olden_Era___Template_Editor.Models.NativeSchema;

namespace Olden_Era___Template_Editor.Services.NativeSchema;

/// <summary>
/// Offline .mrmg reader and editor. A schema is not an .rmg template, a map or a save.
/// No files are registered with the game by this service.
/// </summary>
public sealed class NativeSchemaDocument
{
    public JsonObject Root { get; }

    private NativeSchemaDocument(JsonObject root) => Root = root;

    public static NativeSchemaDocument Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            MaxDepth = 128
        }) as JsonObject ?? throw new JsonException("A native schema must be a JSON object.");
        if (root["zoneClasses"] is not JsonArray || root["mainObjectClasses"] is not JsonArray)
            throw new JsonException("A native schema requires zoneClasses and mainObjectClasses arrays.");
        return new NativeSchemaDocument(root);
    }

    public static NativeSchemaDocument Read(string path) => Parse(File.ReadAllText(path));

    public string ToJson(bool indented = true) => Root.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = indented,
        MaxDepth = 128,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });

    public IReadOnlyList<string> Names(string collection)
    {
        if (Root[collection] is not JsonArray rows)
            throw new ArgumentException("Unknown native schema collection.", nameof(collection));
        return rows.Select(row => row?["name"]?.GetValue<string>()
            ?? throw new JsonException($"A {collection} entry has no name.")).ToArray();
    }

    public JsonObject Find(string collection, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (Root[collection] is not JsonArray rows)
            throw new ArgumentException("Unknown native schema collection.", nameof(collection));
        var matches = rows.OfType<JsonObject>()
            .Where(row => string.Equals(row["name"]?.GetValue<string>(), name, StringComparison.Ordinal))
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new KeyNotFoundException($"Unknown {collection} identifier '{name}'."),
            _ => throw new JsonException($"Duplicate {collection} identifier '{name}'.")
        };
    }

    public static IReadOnlyList<NativeConditionalEntry> ReadTable(JsonObject owner, string parameter)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!owner.TryGetPropertyValue(parameter, out var value) || value is null)
            return [];
        if (value is not JsonArray rows)
            throw new JsonException($"Native conditional parameter '{parameter}' must be an array.");
        return rows.Select((row, index) => new NativeConditionalEntry(index,
            (row as JsonObject)?.DeepClone().AsObject()
            ?? throw new JsonException($"'{parameter}' row {index} must be an object."))).ToArray();
    }

    public static void SetConditionalValue(JsonObject owner, string parameter, int index, JsonNode? value)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (owner[parameter] is not JsonArray rows)
            throw new JsonException($"Native conditional parameter '{parameter}' must be an array.");
        if (index < 0 || index >= rows.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (rows[index] is not JsonObject row)
            throw new JsonException($"'{parameter}' row {index} must be an object.");
        row["value"] = value?.DeepClone();
    }

    /// <summary>Checks exact local references only; external game database references stay external.</summary>
    public IReadOnlyList<string> ValidateLocalReferences()
    {
        var findings = new List<string>();
        ValidateConditionalRows(Root, "$", findings);
        foreach (var collection in new[] { "zoneClasses", "mainObjectClasses", "zoneLayouts",
            "mandatoryContent", "contentCountLimits", "contentPools", "contentLists" })
        {
            if (Root[collection] is not JsonArray rows) continue;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                if (row is not JsonObject entry || entry["name"] is not JsonValue nameNode
                    || !nameNode.TryGetValue<string>(out var name) || string.IsNullOrEmpty(name))
                {
                    findings.Add($"{collection}: missing or invalid name.");
                    continue;
                }
                if (!names.Add(name)) findings.Add($"{collection}: duplicate identifier '{name}'.");
            }
        }
        var zoneNames = ValidNames("zoneClasses");
        foreach (var parameter in new[] { "defaultZoneClass", "centerZoneClass", "primarySpawnZoneClass",
            "secondarySpawnZoneClass", "centerSpawnZoneClass" })
            CheckClassReferences(Root, parameter, zoneNames, parameter);
        var mainNames = ValidNames("mainObjectClasses");
        CheckClassReferences(Root, "holdCityMainObjectClass", mainNames, "holdCityMainObjectClass");
        foreach (var zone in (JsonArray)Root["zoneClasses"]!)
        {
            if (zone is not JsonObject owner) continue;
            CheckClassReferences(owner, "mainObjectClass", mainNames, $"{owner["name"]}.mainObjectClass");
        }
        return findings;

        HashSet<string> ValidNames(string collection) => ((JsonArray)Root[collection]!).OfType<JsonObject>()
            .Select(row => row["name"] is JsonValue name && name.TryGetValue<string>(out var text) ? text : null)
            .OfType<string>().ToHashSet(StringComparer.Ordinal);

        void CheckClassReferences(JsonObject owner, string parameter, HashSet<string> names, string path)
        {
            if (owner[parameter] is null) return;
            if (owner[parameter] is not JsonArray rows) { findings.Add($"{path}: expected conditional array."); return; }
            for (var index = 0; index < rows.Count; index++)
            {
                if (rows[index] is not JsonObject row || row["value"] is not JsonValue value || !value.TryGetValue<string>(out var name))
                    findings.Add($"{path}[{index}]: a class reference requires a string value.");
                else if (!names.Contains(name)) findings.Add($"{path}[{index}]: unknown class '{name}'.");
            }
        }
    }

    private static void ValidateConditionalRows(JsonNode? node, string path, List<string> findings)
    {
        if (node is JsonArray rows)
        {
            var table = rows.OfType<JsonObject>().ToArray();
            if (table.Length == rows.Count && table.Length > 0 && table.All(row => row.ContainsKey("value"))
                && table.All(row => row["weight"] is null || NativeConditionalEntry.TryReadNumber(row["weight"], out var amount) && NativeSchemaWeight.IsRepresentable(amount))
                && table.All(row => row["fallback"] is null || row["fallback"] is JsonValue boolean && boolean.TryGetValue<bool>(out _)))
            {
                foreach (bool fallback in new[] { false, true })
                {
                    var group = table.Where(row => (row["fallback"]?.GetValue<bool>() ?? false) == fallback).ToArray();
                    if (group.Length > 0 && !NativeSchemaWeight.HasUsableSum(group.Select(row => new NativeConditionalEntry(0, row).Weight)))
                        findings.Add($"{path}: {(fallback ? "fallback" : "conditional")} weights have no positive finite native sum; reduce relative weights before export.");
                }
            }
            for (var index = 0; index < rows.Count; index++) ValidateConditionalRows(rows[index], $"{path}[{index}]", findings);
        }
        else if (node is JsonObject obj)
        {
            if (obj.ContainsKey("conditionsAnd") || obj.ContainsKey("conditionsOr") || obj.ContainsKey("fallback")
                || obj.ContainsKey("value") && obj.ContainsKey("weight"))
            {
                if (!obj.ContainsKey("value")) findings.Add($"{path}: conditional row has no value.");
                if (obj["weight"] is { } weight && (!NativeConditionalEntry.TryReadNumber(weight, out var amount) || !NativeSchemaWeight.IsRepresentable(amount)))
                    findings.Add($"{path}.weight: expected nonnegative weight representable as a finite native float.");
                if (obj["fallback"] is { } fallback && (fallback is not JsonValue boolean || !boolean.TryGetValue<bool>(out _)))
                    findings.Add($"{path}.fallback: expected boolean.");
                foreach (var condition in new[] { "conditionsAnd", "conditionsOr" })
                    if (obj[condition] is { } value && value is not JsonObject)
                        findings.Add($"{path}.{condition}: expected an object.");
            }
            foreach (var property in obj) ValidateConditionalRows(property.Value, path + "." + property.Key, findings);
        }
    }
}
