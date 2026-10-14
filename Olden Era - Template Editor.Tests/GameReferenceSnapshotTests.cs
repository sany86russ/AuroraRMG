using System.IO.Compression;
using Olden_Era___Template_Editor.Services.GameData;

namespace Olden_Era___Template_Editor.Tests;

public class GameReferenceSnapshotTests
{
    [Fact]
    public void ReferenceSnapshot_UsesDefinitionsAndPreservesIdentifierWhitespace()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            SyntheticInstalledGame.Add(zip, "generator/content_pools/p.json", "[{\"name\":\"pool \",\"groups\":[]}]" );
            SyntheticInstalledGame.Add(zip, "generator/content_lists/l.json", "[{\"name\":\"list\",\"content\":[{\"sid\":\"dangling_reference\"}]}]");
            SyntheticInstalledGame.Add(zip, "generator/zone_layouts/z.json", "[{\"name\":\"layout\"}]");
            SyntheticInstalledGame.Add(zip, "DB/map/objects/objects.json", "{\"array\":[{\"id\":\"real_object\"}]}");
            SyntheticInstalledGame.Add(zip, "generator/generator_config.json", "{\"metaObjects\":[{\"sid\":\"random_hire_1\",\"type\":\"RandomHire\",\"args\":[\"1\"]}]}");
        }
        stream.Position = 0; using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var snapshot = GameReferenceSnapshotBuilder.Build(archive);
        Assert.Contains("pool ", snapshot.Pools);
        Assert.DoesNotContain("pool", snapshot.Pools);
        Assert.Contains("real_object", snapshot.Objects);
        Assert.Contains("random_hire_1", snapshot.Objects);
        Assert.DoesNotContain("dangling_reference", snapshot.Objects);
        Assert.True(snapshot.CoreReadComplete);
    }

    [Fact]
    public void ReferenceSnapshot_SchemaLocalDefinitionsNeedExplicitOptIn()
    {
        var snapshot = new GameReferenceSnapshot
        { CoreReadComplete = true, NativeMandatory = ["native_group"], NativeCountLimits = ["native_limit"], NativeLayouts = ["native_layout"] };
        var source = new GameInstallation { SourceFingerprint = "fixture" };
        var globalOnly = snapshot.ToReferenceCatalog(source);
        Assert.Empty(globalOnly.Mandatory!); Assert.Empty(globalOnly.CountLimits!);
        var includeNative = snapshot.ToReferenceCatalog(source, includeNativeSchemaDefinitions: true);
        Assert.Contains("native_group", includeNative.Mandatory!);
        Assert.Contains("native_limit", includeNative.CountLimits!);
        Assert.Null(includeNative.CustomAI);
    }

    [Fact]
    public void MalformedReferenceDomainCannotClaimCompleteSnapshot()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) SyntheticInstalledGame.Add(zip, "generator/content_pools/broken.json", "{bad");
        stream.Position = 0; using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var warnings = new List<string>(); var snapshot = GameReferenceSnapshotBuilder.Build(archive, warnings: warnings);
        Assert.False(snapshot.CoreReadComplete);
        Assert.False(snapshot.ToReferenceCatalog(new GameInstallation()).IsComplete);
        Assert.NotEmpty(warnings);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    public void InvalidGeneratorConfigRootsAreDiagnosedInsteadOfThrowing(string json)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) SyntheticInstalledGame.Add(zip, "generator/generator_config.json", json);
        stream.Position = 0; using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var warnings = new List<string>(); var snapshot = GameReferenceSnapshotBuilder.Build(archive, warnings: warnings);
        Assert.False(snapshot.CoreReadComplete); Assert.NotEmpty(warnings);
    }

    [Fact]
    public void ExactNonCampaignAiIdsAreAvailableWithoutClaimingPassiveBehavior()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            SyntheticInstalledGame.Add(zip, "DB/ai_world/ai/common.json", "{\"array\":[{\"id\":\"custom_ai_exact \",\"strategies\":[{\"roles\":[\"role\"],\"actionsBefore\":[{\"sid\":\"action\"}]}]}]}");
            SyntheticInstalledGame.Add(zip, "DB/ai_world/ai/campaign/story.json", "{\"array\":[{\"id\":\"story_ai\"}]}");
        }
        stream.Position = 0; using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var snapshot = GameReferenceSnapshotBuilder.Build(archive);
        var refs = snapshot.ToReferenceCatalog(new GameInstallation());
        Assert.Contains("custom_ai_exact ", refs.CustomAI!); Assert.DoesNotContain("story_ai", refs.CustomAI!);
        Assert.DoesNotContain("custom_ai_exact", refs.CustomAI!);
        Assert.Contains("do not verify passive", snapshot.AiEvidence);
        Assert.Null(snapshot.ToReferenceCatalog(null).CustomAI);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"array\":[{\"id\":\"same\"},{\"id\":\"same\"}]}")]
    public void InvalidAiDatabaseRetainsUnverifiedReferencesAndWarning(string json)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) SyntheticInstalledGame.Add(zip, "DB/ai_world/ai/common.json", json);
        stream.Position = 0; using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var warnings = new List<string>(); var snapshot = GameReferenceSnapshotBuilder.Build(archive, warnings: warnings);
        Assert.Null(snapshot.CustomAI); Assert.NotEmpty(warnings);
        Assert.Null(snapshot.ToReferenceCatalog(new GameInstallation()).CustomAI);
    }

    [Fact]
    public void MissingAiDomainDoesNotClaimThatTheGameAcceptsNoAiIdentifiers()
    {
        using var stream = new MemoryStream(); using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) { }
        stream.Position = 0; using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var warnings = new List<string>(); var snapshot = GameReferenceSnapshotBuilder.Build(archive, warnings: warnings);
        Assert.Null(snapshot.CustomAI); Assert.Contains(warnings, warning => warning.Contains("unverified"));
        Assert.Null(snapshot.ToReferenceCatalog(new GameInstallation()).CustomAI);
    }
}
