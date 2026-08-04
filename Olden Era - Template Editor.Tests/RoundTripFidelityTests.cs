using OldenEraTemplateEditor.Models;
using System.Text.Json;

namespace Olden_Era___Template_Editor.Tests;

/// <summary>
/// A template opened in the visual editor and saved again must come back out unchanged. Before the
/// <see cref="RmgNode"/> catch-all, every field the POCOs did not model was silently dropped —
/// importing a stock map and re-saving it lost e.g. <c>connections[].guardRandomization</c>,
/// <c>mainObjects[].factions</c> and the zones' <c>randomHire*</c> arrays.
/// </summary>
public class RoundTripFidelityTests
{
    private static JsonSerializerOptions Options => Olden_Era___Template_Editor.Services.JsonExport.Options;

    /// <summary>
    /// Every field name that appears anywhere in a template, by JSON path shape. Walks
    /// <see cref="JsonElement"/> rather than <see cref="JsonNode"/> because at least one stock
    /// template repeats a top-level key (<c>zoneLayouts</c>), which JsonNode rejects outright.
    /// </summary>
    private static IEnumerable<string> Fields(JsonElement element, string path = "")
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty prop in element.EnumerateObject())
                {
                    string child = path.Length == 0 ? prop.Name : $"{path}.{prop.Name}";
                    yield return child;
                    foreach (string f in Fields(prop.Value, child)) yield return f;
                }
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                    foreach (string f in Fields(item, $"{path}[]")) yield return f;
                break;
        }
    }

    [Fact]
    public void LoadAndSave_KeepsEveryFieldOfTheBundledExampleTemplates()
    {
        // The repo does not publish the studio's game data, so the corpus is whatever .rmg.json files
        // are available locally (bundled examples and/or the installed game). Skip cleanly when none.
        var corpus = new List<string>();
        foreach (string dir in TemplateCorpusDirectories())
            if (Directory.Exists(dir))
                corpus.AddRange(Directory.GetFiles(dir, "*.rmg.json"));

        if (corpus.Count == 0) return;

        var losses = new List<string>();
        foreach (string file in corpus.Take(200))
        {
            string json = File.ReadAllText(file);
            RmgTemplate? template;
            try
            {
                template = JsonSerializer.Deserialize<RmgTemplate>(json, Options);
            }
            catch (JsonException ex)
            {
                losses.Add($"{Path.GetFileName(file)}: FAILED TO LOAD — {ex.Message}");
                continue;
            }
            if (template is null) { losses.Add($"{Path.GetFileName(file)}: deserialized to null"); continue; }

            string reSaved = JsonSerializer.Serialize(template, Options);

            using JsonDocument original = JsonDocument.Parse(json);
            using JsonDocument rewritten = JsonDocument.Parse(reSaved);
            // "[]" is stripped so the deliberate single-object → one-element-array normalisation
            // (see SingleObjectOrArrayConverterFactory) does not read as a lost field.
            var before = Fields(original.RootElement).Select(Flatten).ToHashSet(StringComparer.Ordinal);
            var after = Fields(rewritten.RootElement).Select(Flatten).ToHashSet(StringComparer.Ordinal);

            foreach (string lost in before.Except(after).OrderBy(f => f, StringComparer.Ordinal))
                losses.Add($"{Path.GetFileName(file)}: lost '{lost}'");
        }

        Assert.True(losses.Count == 0,
            $"{losses.Count} field(s) lost on load→save:\n" + string.Join("\n", losses.Distinct().Take(30)));
    }

    private static string Flatten(string path) => path.Replace("[]", "");

    private static IEnumerable<string> TemplateCorpusDirectories()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ReadyMaps");
        yield return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                                  "Olden Era - Template Editor", "GameData", "ExampleTemplates");

        // The installed game, when present on this machine (CI has no game install).
        foreach (string drive in new[] { "G", "C", "D", "E", "F" })
            yield return $@"{drive}:\Steam\steamapps\common\Heroes of Might and Magic Olden Era\HeroesOldenEra_Data\StreamingAssets\map_templates";
    }
}
