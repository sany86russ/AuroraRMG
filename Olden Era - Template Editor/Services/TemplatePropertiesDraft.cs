using System.Text.Json;
using System.Text.Json.Nodes;
using Olden_Era___Template_Editor.Services.Analysis;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services;

/// <summary>A detached root-properties draft. Cancel or a failed commit never changes the editor's document.</summary>
public sealed class TemplatePropertiesDraft
{
    private readonly JsonObject _source;
    private readonly HashSet<(string Code, string Path, string Message)> _existingErrors;
    private readonly TemplateReferenceCatalog? _catalog;

    public TemplatePropertiesDraft(RmgTemplate source, TemplateReferenceCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        _catalog = catalog;
        _source = JsonSerializer.SerializeToNode(source, JsonExport.Options)!.AsObject();
        _existingErrors = TemplateCompatibilityValidator.Validate(source, catalog: _catalog)
            .Where(issue => issue.Severity == TemplateIssueSeverity.Error).Select(issue => (issue.Code, issue.Path, issue.Message)).ToHashSet();
    }

    public JsonObject Snapshot() => _source.DeepClone().AsObject();

    /// <summary>Preserves blank/null/absent source fields unless their displayed text was edited.</summary>
    public static bool ApplyTextEdit(JsonObject candidate, IReadOnlyList<string> path, string property,
        string initialText, string currentText, Func<string, JsonNode?> parseChangedText)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(parseChangedText);
        if (string.Equals(initialText, currentText, StringComparison.Ordinal)) return false;
        JsonNode? value = parseChangedText(currentText);
        JsonNode current = candidate;
        foreach (string part in path)
        {
            if (current is JsonObject obj) { obj[part] ??= new JsonObject(); current = obj[part]!; }
            else if (current is JsonArray array && int.TryParse(part, out int index) && index >= 0 && index < array.Count)
                current = array[index] ?? throw new JsonException("A text field cannot target a null array entry.");
            else throw new JsonException("A text field has an invalid object path.");
        }
        if (current is not JsonObject target) throw new JsonException("A text field must belong to an object.");
        if (value is null) target.Remove(property); else target[property] = value;
        return true;
    }

    public RmgTemplate Commit(JsonObject candidate, out IReadOnlyList<TemplateValidationIssue> findings)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var result = candidate.Deserialize<RmgTemplate>(JsonExport.Options) ?? throw new JsonException("Template must be an object.");
        findings = TemplateCompatibilityValidator.Validate(result, catalog: _catalog);
        var added = findings.Where(issue => issue.Severity == TemplateIssueSeverity.Error
            && (!_existingErrors.Contains((issue.Code, issue.Path, issue.Message))
                || issue.Code != "graph.invalid" && issue.Code != "variants.spawns" && ChangedAt(issue.Path, candidate))).ToArray();
        if (added.Length > 0) throw new InvalidOperationException(string.Join("\n", added.Take(15).Select(issue => $"{issue.Path}: {issue.Message}")));
        return result;
    }

    private bool ChangedAt(string path, JsonObject candidate)
    {
        var parts = System.Text.RegularExpressions.Regex.Matches(path, @"([^.[\]]+)|\[(\d+)\]")
            .Select(match => match.Groups[2].Success ? match.Groups[2].Value : match.Groups[1].Value).ToArray();
        return !JsonNode.DeepEquals(At(_source, parts), At(candidate, parts));
        static JsonNode? At(JsonNode root, string[] parts)
        {
            JsonNode? node = root;
            foreach (string part in parts)
            {
                // Some findings refer to a group such as heroCount rather than a JSON property.
                // Its containing object must still be compared when editing an already-invalid range.
                if (node is JsonObject obj)
                { if (!obj.TryGetPropertyValue(part, out var child)) return obj; node = child; }
                else if (node is JsonArray array && int.TryParse(part, out var index) && index >= 0 && index < array.Count) node = array[index];
                else return node;
            }
            return node;
        }
    }
}
