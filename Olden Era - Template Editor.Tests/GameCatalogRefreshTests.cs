using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Olden_Era___Template_Editor.Services.GameData;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public class GameCatalogRefreshTests
{
    [Fact]
    public async Task Catalog_RefreshesMemoryAfterCoreReplacement()
    {
        using var fixture = new SyntheticInstalledGame();
        var service = new GameCatalogService(fixture.Service(), Path.Combine(fixture.Root, "cache"));
        var old = await service.GetCatalogAsync("russian");
        Assert.Contains(old.Heroes, h => h.Sid == "human_hero_8");
        File.Delete(fixture.CorePath); fixture.WriteCore(hero: "human_hero_9");
        File.SetLastWriteTimeUtc(fixture.CorePath, DateTime.UtcNow.AddMinutes(2));
        var current = await service.GetCatalogAsync("russian");
        Assert.Contains(current.Heroes, h => h.Sid == "human_hero_9");
        Assert.DoesNotContain(current.Heroes, h => h.Sid == "human_hero_8");
        Assert.NotEqual(old.Source!.SourceFingerprint, current.Source!.SourceFingerprint);
    }

    [Fact]
    public async Task Catalog_ChangesSourceWhenManualInstallChangesAndWorksOffline()
    {
        using var a = new SyntheticInstalledGame(); using var b = new SyntheticInstalledGame();
        var locator = a.Service();
        var service = new GameCatalogService(locator, Path.Combine(a.Root, "cache"));
        Assert.Equal(a.CorePath, (await service.GetCatalogAsync("russian")).Source!.CoreZipPath);
        locator.ManualInstallPath = b.InstallPath;
        Assert.Equal(b.CorePath, (await service.GetCatalogAsync("russian")).Source!.CoreZipPath);
        locator.ManualInstallPath = Path.Combine(a.Root, "unavailable");
        var offline = await service.GetCatalogAsync("russian");
        Assert.False(offline.IsLiveSource);
        Assert.True(offline.IsEmpty);
        Assert.NotEmpty(offline.Warnings);
    }

    [Fact]
    public async Task Catalog_DiskCacheRemainsBoundToActualSource()
    {
        using var fixture = new SyntheticInstalledGame();
        string cache = Path.Combine(fixture.Root, "cache");
        var first = await new GameCatalogService(fixture.Service(), cache).GetCatalogAsync("english");
        var second = await new GameCatalogService(fixture.Service(), cache).GetCatalogAsync("english");
        Assert.Equal(first.Source!.SourceFingerprint, second.Source!.SourceFingerprint);
        Assert.Equal(first.Content.Count, second.Content.Count);
        Assert.Contains(second.Content, c => c.Sid == "scroll_box" && c.RewardVariants.Count == 1);
    }

    [Theory]
    [InlineData("heroes", "null")]
    [InlineData("Content", "null")]
    [InlineData("Warnings", "null")]
    [InlineData("Source", "null")]
    [InlineData("Source.Profile", "null")]
    [InlineData("Source.Warnings", "null")]
    [InlineData("Source.Profile.ConfirmedCapabilities", "null")]
    [InlineData("Source.Profile.ConfirmedCapabilities", "undefined-enum")]
    [InlineData("Source.Profile.Status", "undefined-number")]
    [InlineData("References", "null")]
    [InlineData("References.Pools", "null")]
    [InlineData("References.Lists", "null")]
    [InlineData("References.Layouts", "null")]
    [InlineData("References.Objects", "null")]
    [InlineData("References.NativeLayouts", "null")]
    [InlineData("References.NativeMandatory", "null")]
    [InlineData("References.NativeCountLimits", "null")]
    [InlineData("References.Pools", "null-row")]
    [InlineData("References.CustomAI", "null-row")]
    [InlineData("heroes", "null-row")]
    [InlineData("Content", "null-row")]
    [InlineData("Content.0.PlacementVerified", "true")]
    [InlineData("Content.0.RewardVariants.0.RollChance", "infinite")]
    [InlineData("Content.0.RewardVariants", "null")]
    [InlineData("Content.0.RewardVariants", "null-row")]
    [InlineData("Content.0.GrantedSpells", "null")]
    [InlineData("Content.0.RewardVariants.0.RewardTypes", "null")]
    [InlineData("Content.0.RewardVariants.0.RewardParameters", "null-row")]
    [InlineData("Content.0.RewardVariants.0.Spells", "null")]
    [InlineData("Content.0.RewardVariants.0.Spells", "null-row")]
    [InlineData("Content", "remove")]
    [InlineData("References.Layouts", "remove")]
    public async Task MalformedCacheIsRejectedWithAVisibleWarningAndRebuiltFromActualData(string path, string corruption)
    {
        using var fixture = new SyntheticInstalledGame(); string cache = Path.Combine(fixture.Root, "cache");
        var original = await new GameCatalogService(fixture.Service(), cache).GetCatalogAsync("english");
        string cacheFile = Assert.Single(Directory.GetFiles(cache, "*.json"));
        var root = JsonNode.Parse(File.ReadAllText(cacheFile))!.AsObject();
        string[] parts = path.Split('.'); JsonNode parent = root;
        foreach (string part in parts[..^1]) parent = parent is JsonArray array ? array[int.Parse(part)]! : parent[part]!;
        string name = parts[^1];
        if (corruption == "remove") parent.AsObject().Remove(name);
        else parent[name] = corruption switch
        {
            "null-row" => new JsonArray((JsonNode?)null),
            "undefined-enum" => new JsonArray(JsonValue.Create(999)),
            "undefined-number" => JsonValue.Create(999),
            "true" => JsonValue.Create(true),
            "infinite" => JsonNode.Parse("1e999"),
            _ => null,
        };
        File.WriteAllText(cacheFile, root.ToJsonString());
        var rebuilt = await new GameCatalogService(fixture.Service(), cache).GetCatalogAsync("english");
        Assert.False(rebuilt.IsEmpty); Assert.True(rebuilt.IsLiveSource);
        Assert.Equal(original.Source!.SourceFingerprint, rebuilt.Source!.SourceFingerprint);
        Assert.Equal(original.Heroes.Count, rebuilt.Heroes.Count); Assert.Equal(original.Content.Count, rebuilt.Content.Count);
        Assert.Contains(rebuilt.Warnings, warning => warning.Contains("cache was rejected", StringComparison.Ordinal));
        Assert.Contains("current_pool", rebuilt.GetReferenceCatalog().Pools!);
        Assert.Single(rebuilt.Content.Single(c => c.Sid == "scroll_box").RewardVariants);
        Assert.NotEmpty(rebuilt.Search("scroll"));
    }

    [Fact]
    public async Task ValidCacheCannotReplaceCurrentlyDiscoveredSourceCapabilityEvidence()
    {
        using var fixture = new SyntheticInstalledGame(); string cache = Path.Combine(fixture.Root, "cache");
        var actual = await new GameCatalogService(fixture.Service(), cache).GetCatalogAsync("english");
        string cacheFile = Assert.Single(Directory.GetFiles(cache, "*.json"));
        var root = JsonNode.Parse(File.ReadAllText(cacheFile))!.AsObject();
        root["heroes"]![0]!["name"] = "Retained cache display name";
        root["Source"]!["Profile"]!["Status"] = (int)GameProfileStatus.InspectedGameData;
        root["Source"]!["Profile"]!["ConfirmedCapabilities"] = new JsonArray(JsonValue.Create((int)GameCapability.Native256Maps));
        root["Source"]!["Profile"]!["Evidence"] = "Cache-only evidence";
        File.WriteAllText(cacheFile, root.ToJsonString());
        var cached = await new GameCatalogService(fixture.Service(), cache).GetCatalogAsync("english");
        Assert.Equal("Retained cache display name", Assert.Single(cached.Heroes).Name);
        Assert.Equal(actual.Source!.Profile.Status, cached.Source!.Profile.Status);
        Assert.Equal(actual.Source.Profile.Evidence, cached.Source.Profile.Evidence);
        Assert.Equal(actual.Source.Profile.ConfirmedCapabilities, cached.Source.Profile.ConfirmedCapabilities);
        Assert.False(cached.Source.Profile.Supports(GameCapability.Native256Maps));
    }

    [Fact]
    public void ContentCatalog_SearchesBilingualNamesAndSidWithoutPromisingPlacement()
    {
        using var fixture = new SyntheticInstalledGame();
        using var zip = ZipFile.OpenRead(fixture.CorePath);
        var entries = GameContentCatalogBuilder.Build(zip);
        var scroll = entries.Single(e => e.Sid == "scroll_box");
        Assert.Equal("Сундук свитков", scroll.NameRu);
        Assert.Equal("Scroll Box", scroll.NameEn);
        Assert.True(scroll.Matches("свитков"));
        Assert.True(scroll.Matches("SCROLL_BOX"));
        Assert.True(scroll.HasGeneratorConfig);
        Assert.True(scroll.ReferencedByRmgContent);
        Assert.False(scroll.PlacementVerified);
        Assert.False(entries.Single(e => e.Sid == "unreferenced_object").ReferencedByRmgContent);
        Assert.Null(scroll.Level);
        var reward = Assert.Single(scroll.RewardVariants);
        Assert.Equal(0, reward.Index);
        Assert.Equal(500, reward.Value);
        Assert.Contains("actual_scroll_artifact", reward.RewardParameters);
    }

    [Fact]
    public void ContentCatalog_ParsesSpellRankSchoolAndLocalizesByConfiguredNameKey()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            SyntheticInstalledGame.Add(archive, "DB/magics/world_day_magics.json", "{\"array\":[{\"id\":\"test_spell\",\"name\":\"real_name_key\",\"school_\":\"day\",\"rank\":3}]}");
            SyntheticInstalledGame.Add(archive, "Lang/english/texts/magic.json", "{\"tokens\":[{\"sid\":\"real_name_key\",\"text\":\"Clear View\"}]}");
        }
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var spell = Assert.Single(GameContentCatalogBuilder.Build(zip));
        Assert.Equal(GameContentKind.Spell, spell.Kind);
        Assert.Equal(3, spell.Level);
        Assert.Equal("day", spell.Category);
        Assert.Equal("Clear View", spell.NameRu); // explicitly use available localization as fallback
        Assert.True(spell.Matches("3"));
    }

    [Fact]
    public async Task ReferenceCatalogDerivesOnlyExactObservedObjectRewardIndicesWithoutChangingCacheFormat()
    {
        using var fixture = new SyntheticInstalledGame();
        using (var zip = ZipFile.Open(fixture.CorePath, ZipArchiveMode.Update))
        {
            SyntheticInstalledGame.Add(zip, "DB/objects_logic/chests/sparse.json", """{"array":[{"id":"sparse_reward ","variants":[{"value":100},null,{"value":300}]},{"id":"empty_reward","variants":[]}]}""");
            SyntheticInstalledGame.Add(zip, "DB/magics/variant_spell.json", """{"array":[{"id":"spell_variants","variants":[{"value":1}]}]}""");
            SyntheticInstalledGame.Add(zip, "DB/items/items/variant_artifact.json", """{"array":[{"id":"artifact_variants","variants":[{"value":1}]}]}""");
        }
        string cache = Path.Combine(fixture.Root, "cache");
        var catalog = await new GameCatalogService(fixture.Service(), cache).GetCatalogAsync("english");
        var map = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlySet<int>>>(catalog.GetReferenceCatalog().ObjectRewardVariantIndices);
        Assert.Equal(new[] { 0, 2 }, map["sparse_reward "].Order()); Assert.Equal(new[] { 0 }, map["scroll_box"].Order());
        Assert.False(map.ContainsKey("sparse_reward")); Assert.False(map.ContainsKey("SPARSE_REWARD "));
        Assert.False(map.ContainsKey("empty_reward")); Assert.False(map.ContainsKey("spell_variants")); Assert.False(map.ContainsKey("artifact_variants"));
        Assert.DoesNotContain(-1, map["sparse_reward "]); Assert.DoesNotContain(1, map["sparse_reward "]);
        catalog.Content.Single(entry => entry.Sid == "sparse_reward ").RewardVariants[0].Index = 7;
        Assert.Equal(new[] { 0, 2 }, map["sparse_reward "].Order()); // the injected validation snapshot is detached from mutable catalogue rows
        Assert.Equal(new[] { 2, 7 }, catalog.GetReferenceCatalog().ObjectRewardVariantIndices!["sparse_reward "].Order());
        Assert.Equal(4, catalog.SnapshotFormatVersion);
        using var stored = JsonDocument.Parse(File.ReadAllText(Assert.Single(Directory.GetFiles(cache, "*.json"))));
        Assert.False(stored.RootElement.GetProperty("References").TryGetProperty("ObjectRewardVariantIndices", out _));
        Assert.Null(new GameCatalog().GetReferenceCatalog().ObjectRewardVariantIndices);
    }

    [Fact]
    public void ScrollVariantRanksFollowActualArtifactSpellLinksAndGeneratorAliasesAreListed()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            SyntheticInstalledGame.Add(zip, "DB/magics/spells.json", "{\"array\":[{\"id\":\"real_spell\",\"school_\":\"day\",\"rank\":3}]}");
            SyntheticInstalledGame.Add(zip, "DB/items/items/scroll.json", "{\"array\":[{\"id\":\"real_scroll\",\"bonuses\":[{\"type\":\"heroMagicAddition\",\"parameters\":[\"real_spell\"]}]}]}");
            SyntheticInstalledGame.Add(zip, "DB/objects_logic/chests/scroll_box.json", "{\"array\":[{\"id\":\"scroll_box\",\"variants\":[{\"rewardSet\":{\"rewards\":[{\"rewardType\":\"HeroItemReward\",\"parameters\":[\"real_scroll\"]}]}}]}]}");
            SyntheticInstalledGame.Add(zip, "generator/generator_config.json", "{\"metaObjects\":[{\"sid\":\"random_hire_4\",\"type\":\"RandomHire\",\"args\":[\"4\"]}]}");
        }
        stream.Position = 0; using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entries = GameContentCatalogBuilder.Build(archive);
        var box = entries.Single(e => e.Sid == "scroll_box");
        Assert.Null(box.Level); // never invent a level property on the map object itself
        var linked = Assert.Single(Assert.Single(box.RewardVariants).Spells);
        Assert.Equal("real_spell", linked.Sid); Assert.Equal(3, linked.Rank); Assert.True(box.Matches("3"));
        Assert.Equal(4, entries.Single(e => e.Sid == "random_hire_4").Level);
    }

    [Fact]
    public void ContentCatalog_MalformedEntryIsDiagnosedWhileValidDataSurvives()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            SyntheticInstalledGame.Add(archive, "DB/magics/bad.json", "{broken");
            SyntheticInstalledGame.Add(archive, "DB/items/items/good.json", "{\"array\":[{\"id\":\"good\",\"maxLevel\":1}]}");
        }
        stream.Position = 0; using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var warnings = new List<string>();
        Assert.Single(GameContentCatalogBuilder.Build(zip, warnings));
        Assert.Contains(warnings, w => w.Contains("DB/magics/bad.json"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    public void ContentCatalog_UnrecognizedRootShapesAreDiagnosedWithoutThrowing(string json)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            SyntheticInstalledGame.Add(zip, "DB/magics/odd.json", json);
            SyntheticInstalledGame.Add(zip, "Lang/english/texts/magic.json", json);
        }
        stream.Position = 0; using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var warnings = new List<string>();
        Assert.Empty(GameContentCatalogBuilder.Build(archive, warnings));
        Assert.Equal(2, warnings.Count);
    }
}

