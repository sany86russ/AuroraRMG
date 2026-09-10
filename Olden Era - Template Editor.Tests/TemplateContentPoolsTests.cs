using System.Text.Json;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services;

namespace Olden_Era___Template_Editor.Tests;

public class TemplateContentPoolsTests
{
    [Fact]
    public void CustomPool_IsEmbeddedWithGameFieldNames_AndSurvivesRoundTrip()
    {
        var template = new RmgTemplate { Variants = [new Variant { Zones = [new Zone { Name = "N", GuardedContentPool = ["custom_test"] }] }] };
        var pool = new GamePool { Name = "custom_test", Groups = [new PoolGroup { Weight = 1, IncludeLists = ["basic_content_list_basic_mines"] }],
            Extra = new() { ["futureOption"] = JsonSerializer.SerializeToElement(7) } };
        TemplateContentPools.EmbedSelected(template, ["custom_test"], [pool]);
        pool.Groups[0].IncludeLists.Clear();
        var restored = JsonSerializer.Deserialize<RmgTemplate>(JsonSerializer.Serialize(template, JsonExport.Options), JsonExport.Options)!;
        var definition = (JsonElement)Assert.Single(restored.ContentPools!);
        Assert.Equal("custom_test", definition.GetProperty("name").GetString());
        Assert.Equal("basic_content_list_basic_mines", definition.GetProperty("groups")[0].GetProperty("includeLists")[0].GetString());
        Assert.Equal(7, definition.GetProperty("futureOption").GetInt32());
        Assert.Equal("custom_test", Assert.Single(TemplateContentPools.Names(restored)));
        Assert.Equal("basic_content_list_basic_mines", Assert.Single(TemplateContentPools.Definitions(restored)).Groups[0].IncludeLists[0]);
        Assert.False(definition.TryGetProperty("Name", out _));
    }

    [Fact]
    public void RepeatedSelection_PreservesExistingDefinition_AndDoesNotEmbedUnusedPools()
    {
        var template = new RmgTemplate();
        var pool = new GamePool { Name = "custom_first" };
        TemplateContentPools.Add(template, pool);
        string original = JsonSerializer.Serialize(template, JsonExport.Options);
        TemplateContentPools.EmbedSelected(template, ["custom_first"], [pool, new GamePool { Name = "custom_unused" }]);
        Assert.Equal(original, JsonSerializer.Serialize(template, JsonExport.Options));
        Assert.Throws<InvalidOperationException>(() => TemplateContentPools.Add(template, pool));
        Assert.Equal(original, JsonSerializer.Serialize(template, JsonExport.Options));
    }

    [Fact]
    public void LibraryRead_PreservesUnknownGamePoolAndGroupFields()
    {
        var pool = JsonSerializer.Deserialize<GamePool>("""{"name":"custom_x","valueDistribution":{"weights":[1]},"groups":[{"weight":1,"includeLists":["x"],"futureGroup":3}]}""")!;
        var json = JsonSerializer.SerializeToElement(pool, JsonExport.Options);
        Assert.True(json.TryGetProperty("valueDistribution", out _));
        Assert.Equal(3, json.GetProperty("groups")[0].GetProperty("futureGroup").GetInt32());
    }
}
