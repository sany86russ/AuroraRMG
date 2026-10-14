using System.Text.Json;
using Olden_Era___Template_Editor.Services.Localization;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services;

/// <summary>A duplicate member has no unambiguous value; imports never merge it or choose a winner.</summary>
public sealed class DuplicateJsonPropertyException(string propertyName, string jsonPath)
    : JsonException(LocalizationManager.T("S.TemplateImport.DuplicateProperty", propertyName, jsonPath), jsonPath, null, null)
{
    public string PropertyName { get; } = propertyName;
}

/// <summary>Checks external JSON before typed or JsonNode readers can discard repeated properties.</summary>
public static class TemplateJsonReader
{
    private static readonly JsonDocumentOptions GuardOptions = new()
    { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 128 };

    public static RmgTemplate? Deserialize(string json, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        GuardBeforeDeserialize(json);
        return JsonSerializer.Deserialize<RmgTemplate>(json, options);
    }

    public static void GuardBeforeDeserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json, GuardOptions);
        Visit(document.RootElement, "$");
        static void Visit(JsonElement node, string path)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in node.EnumerateObject())
                {
                    string child = path + "." + property.Name;
                    if (!names.Add(property.Name)) throw new DuplicateJsonPropertyException(property.Name, child);
                    Visit(property.Value, child);
                }
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var item in node.EnumerateArray()) Visit(item, path + "[" + index++ + "]");
            }
        }
    }
}