public class GamePoolCatalogTests
{
    private static GamePoolCatalog Catalog() => new()
    {
        Source = new GameInstallation { InstallPath = "synthetic", SourceFingerprint = "synthetic" },
        Pools = [new GamePool { Name = "current_pool", Groups = [new PoolGroup { Weight = 100, IncludeLists = ["current_list"] }] }],
        ContentLists = [new ContentListEntry { Name = "current_list", Content = [new PoolContentItem { Sid = "installed_object", Weight = 100 }] }]
    };
    private static RmgTemplate Template(string sid) => new()
    { Variants = [new Variant { Zones = [new Zone { GuardedContentPool = [sid] }] }] };

    [Fact]
    public void ReferenceResolution_PrefersTemplateThenLocalThenCurrentGame()
    {
        var catalog = Catalog(); var template = Template("current_pool");
        var local = new GamePool { Name = "current_pool" };
        Assert.Equal(PoolReferenceOrigin.InstalledGame, catalog.ResolvePool("current_pool").Origin);
        Assert.Equal(PoolReferenceOrigin.LocalDefinition, catalog.ResolvePool("current_pool", template, [local]).Origin);
        template.ContentPools = [new GamePool { Name = "current_pool" }];
        Assert.Equal(PoolReferenceOrigin.TemplateDefinition, catalog.ResolvePool("current_pool", template, [local]).Origin);
    }

