using System.Text.Json;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services;

namespace Olden_Era___Template_Editor.Tests;

public class TemplateBulkEditorTests
{
    [Fact]
    public void BulkEditPreservesOtherVariantsUnknownFieldsAndOriginalDocument()
    {
        var source = JsonSerializer.Deserialize<RmgTemplate>("""{"name":"x","sizeX":128,"sizeZ":128,"variants":[{"zones":[{"name":"a","future":{"k":1}},{"name":"b"}]},{"zones":[{"name":"a","guardMultiplier":2}]}]}""", JsonExport.Options)!;
        var edited = TemplateBulkEditor.Apply(source, 0, ["a", "b"], "guardMultiplier", "1.25");
        Assert.All(edited.Variants![0].Zones!, z => Assert.Equal(1.25, z.GuardMultiplier));
        Assert.Equal(1, edited.Variants[0].Zones![0].Extra!["future"].GetProperty("k").GetInt32());
        Assert.Equal(2, edited.Variants[1].Zones![0].GuardMultiplier);
        Assert.Null(source.Variants![0].Zones![0].GuardMultiplier);
    }

    [Fact]
    public void InvalidNewWeightsAreRejectedWithoutPartialChanges()
    {
        var source = new RmgTemplate { SizeX = 128, SizeZ = 128, Variants = [new() { Zones = [new() { Name = "a" }] }] };
        Assert.ThrowsAny<Exception>(() => TemplateBulkEditor.Apply(source, 0, ["a"], "guardReactionDistribution", "[1,-2]"));
        Assert.Null(source.Variants![0].Zones![0].GuardReactionDistribution);
    }
}
