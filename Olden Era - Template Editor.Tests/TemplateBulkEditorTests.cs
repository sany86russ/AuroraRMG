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

    [Fact]
    public void BulkEditCanUseASecondVariantWhenTheFirstIsNull()
    {
        var source = TemplateJsonReader.Deserialize("""{"name":"Variant gap","sizeX":32,"sizeZ":32,"variants":[null,{"zones":[{"name":"Second"}]}]}""", JsonExport.Options)!;

        Assert.Equal([1], TemplateBulkEditor.GetEditableVariants(source));
        var result = TemplateBulkEditor.Apply(source, 1, ["Second"], "guardMultiplier", "1.25");
        Assert.Null(result.Variants![0]);
        Assert.Equal(1.25, result.Variants[1].Zones![0].GuardMultiplier);
        Assert.Null(source.Variants![1].Zones![0].GuardMultiplier);
    }

    [Fact]
    public void DuplicateZonesCannotMaskAMissingSelectedZone()
    {
        var source = new RmgTemplate { Name = "Ambiguous", SizeX = 32, SizeZ = 32,
            Variants = [new() { Zones = [new() { Name = "a" }, new() { Name = "a" }] }] };

        Assert.Throws<ArgumentException>(() => TemplateBulkEditor.Apply(source, 0, ["a", "b"], "guardMultiplier", "2"));
        Assert.All(source.Variants![0].Zones!, zone => Assert.Null(zone.GuardMultiplier));
    }
}
