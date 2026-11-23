using System.IO;
using System.Text.Json;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services.Analysis;

namespace Olden_Era___Template_Editor.Services.Export;

internal static class ReadyMapExporter
{
    private const int Seed = 1729;

    public static int Generate(string outputDirectory, TemplateReferenceCatalog? references = null)
    {
        string directory = Path.GetFullPath(outputDirectory);
        ExportDestination.EnsureOutsideGame(directory);
        var maps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var preset in PatchPresets.Current.Concat(PatchPresets.Cooperative))
        {
            var settings = Presets.ToGeneratorSettings(PatchPresets.Clone(preset.Settings));
            settings.Seed = Seed;
            settings.ProfileOptions.TemplateSeed = Seed;
            var template = TemplateGenerator.Generate(settings);
            var errors = TemplateCompatibilityValidator.Validate(template, TemplateValidationMode.StrictExport, references)
                .Where(issue => issue.Severity == TemplateIssueSeverity.Error).ToArray();
            if (errors.Length > 0)
                throw new InvalidOperationException($"ReadyMaps validation failed for {preset.Id}. {string.Join("\n", errors.Select(error => error.Message))}");
            string file = $"[Gen] {settings.TemplateName}.rmg.json";
            if (!maps.TryAdd(file, JsonSerializer.Serialize(template, JsonExport.Options)))
                throw new InvalidOperationException($"Multiple ReadyMaps share the name {file}.");
        }

        Directory.CreateDirectory(directory);
        foreach (var (file, json) in maps)
            AtomicFile.WriteAllText(Path.Combine(directory, file), json,
                validateDestination: path => ExportDestination.EnsureOutsideGame(path));
        return maps.Count;
    }
}
