using System.Text.Json;
using System.Text.Json.Nodes;
using Olden_Era___Template_Editor.Models;

namespace Olden_Era___Template_Editor.Services;

/// <summary>Edits visible input groups while retaining original groups that the UI cannot fully represent.</summary>
public static class GeneratorSettingsOverlay
{
    public static GeneratorSettings Merge(GeneratorSettings originalSnapshot, GeneratorSettings uiBaseline, GeneratorSettings currentUi)
    {
        ArgumentNullException.ThrowIfNull(originalSnapshot); ArgumentNullException.ThrowIfNull(uiBaseline); ArgumentNullException.ThrowIfNull(currentUi);
        var options = SettingsSnapshotMapper.SnapshotOptions;
        var result = JsonSerializer.SerializeToNode(originalSnapshot, options)!.AsObject();
        var baseline = JsonSerializer.SerializeToNode(uiBaseline, options)!.AsObject();
        var current = JsonSerializer.SerializeToNode(currentUi, options)!.AsObject();
        foreach (var property in current)
            if (!JsonNode.DeepEquals(property.Value, baseline[property.Key])) result[property.Key] = property.Value?.DeepClone();
        return result.Deserialize<GeneratorSettings>(options) ?? throw new JsonException("Merged generator settings could not be restored.");
    }
}