    [Fact]
    public void MissingLegacyPoolIsWarnedWithoutGuessedRemappingAndEmbeddedDefinitionResolvesIt()
    {
        var catalog = Catalog(); var template = Template("removed_legacy_pool");
        Assert.Equal(PoolReferenceOrigin.Missing, catalog.ResolvePool("removed_legacy_pool", template).Origin);
        Assert.Contains(catalog.ValidateReferences(template), w => w.Contains("no replacement was guessed"));
        template.ContentPools = [JsonSerializer.SerializeToElement(new GamePool { Name = "removed_legacy_pool" })];
        Assert.Empty(catalog.ValidateReferences(template));
    }

    [Fact]
    public void EmbeddedContentListsOverrideGameDefinitionsAndDependenciesAreChecked()
    {
        var catalog = Catalog(); var template = Template("current_pool");
        var embedded = new ContentListEntry { Name = "current_list", Content = [new PoolContentItem { Sid = "author_object", Weight = 10 }] };
        template.ContentLists = [JsonSerializer.SerializeToElement(embedded)];
        Assert.Equal("author_object", Assert.Single(catalog.ResolveList("current_list", template)!.Content).Sid);
        template.ContentPools = [new GamePool { Name = "current_pool", Groups = [new PoolGroup { IncludeLists = ["missing_list"] }] }];
        Assert.Contains(catalog.ValidateReferences(template), w => w.Contains("missing_list"));
    }

