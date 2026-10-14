using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.Analysis;

public sealed record TemplateRoundTripFinding(string Path, string Message);
public sealed record TemplateRoundTripResult(IReadOnlyList<TemplateRoundTripFinding> Differences,
    IReadOnlyList<TemplateRoundTripFinding> Normalizations)
{
    public bool PreservesValues => Differences.Count == 0;
}

/// <summary>Checks source values at every depth against the typed export, including JsonExtensionData.</summary>
public static class TemplateRoundTripAudit
{
    private static readonly JsonDocumentOptions ReadOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 128 };

    public static TemplateRoundTripResult Compare(string originalJson, string exportedJson)
    {
        using var source = JsonDocument.Parse(originalJson, ReadOptions);
        using var destination = JsonDocument.Parse(exportedJson, ReadOptions);
        var lost = new List<TemplateRoundTripFinding>(); var normalized = new List<TemplateRoundTripFinding>();
        CompareNode(source.RootElement, destination.RootElement, typeof(RmgTemplate), "$", false);
        return new(lost.ToArray(), normalized.ToArray());

        void CompareNode(JsonElement before, JsonElement after, Type? expected, string path, bool scalarStringList)
        {
            expected = expected is null ? null : Nullable.GetUnderlyingType(expected) ?? expected;
            Type? elementType = expected?.IsGenericType == true && expected.GetGenericTypeDefinition() == typeof(List<>) ? expected.GetGenericArguments()[0] : null;
            if (elementType is not null && before.ValueKind is not (JsonValueKind.Array or JsonValueKind.Null) && after.ValueKind == JsonValueKind.Array && after.GetArrayLength() == 1)
            {
                bool accepted = !elementType.IsValueType && elementType != typeof(string)
                    || elementType == typeof(string) && before.ValueKind is JsonValueKind.String or JsonValueKind.Number
                    || scalarStringList && elementType.IsValueType && before.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False;
                if (accepted)
                {
                    normalized.Add(new(path, "Accepted collapsed collection was expanded to an array."));
                    CompareNode(before, after[0], elementType, path + "[0]", elementType == typeof(string)); return;
                }
            }
            if (before.ValueKind == JsonValueKind.Number && after.ValueKind == JsonValueKind.String && expected == typeof(string) && scalarStringList)
            {
                if (before.GetRawText() != after.GetString()) Difference(path, "Numeric string-list argument changed its value.");
                else normalized.Add(new(path, "Accepted numeric string-list argument was written as a string."));
                return;
            }
            if (before.ValueKind != after.ValueKind) { Difference(path, $"Value type changed from {before.ValueKind} to {after.ValueKind}."); return; }
            switch (before.ValueKind)
            {
                case JsonValueKind.Object:
                    var properties = expected is null || expected == typeof(object) ? new Dictionary<string, PropertyInfo>(StringComparer.Ordinal)
                        : expected.GetProperties().Where(property => property.GetCustomAttribute<JsonPropertyNameAttribute>() is not null)
                            .ToDictionary(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name, StringComparer.Ordinal);
                    foreach (var property in before.EnumerateObject())
                    {
                        properties.TryGetValue(property.Name, out var modeled);
                        if (!after.TryGetProperty(property.Name, out var written))
                        {
                            if (property.Value.ValueKind == JsonValueKind.Null && modeled is not null)
                                normalized.Add(new(path + "." + property.Name, "Optional explicit null was omitted by the export contract."));
                            else Difference(path + "." + property.Name, "Source property was lost.");
                            continue;
                        }
                        bool collapsedValueArray = modeled?.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType?.IsGenericType == true
                            && modeled.GetCustomAttribute<JsonConverterAttribute>()!.ConverterType!.GetGenericTypeDefinition() == typeof(ScalarOrArrayConverter<>);
                        CompareNode(property.Value, written, modeled?.PropertyType, path + "." + property.Name, collapsedValueArray);
                    }
                    break;
                case JsonValueKind.Array:
                    if (before.GetArrayLength() != after.GetArrayLength()) { Difference(path, "Array length changed."); break; }
                    for (int index = 0; index < before.GetArrayLength(); index++)
                        CompareNode(before[index], after[index], elementType, path + "[" + index + "]", elementType == typeof(string));
                    break;
                case JsonValueKind.String:
                    if (before.GetString() != after.GetString()) Difference(path, "String value changed (identifiers are compared exactly).");
                    break;
                case JsonValueKind.Number:
                    // Typed floating-point fields have double precision. Unknown numeric literals retain their exact text.
                    bool same = expected is null || expected == typeof(object) ? before.GetRawText() == after.GetRawText()
                        : expected == typeof(double) ? double.IsFinite(before.GetDouble()) && double.IsFinite(after.GetDouble()) && before.GetDouble().Equals(after.GetDouble())
                        : before.TryGetDecimal(out var first) && after.TryGetDecimal(out var second) ? first == second
                        : before.GetRawText() == after.GetRawText();
                    if (!same) Difference(path, "Numeric value changed.");
                    else if (before.GetRawText() != after.GetRawText()) normalized.Add(new(path, "Modeled numeric formatting was normalized at its declared precision."));
                    break;
                case JsonValueKind.True: case JsonValueKind.False: case JsonValueKind.Null: break;
                default: Difference(path, "Unrecognized JSON value."); break;
            }
        }
        void Difference(string path, string message) => lost.Add(new(path, message));
    }
}
