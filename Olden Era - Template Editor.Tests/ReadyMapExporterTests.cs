using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services.Analysis;
using Olden_Era___Template_Editor.Services.Export;

namespace Olden_Era___Template_Editor.Tests;

public sealed class ReadyMapExporterTests
{
    [Fact]
    public void WritesEveryCurrentAndCooperativePresetWithTheSameBytesOnRepeat()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"aurora-ready-{Guid.NewGuid():N}");
        try
        {
            int expected = PatchPresets.Current.Count + PatchPresets.Cooperative.Count;
            Assert.Equal(expected, ReadyMapExporter.Generate(directory));
            var original = Directory.GetFiles(directory, "*.rmg.json").ToDictionary(path => path, File.ReadAllText);
            Assert.Equal(expected, original.Count);
            Assert.All(original.Values, json => Assert.Contains(GenerationProfileOptions.CurrentProfile, json));

            Assert.Equal(expected, ReadyMapExporter.Generate(directory));
            Assert.All(original, file => Assert.Equal(file.Value, File.ReadAllText(file.Key)));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void StrictValidationRejectsAnInvalidCatalogBeforeWritingAnyFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"aurora-invalid-ready-{Guid.NewGuid():N}");
        var references = new TemplateReferenceCatalog { IsComplete = true, Pools = new HashSet<string>(StringComparer.Ordinal) };

        Assert.Throws<InvalidOperationException>(() => ReadyMapExporter.Generate(directory, references));
        Assert.False(Directory.Exists(directory));
    }
}