    [Fact]
    public void OfflineResolutionReportsUnavailableRatherThanRemoved()
    {
        var catalog = new GamePoolCatalog();
        Assert.Equal(PoolReferenceOrigin.GameUnavailable, catalog.ResolvePool("unknown").Origin);
        Assert.Contains(catalog.ValidateReferences(Template("unknown")), w => w.Contains("cannot be checked"));
    }

    [Fact]
    public void ExplicitEmbeddingIncludesDependenciesAndSeversAliases()
    {
        var catalog = Catalog(); var template = Template("current_pool");
        catalog.EmbedSelected(template, ["current_pool"]);
        Assert.Single(template.ContentPools!); Assert.Single(template.ContentLists!);
        catalog.Pools[0].Groups[0].Weight = 1;
        Assert.Equal(100, catalog.ResolvePool("current_pool", template).Definition!.Groups[0].Weight);
        Assert.Empty(catalog.ValidateReferences(template));
    }

    [Fact]
    public void EmbeddingPreflightPreventsPartialMutationForMissingDependency()
    {
        var catalog = Catalog(); var template = Template("current_pool");
        catalog.Pools.Add(new GamePool { Name = "bad_pool", Groups = [new PoolGroup { IncludeLists = ["missing"] }] });
        Assert.Throws<InvalidOperationException>(() => catalog.EmbedSelected(template, ["current_pool", "bad_pool"]));
        Assert.Null(template.ContentPools); Assert.Null(template.ContentLists);
    }

