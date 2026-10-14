using System.Text.Json;
using Olden_Era___Template_Editor.Services.ContentPools;
using Olden_Era___Template_Editor.Services.GameData;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public class PoolContentInspectorTests
{
    [Fact]
    public void AuthorListOverridesInstalledContentAndShowsExactVariantFractionalWeightsAndDependencySource()
    {
        var pool = new GamePool { Name = "pool", Groups = [new() { Weight = 0.25, IncludeLists = ["list"] }, new() { Weight = 0.75, Content = [new() { Sid = "direct", Weight = 1.5 }] }] };
        var catalog = new GamePoolCatalog { Source = new GameInstallation(), ContentLists = [new() { Name = "list", Content = [new() { Sid = "installed", Weight = 100 }] }] };
        var template = new RmgTemplate { ContentLists = [new ContentListEntry { Name = "list", Content = [new() { Sid = "author ", Weight = 0.125, Variant = 2 }, new() { Sid = "author_2", Weight = 0.375 }] }] };
        var report = PoolContentInspector.Inspect(pool, catalog, template);
        Assert.DoesNotContain(report.Items, row => row.Sid == "installed");
        var row = report.Items[0]; Assert.Equal("author ", row.Sid); Assert.Equal(2, row.Variant); Assert.Equal(0.125, row.Weight);
        Assert.Equal(0.25, row.RelativeGroupShare); Assert.Equal(0.25, row.RelativeWithinListShare);
        Assert.Equal(PoolReferenceOrigin.TemplateDefinition, Assert.Single(report.Groups[0].Dependencies).Origin);
        Assert.Empty(report.Warnings); Assert.Equal(2, row.Variant);
        Assert.Equal(1.5, report.Items[2].Weight); Assert.Equal(0.75, report.Items[2].RelativeGroupShare);
    }

    [Fact]
    public void InvalidAuthorDefinitionNeverShowsInstalledFallbackAsAuthorContent()
    {
        var pool = new GamePool { Name = "pool", Groups = [new() { Weight = 1, IncludeLists = ["list"] }] };
        var catalog = new GamePoolCatalog { Source = new GameInstallation(), ContentLists = [new() { Name = "list", Content = [new() { Sid = "fallback", Weight = 1 }] }] };
        var template = new RmgTemplate { ContentLists = [JsonSerializer.SerializeToElement(new { name = "list", content = "broken" })] };
        var report = PoolContentInspector.Inspect(pool, catalog, template);
        Assert.True(Assert.Single(report.Items).IsMissing);
        Assert.Equal(PoolReferenceOrigin.InvalidTemplateDefinition, Assert.Single(report.Groups[0].Dependencies).Origin);
        Assert.DoesNotContain(report.Items, row => row.Sid == "fallback"); Assert.NotEmpty(report.Warnings);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1e50)]
    public void InvalidWeightsDoNotBecomeMisleadingRelativeShares(double weight)
    {
        var pool = new GamePool { Groups = [new() { Weight = weight, Content = [new() { Sid = "a", Weight = weight }] }] };
        var report = PoolContentInspector.Inspect(pool, new()); var item = Assert.Single(report.Items);
        Assert.Null(item.RelativeGroupShare); Assert.Null(item.RelativeWithinListShare); Assert.NotEmpty(report.Warnings);
    }

    [Fact]
    public void NullImportedRowsAreDiagnosedAndMissingOfflineListIsDistinguished()
    {
        var pool = new GamePool { Groups = [null!, new() { Weight = 1, IncludeLists = ["unknown"], Content = [null!] }] };
        var report = PoolContentInspector.Inspect(pool, new());
        Assert.True(Assert.Single(report.Items).IsMissing); Assert.NotEmpty(report.Warnings);
        Assert.Equal(PoolReferenceOrigin.GameUnavailable, Assert.Single(report.Groups[0].Dependencies).Origin);
    }
}
