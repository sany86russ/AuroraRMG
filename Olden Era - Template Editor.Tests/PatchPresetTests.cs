using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Analysis;

namespace Olden_Era___Template_Editor.Tests;

public class PatchPresetTests
{
    [Fact]
    public void AllLegacyPresetsHaveSeparatePatchVariantsWithoutMutatingOriginals()
    {
        Assert.Equal(43, PatchPresets.Current.Count);
        Assert.All(Presets.All, p => Assert.Equal(GenerationProfileOptions.LegacyProfile, p.Settings.ProfileOptions.ProfileId));
        Assert.All(PatchPresets.Current, p => Assert.Equal(GenerationProfileOptions.CurrentProfile, p.Settings.ProfileOptions.ProfileId));
        var changed = PatchPresets.Clone(PatchPresets.Current[0].Settings);
        changed.ProfileOptions.Alliances.Enabled = true;
        Assert.False(PatchPresets.Current[0].Settings.ProfileOptions.Alliances.Enabled);
    }

    public static IEnumerable<object[]> Cases() => PatchPresets.Current.Concat(PatchPresets.Cooperative)
        .SelectMany(p => new[] { 17, 20261014 }.Select(seed => new object[] { p.Id, p.Settings, seed }));

    [Theory]
    [MemberData(nameof(Cases))]
    public void PatchPresetsProduceReproducibleStructurallyValidTemplates(string id, SettingsFile persisted, int seed)
    {
        Assert.Contains(id, PatchPresets.Current.Concat(PatchPresets.Cooperative).Select(p => p.Id));
        var settings = Presets.ToGeneratorSettings(persisted);
        settings.Seed = seed;
        var first = TemplateGenerator.Generate(settings);
        var second = TemplateGenerator.Generate(settings);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(first, JsonExport.Options), System.Text.Json.JsonSerializer.Serialize(second, JsonExport.Options));
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(first), i => i.Severity == TemplateIssueSeverity.Error);
        Assert.NotNull(first.DescBlocks);
        Assert.Equal(seed, settings.Seed);
    }
}
