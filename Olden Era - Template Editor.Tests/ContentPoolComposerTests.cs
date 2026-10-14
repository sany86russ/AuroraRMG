using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Analysis;
using Olden_Era___Template_Editor.Services.ContentPools;
using Olden_Era___Template_Editor.Services.GameData;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public class ContentPoolComposerTests
{
    [Fact]
    public void FractionalWeightsActualVariantAndExactSidAreEmittedWithoutCountOrLevelGuesses()
    {
        var group = new PoolGroupDraft { WeightText = "0,25" };
        group.Content.Add(new() { Sid = "scroll_box ", WeightText = "1.75", VariantText = "2" });
        var pool = ContentPoolComposer.Build("Rewards", [group], culture: CultureInfo.GetCultureInfo("ru-RU"));
        Assert.Equal("custom_Rewards", pool.Name); Assert.Equal(0.25, pool.Groups[0].Weight);
        var item = Assert.Single(pool.Groups[0].Content!);
        Assert.Equal("scroll_box ", item.Sid); Assert.Equal(1.75, item.Weight); Assert.Equal(2, item.Variant);
        string json = JsonSerializer.Serialize(pool);
        Assert.DoesNotContain("maxCount", json); Assert.DoesNotContain("level", json);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e309")]
    [InlineData("1e50")]
    [InlineData("")]
    public void InvalidOrNativeFloatOverflowingWeightsAreRejected(string weight)
    {
        var group = new PoolGroupDraft { WeightText = weight }; group.IncludeLists.Add("list");
        Assert.Equal("Weight", Assert.Throws<ContentPoolValidationException>(() => ContentPoolComposer.Build("name", [group])).Code);
    }

    [Fact]
    public void PositiveWeightsMustSelectContentAndZeroGroupsCannotBeCreated()
    {
        var empty = new PoolGroupDraft();
        Assert.Equal("EmptyGroup", Assert.Throws<ContentPoolValidationException>(() => ContentPoolComposer.Build("name", [empty])).Code);
        empty.WeightText = "0";
        Assert.Equal("ZeroGroups", Assert.Throws<ContentPoolValidationException>(() => ContentPoolComposer.Build("name", [empty])).Code);
        Assert.Equal("ZeroGroups", Assert.Throws<ContentPoolValidationException>(() => ContentPoolComposer.Build("name", [])).Code);
    }

    [Fact]
    public void DuplicateEntriesAndUnknownConfiguredVariantAreDiagnosed()
    {
        var group = new PoolGroupDraft(); group.IncludeLists.Add("same"); group.IncludeLists.Add("same");
        Assert.Equal("DuplicateList", Assert.Throws<ContentPoolValidationException>(() => ContentPoolComposer.Build("name", [group])).Code);
        group.IncludeLists.Clear(); group.Content.Add(new() { Sid = "scroll_box", VariantText = "3" });
        var catalog = new GameCatalog { Content = [new() { Kind = GameContentKind.Object, Sid = "scroll_box", RewardVariants = [new() { Index = 0 }, new() { Index = 1 }] }] };
        Assert.Equal("UnknownVariant", Assert.Throws<ContentPoolValidationException>(() => ContentPoolComposer.Build("name", [group], catalog: catalog)).Code);
        group.Content[0].VariantText = "0"; group.Content.Add(new() { Sid = "scroll_box", VariantText = "0" });
        Assert.Equal("DuplicateItem", Assert.Throws<ContentPoolValidationException>(() => ContentPoolComposer.Build("name", [group])).Code);
    }

    [Fact]
    public void EditingRetainsUnknownFieldsAndNeverMutatesOriginalOrSharesResultAliases()
    {
        var original = JsonSerializer.Deserialize<GamePool>("{\"name\":\"custom_original\",\"futurePool\":true,\"groups\":[{\"weight\":1,\"futureGroup\":\"keep\",\"content\":[{\"sid\":\"scroll_box\",\"weight\":1,\"futureItem\":42}]}]}")!;
        var drafts = ContentPoolComposer.Drafts(original); drafts[0].WeightText = "0.5";
        var updated = ContentPoolComposer.Build(original.Name, drafts, original);
        Assert.Equal(1, original.Groups[0].Weight); Assert.Equal(0.5, updated.Groups[0].Weight);
        Assert.True(updated.Extra!["futurePool"].GetBoolean()); Assert.Equal("keep", updated.Groups[0].Extra!["futureGroup"].GetString());
        Assert.Equal(42, updated.Groups[0].Content![0].Extra!["futureItem"].GetInt32());
        updated.Groups[0].Content![0].Sid = "changed";
        Assert.Equal("scroll_box", original.Groups[0].Content![0].Sid); Assert.Equal("scroll_box", drafts[0].Content[0].Sid);
    }

    [Fact]
    public void NativeFloatWeightSumOverflowIsRejectedEvenWhenIndividualWeightsAreFinite()
    {
        var a = new PoolGroupDraft { WeightText = "2e38" }; a.IncludeLists.Add("a");
        var b = new PoolGroupDraft { WeightText = "2e38" }; b.IncludeLists.Add("b");
        Assert.Equal("Weight", Assert.Throws<ContentPoolValidationException>(() => ContentPoolComposer.Build("name", [a, b])).Code);
    }

    [Theory]
    [InlineData("1e-50")]
    [InlineData("5e-324")]
    [InlineData("1e-400")]
    [InlineData("-1e-400")]
    [InlineData("0.0001E-400")]
    [InlineData("-0,0001E-400")]
    public void NonzeroWeightsThatBecomeNativeZeroAreRejectedForGroupsAndItems(string tiny)
    {
        var group = new PoolGroupDraft { WeightText = tiny }; group.IncludeLists.Add("list");
        Assert.Equal("Weight", Assert.Throws<ContentPoolValidationException>(() => ContentPoolComposer.Build("name", [group], culture: CultureInfo.GetCultureInfo("ru-RU"))).Code);
        group.WeightText = "1"; group.Content.Add(new() { Sid = "sid", WeightText = tiny });
        Assert.Equal("Weight", Assert.Throws<ContentPoolValidationException>(() => ContentPoolComposer.Build("name", [group], culture: CultureInfo.GetCultureInfo("ru-RU"))).Code);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0e-400")]
    [InlineData("-0")]
    [InlineData("-0.000E-400")]
    [InlineData("0,000e-400")]
    public void TrueZeroSignificandsRemainValidForDisabledGroupsAndItems(string zero)
    {
        var disabled = new PoolGroupDraft { WeightText = zero }; disabled.IncludeLists.Add("list");
        var active = new PoolGroupDraft { WeightText = "1" };
        active.Content.Add(new() { Sid = "enabled", WeightText = "1" });
        active.Content.Add(new() { Sid = "disabled", WeightText = zero });
        var pool = ContentPoolComposer.Build("name", [disabled, active], culture: CultureInfo.GetCultureInfo("ru-RU"));
        Assert.Equal(0, pool.Groups[0].Weight); Assert.Equal(0, pool.Groups[1].Content![1].Weight);
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(pool, JsonExport.Options));
        Assert.Equal(0, serialized.RootElement.GetProperty("groups")[0].GetProperty("weight").GetDouble());
        Assert.Equal(0, serialized.RootElement.GetProperty("groups")[1].GetProperty("content")[1].GetProperty("weight").GetDouble());
    }

    [Fact]
    public void SmallestPositiveNativeWeightAndDisabledZeroWeightRemainValid()
    {
        var group = new PoolGroupDraft { WeightText = ((double)float.Epsilon).ToString("R", CultureInfo.InvariantCulture) };
        group.Content.Add(new() { Sid = "tiny", WeightText = group.WeightText }); group.Content.Add(new() { Sid = "disabled", WeightText = "0" });
        var pool = ContentPoolComposer.Build("native-small", [group]);
        Assert.Equal(float.Epsilon, (float)pool.Groups[0].Weight); Assert.Equal(0, pool.Groups[0].Content![1].Weight);
    }

    [Fact]
    public void NativeContentWeightSumOverflowIsRejected()
    {
        var group = new PoolGroupDraft(); group.Content.Add(new() { Sid = "a", WeightText = "2e38" }); group.Content.Add(new() { Sid = "b", WeightText = "2e38" });
        Assert.Equal("Weight", Assert.Throws<ContentPoolValidationException>(() => ContentPoolComposer.Build("name", [group])).Code);
    }

    [Fact]
    public async Task NativeDefaultVariantSurvivesTheUnchangedEditorDraftAndEmbeddedInspectionPath()
    {
        using var fixture = new SyntheticInstalledGame();
        var catalog = await new GameCatalogService(fixture.Service(), Path.Combine(fixture.Root, "cache")).GetCatalogAsync("english");
        Assert.Equal(0, Assert.Single(catalog.Content.Single(c => c.Sid == "scroll_box").RewardVariants).Index);
        var original = JsonSerializer.Deserialize<GamePool>("""{"name":"custom_native_default","groups":[{"weight":0.75,"includeLists":["author_defaults"],"content":[{"sid":"scroll_box","variant":-1,"weight":0.5,"futureItem":"keep"},{"sid":"scroll_box","weight":0.25}]}]}""", JsonExport.Options)!;
        // The creator binds these exact draft strings to its variant editor.
        var drafts = ContentPoolComposer.Drafts(original);
        Assert.Equal("-1", drafts[0].Content[0].VariantText); Assert.Equal("", drafts[0].Content[1].VariantText);
        var edited = ContentPoolComposer.Build(original.Name, drafts, original, catalog);
        Assert.Equal(2, edited.Groups[0].Content!.Count); Assert.Equal(-1, edited.Groups[0].Content![0].Variant);
        Assert.Null(edited.Groups[0].Content![1].Variant); Assert.Equal(-1, original.Groups[0].Content![0].Variant);
        Assert.Equal("keep", edited.Groups[0].Content![0].Extra!["futureItem"].GetString());
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(edited, JsonExport.Options));
        Assert.Equal(-1, serialized.RootElement.GetProperty("groups")[0].GetProperty("content")[0].GetProperty("variant").GetInt32());
        var template = new RmgTemplate
        {
            Name = "Native default variant", SizeX = 128, SizeZ = 128, GameMode = "Classic",
            ContentPools = [serialized.RootElement.Clone()],
            ContentLists = [new ContentListEntry { Name = "author_defaults", Content = [new() { Sid = "scroll_box", Variant = -1, Weight = 0.5 }] }],
            Variants = [new() { Zones = [new() { Name = "A", GuardedContentPool = [edited.Name], MainObjects = [new() { Type = "Spawn", Spawn = "Player1" }] },
                                        new() { Name = "B", MainObjects = [new() { Type = "Spawn", Spawn = "Player2" }] }],
                Connections = [new() { Name = "AB", From = "A", To = "B", ConnectionType = "Direct" }] }],
        };
        using var zip = ZipFile.OpenRead(fixture.CorePath); var pools = GamePoolCatalog.Read(zip, catalog.Source);
        var resolved = pools.ResolvePool(edited.Name, template);
        Assert.Equal(PoolReferenceOrigin.TemplateDefinition, resolved.Origin);
        Assert.Equal(-1, resolved.Definition!.Groups[0].Content![0].Variant);
        Assert.Equal(-1, Assert.Single(pools.ResolveListReference("author_defaults", template).Definition!.Content).Variant);
        var inspection = PoolContentInspector.Inspect(resolved.Definition!, pools, template);
        Assert.Equal(3, inspection.Items.Count); Assert.All(inspection.Items, item => Assert.False(item.IsMissing));
        Assert.Equal(2, inspection.Items.Count(item => item.Variant == -1)); Assert.Single(inspection.Items, item => item.Variant is null);
        Assert.Empty(inspection.Warnings); Assert.Empty(pools.ValidateReferences(template));
        Assert.DoesNotContain(TemplateCompatibilityValidator.Validate(template, catalog: catalog.GetReferenceCatalog()), issue => issue.Severity == TemplateIssueSeverity.Error);
    }

    [Fact]
    public async Task VariantBelowNativeDefaultIsRejectedEvenWithARealRewardCatalogWithoutChangingTheOriginal()
    {
        using var fixture = new SyntheticInstalledGame();
        var catalog = await new GameCatalogService(fixture.Service(), Path.Combine(fixture.Root, "cache")).GetCatalogAsync("english");
        var original = new GamePool { Name = "custom_native_default", Groups = [new() { Weight = 1, Content = [new() { Sid = "scroll_box", Variant = -1, Weight = 1 }] }] };
        var drafts = ContentPoolComposer.Drafts(original); drafts[0].Content[0].VariantText = "-2";
        Assert.Equal("Variant", Assert.Throws<ContentPoolValidationException>(() => ContentPoolComposer.Build(original.Name, drafts, original, catalog)).Code);
        Assert.Equal(-1, original.Groups[0].Content![0].Variant);
    }

    [Fact]
    public void NativeOmittedItemWeightUsesOneAndUnchangedPoolEditingKeepsOmissionAndExplicitZero()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            SyntheticInstalledGame.Add(zip, "generator/content_pools/defaults.json", """[{"name":"custom_weight_defaults","groups":[{"weight":1,"includeLists":["defaults_list"],"content":[{"sid":"default_item","variant":-1,"futureItem":"keep"},{"sid":"disabled_item","weight":0},{"sid":"explicit_one","weight":1}]}]}]""");
            SyntheticInstalledGame.Add(zip, "generator/content_lists/defaults.json", """[{"name":"defaults_list","content":[{"sid":"list_default","futureListItem":true},{"sid":"list_disabled","weight":0}]}]""");
        }
        stream.Position = 0; using var archive = new ZipArchive(stream, ZipArchiveMode.Read); var catalog = GamePoolCatalog.Read(archive);
        Assert.Empty(catalog.Warnings); var original = Assert.Single(catalog.Pools); var list = Assert.Single(catalog.ContentLists);
        Assert.Equal(1, original.Groups[0].Content![0].Weight); Assert.Equal(0, original.Groups[0].Content![1].Weight); Assert.Equal(1, original.Groups[0].Content![2].Weight);
        Assert.Equal(1, list.Content[0].Weight); Assert.Equal(0, list.Content[1].Weight);
        string before = JsonSerializer.Serialize(original, JsonExport.Options);
        var drafts = ContentPoolComposer.Drafts(original);
        Assert.Equal(new[] { "1", "0", "1" }, drafts[0].Content.Select(row => row.WeightText));
        var edited = ContentPoolComposer.Build(original.Name, drafts, original);
        string after = JsonSerializer.Serialize(edited, JsonExport.Options);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(before), JsonNode.Parse(after))); Assert.Equal(before, JsonSerializer.Serialize(original, JsonExport.Options));
        using var parsed = JsonDocument.Parse(after); var items = parsed.RootElement.GetProperty("groups")[0].GetProperty("content");
        Assert.False(items[0].TryGetProperty("weight", out _)); Assert.Equal(0, items[1].GetProperty("weight").GetDouble()); Assert.Equal(1, items[2].GetProperty("weight").GetDouble());
        using var listJson = JsonDocument.Parse(JsonSerializer.Serialize(list, JsonExport.Options));
        Assert.False(listJson.RootElement.GetProperty("content")[0].TryGetProperty("weight", out _));
        Assert.True(listJson.RootElement.GetProperty("content")[0].GetProperty("futureListItem").GetBoolean());
        var settings = new GeneratorSettings { Extra = new() { ["rawAuthorPool"] = JsonSerializer.SerializeToElement(original, SettingsSnapshotMapper.SnapshotOptions) } };
        var snapshotClone = SettingsSnapshotMapper.Clone(settings);
        var clonedItems = snapshotClone.Extra!["rawAuthorPool"].GetProperty("groups")[0].GetProperty("content");
        Assert.False(clonedItems[0].TryGetProperty("weight", out _)); Assert.Equal(0, clonedItems[1].GetProperty("weight").GetDouble()); Assert.Equal(1, clonedItems[2].GetProperty("weight").GetDouble());
        drafts[0].Content[0].WeightText = "0.25";
        var changed = ContentPoolComposer.Build(original.Name, drafts, original);
        using var changedJson = JsonDocument.Parse(JsonSerializer.Serialize(changed, JsonExport.Options));
        Assert.Equal(0.25, changedJson.RootElement.GetProperty("groups")[0].GetProperty("content")[0].GetProperty("weight").GetDouble());
        Assert.Equal(1, original.Groups[0].Content![0].Weight); Assert.Equal(0, new PoolGroup().Weight); // no unproven group default is introduced
    }

    [Fact]
    public void ExplicitNullWeightRemainsInvalidWhileNewItemsUseTheNativeDefaultOne()
    {
        var item = new PoolContentItem { Sid = "default" }; Assert.Equal(1, item.Weight);
        using var omitted = JsonDocument.Parse(JsonSerializer.Serialize(item)); Assert.False(omitted.RootElement.TryGetProperty("weight", out _));
        item.Weight = 0; using var zero = JsonDocument.Parse(JsonSerializer.Serialize(item)); Assert.Equal(0, zero.RootElement.GetProperty("weight").GetDouble());
        item.Weight = 1; using var one = JsonDocument.Parse(JsonSerializer.Serialize(item)); Assert.Equal(1, one.RootElement.GetProperty("weight").GetDouble());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PoolContentItem>("""{"sid":"invalid","weight":null}""", JsonExport.Options));
    }
}