    [Fact]
    public void PoolReaderKeepsExtraFieldsAndReportsMalformedData()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            SyntheticInstalledGame.Add(archive, "generator/content_pools/current.json", "[{\"name\":\"pool\",\"futureField\":true,\"groups\":[]}]" );
            SyntheticInstalledGame.Add(archive, "generator/content_lists/bad.json", "{broken");
        }
        stream.Position = 0; using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var catalog = GamePoolCatalog.Read(zip);
        Assert.True(Assert.Single(catalog.Pools).Extra!["futureField"].GetBoolean());
        Assert.Contains(catalog.Warnings, w => w.Contains("bad.json"));
    }

    [Fact]
    public void FractionalNativeWeightsRoundTripWithoutTruncation()
    {
        var pool = JsonSerializer.Deserialize<GamePool>("{\"name\":\"fractional\",\"groups\":[{\"weight\":0.25,\"content\":[{\"sid\":\"scroll_box\",\"weight\":1.75}]}]}")!;
        Assert.Equal(0.25, pool.Groups[0].Weight); Assert.Equal(1.75, pool.Groups[0].Content![0].Weight);
        var roundTrip = JsonSerializer.Deserialize<GamePool>(JsonSerializer.Serialize(pool))!;
        Assert.Equal(0.25, roundTrip.Groups[0].Weight); Assert.Equal(1.75, roundTrip.Groups[0].Content![0].Weight);
    }

    [Fact]
    public void AuthorDefinitionSupportsScalarListFieldsAndActualVariantIndex()
    {
        var template = Template("current_pool");
        using var document = JsonDocument.Parse("{\"name\":\"current_pool\",\"groups\":{\"weight\":0.5,\"includeLists\":\"current_list\",\"content\":[{\"sid\":\"scroll_box\",\"variant\":2,\"weight\":1.5}]}}");
        template.ContentPools = [document.RootElement.Clone()];
        var resolution = Catalog().ResolvePool("current_pool", template);
        Assert.Equal(PoolReferenceOrigin.TemplateDefinition, resolution.Origin);
        var group = Assert.Single(resolution.Definition!.Groups);
        Assert.Equal("current_list", Assert.Single(group.IncludeLists));
        Assert.Equal(2, Assert.Single(group.Content!).Variant);
    }

    [Fact]
    public void MalformedAuthorDefinitionCannotSilentlyFallBackToInstalledPool()
    {
        var template = Template("current_pool");
        using var document = JsonDocument.Parse("{\"name\":\"current_pool\",\"groups\":[{\"weight\":\"invalid\"}]}");
        template.ContentPools = [document.RootElement.Clone()];
        var catalog = Catalog(); var resolution = catalog.ResolvePool("current_pool", template);
        Assert.Equal(PoolReferenceOrigin.InvalidTemplateDefinition, resolution.Origin);
        Assert.False(resolution.IsResolved);
        Assert.Contains(catalog.ValidateReferences(template), w => w.Contains("is invalid"));
    }

    [Fact]
    public void NullCustomPoolEntriesAreDiagnosedAndNamedDefinitionsSurvive()
    {
        var parsed = JsonSerializer.Deserialize<List<GamePool>>("[null,{\"name\":\"\"},{\"name\":\"valid\",\"groups\":[]}]" )!;
        var warnings = new List<string>();
        var pools = GamePoolDataLoader.FilterCustomPools(parsed, warnings);
        Assert.Equal("valid", Assert.Single(pools).Name); Assert.Equal(2, warnings.Count);
    }
}
