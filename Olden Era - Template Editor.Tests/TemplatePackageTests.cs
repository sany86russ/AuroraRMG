using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Export;
using Olden_Era___Template_Editor.Services.Analysis;
using Olden_Era___Template_Editor.Services.GameData;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public class TemplatePackageTests
{
    private static TemplatePackageRequest Request()
    {
        var settings = new GeneratorSettings { Seed = 7, TemplateName = "Пакет проверки", PlayerCount = 2, MapSize = 128 };
        return new(TemplateGenerator.Generate(settings), SettingsSnapshotMapper.FromGeneratorSettings(settings)) { TemplateSeed = 7, GameSeed = null };
    }
    private static byte[] Read(ZipArchive zip, string name)
    { using var stream = zip.GetEntry(name)!.Open(); using var memory = new MemoryStream(); stream.CopyTo(memory); return memory.ToArray(); }

    [Fact]
    public async Task PackageContainsExactTemplateHashSettingsAndBilingualInstructions()
    {
        using var fixture = new SyntheticInstalledGame(); string path = Path.Combine(fixture.Root, "package.zip");
        var request = Request(); var receipt = await TemplatePackageService.CreateAsync(path, request);
        using var zip = ZipFile.OpenRead(path);
        Assert.Equal(new[] { "manifest.json", "run.en.txt", "run.ru.txt", "settings.oetgs", "template.rmg.json" }, zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
        byte[] template = Read(zip, "template.rmg.json");
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(request.Template, JsonExport.Options), template);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(template)), receipt.TemplateSha256);
        Assert.Equal(7, receipt.Manifest.TemplateSeed); Assert.Null(receipt.Manifest.GameSeed);
        Assert.False(receipt.Manifest.GamePlayabilityVerified);
        Assert.Contains("выбирается отдельно в игре", Encoding.UTF8.GetString(Read(zip, "run.ru.txt")));
        Assert.Contains("selected separately in game", Encoding.UTF8.GetString(Read(zip, "run.en.txt")));
        Assert.DoesNotContain(fixture.InstallPath, Encoding.UTF8.GetString(Read(zip, "manifest.json")));
        Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains("Core.zip") || e.FullName.Contains("dump"));
    }

    [Fact]
    public async Task PackageCanIncludeBoundedPngAndExplicitSeparateGameSeed()
    {
        using var fixture = new SyntheticInstalledGame(); string path = Path.Combine(fixture.Root, "package.zip");
        var request = Request() with { GameSeed = 9876, PreviewPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=") };
        var receipt = await TemplatePackageService.CreateAsync(path, request);
        using var zip = ZipFile.OpenRead(path);
        Assert.Equal(request.PreviewPng, Read(zip, "preview.png"));
        Assert.Equal(9876, receipt.Manifest.GameSeed);
    }

    [Fact]
    public async Task InvalidTemplateOrPreviewPreservesExistingPackage()
    {
        using var fixture = new SyntheticInstalledGame(); string path = Path.Combine(fixture.Root, "package.zip");
        File.WriteAllText(path, "keep existing result"); var invalid = Request(); invalid.Template.Name = "";
        await Assert.ThrowsAsync<InvalidOperationException>(() => TemplatePackageService.CreateAsync(path, invalid));
        Assert.Equal("keep existing result", File.ReadAllText(path));
        await Assert.ThrowsAsync<InvalidDataException>(() => TemplatePackageService.CreateAsync(path, Request() with { PreviewPng = [1, 2, 3] }));
        Assert.Equal("keep existing result", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(fixture.Root, ".aurora-package-*.tmp"));
    }

    [Fact]
    public async Task CancelledPackageDoesNotReplaceExistingDestination()
    {
        using var fixture = new SyntheticInstalledGame(); string path = Path.Combine(fixture.Root, "package.zip");
        File.WriteAllText(path, "existing"); using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => TemplatePackageService.CreateAsync(path, Request(), cancelled.Token));
        Assert.Equal("existing", File.ReadAllText(path));
    }

    [Fact]
    public async Task PackageUsesDerivedRewardIndexMetadataAndBlocksAnUnknownIndexBeforeReplacingTheArchive()
    {
        using var fixture = new SyntheticInstalledGame(); string path = Path.Combine(fixture.Root, "package.zip");
        var catalog = await new GameCatalogService(fixture.Service(), Path.Combine(fixture.Root, "cache")).GetCatalogAsync("english");
        var item = new ContentItem { Sid = "scroll_box", Variant = 0 };
        var template = new RmgTemplate
        {
            Name = "Configured reward variant", GameMode = "Classic", SizeX = 128, SizeZ = 128,
            MandatoryContent = [new() { Name = "required_reward", Content = [item] }],
            Variants = [new() { Zones = [new() { Name = "A", MandatoryContent = ["required_reward"], MainObjects = [new() { Type = "Spawn", Spawn = "Player1" }] },
                                        new() { Name = "B", MainObjects = [new() { Type = "Spawn", Spawn = "Player2" }] }],
                Connections = [new() { Name = "AB", From = "A", To = "B", ConnectionType = "Direct" }] }],
        };
        var request = new TemplatePackageRequest(template, new SettingsFile()) { ValidationCatalog = catalog.GetReferenceCatalog() };
        await TemplatePackageService.CreateAsync(path, request);
        byte[] originalArchive = File.ReadAllBytes(path); item.Variant = 999;
        var issue = Assert.Single(TemplateCompatibilityValidator.Validate(template, catalog: request.ValidationCatalog), finding => finding.Code == "content.unknownVariant");
        Assert.Equal(TemplateIssueSeverity.Error, issue.Severity);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => TemplatePackageService.CreateAsync(path, request));
        Assert.Contains("configured reward variant index 999", error.Message, StringComparison.Ordinal);
        Assert.Equal(originalArchive, File.ReadAllBytes(path)); Assert.Equal(999, item.Variant);
        Assert.Empty(Directory.GetFiles(fixture.Root, ".aurora-package-*.tmp"));
    }

    [Fact]
    public async Task GenerationProfileAndInstalledDataAuditAreDistinctEvenIfProjectedSettingsWereEdited()
    {
        using var fixture = new SyntheticInstalledGame(); string path = Path.Combine(fixture.Root, "package.zip");
        var settings = Request().Settings;
        settings.ProfileOptions.ProfileId = GenerationProfileOptions.CurrentProfile;
        var profile = new GameCompatibilityProfile("inspected-fixture", GameProfileStatus.InspectedGameData, [], "fixture evidence");
        var receipt = await TemplatePackageService.CreateAsync(path, Request() with { Settings = settings, Profile = profile });
        Assert.Equal(GenerationProfileOptions.LegacyProfile, receipt.Manifest.GenerationProfileId);
        Assert.Equal("inspected-fixture", receipt.Manifest.GameDataProfileId);
        Assert.Equal(receipt.Manifest.GameDataProfileId, receipt.Manifest.ProfileId);
        using var zip = ZipFile.OpenRead(path);
        Assert.Contains("Aurora generation profile: " + GenerationProfileOptions.LegacyProfile, Encoding.UTF8.GetString(Read(zip, "run.en.txt")));
        Assert.Contains("Профиль проверки данных игры: inspected-fixture", Encoding.UTF8.GetString(Read(zip, "run.ru.txt")));
    }

    [Theory]
    [InlineData("access_token")]
    [InlineData("CLIENT-SECRET")]
    [InlineData("password.privateKey")]
    [InlineData("password=synthetic-key-secret")]
    public async Task ExactSettingsWithKnownNativeCredentialsCannotBeSharedAccidentally(string key)
    {
        using var fixture = new SyntheticInstalledGame(); string path = Path.Combine(fixture.Root, "package.zip"); File.WriteAllText(path, "existing");
        var input = new GeneratorSettings { Seed = 7 };
        input.ProfileOptions.ImportedNativeSchema = new System.Text.Json.Nodes.JsonObject { [key] = "synthetic-private" };
        var request = Request() with { Settings = SettingsSnapshotMapper.FromGeneratorSettings(input) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => TemplatePackageService.CreateAsync(path, request));
        Assert.Equal("existing", File.ReadAllText(path)); Assert.Equal("synthetic-private", input.ProfileOptions.ImportedNativeSchema[key]!.GetValue<string>());
    }

    [Fact]
    public async Task SettingsMayBeExplicitlyExcludedWithoutRedactingTheReproducibleInputs()
    {
        using var fixture = new SyntheticInstalledGame(); string path = Path.Combine(fixture.Root, "package.zip");
        var input = new GeneratorSettings { Seed = 7 }; input.ProfileOptions.ImportedNativeConfiguration = new System.Text.Json.Nodes.JsonObject { ["client_secret"] = "synthetic-private" };
        var request = Request() with { Settings = SettingsSnapshotMapper.FromGeneratorSettings(input), IncludeSettings = false };
        var receipt = await TemplatePackageService.CreateAsync(path, request);
        using var zip = ZipFile.OpenRead(path); Assert.Null(zip.GetEntry("settings.oetgs")); Assert.Null(receipt.Manifest.SettingsFile); Assert.Null(receipt.Manifest.SettingsSha256);
        Assert.DoesNotContain("synthetic-private", string.Join("\n", zip.Entries.Select(entry => Encoding.UTF8.GetString(Read(zip, entry.FullName)))));
        Assert.Contains("not included", Encoding.UTF8.GetString(Read(zip, "run.en.txt")));
    }

    [Fact]
    public async Task BenignExactSettingsAndExplicitAuthorPathMetadataRemainUnchanged()
    {
        using var fixture = new SyntheticInstalledGame(); string path = Path.Combine(fixture.Root, "package.zip");
        var input = new GeneratorSettings { Seed = 7 }; input.ProfileOptions.AuthorDescription = "My map source: C:\\Author Folder\\own-map";
        input.ProfileOptions.ImportedNativeSchema = new System.Text.Json.Nodes.JsonObject { ["futureBudget"] = 0.125 };
        var request = Request() with { Settings = SettingsSnapshotMapper.FromGeneratorSettings(input) };
        await TemplatePackageService.CreateAsync(path, request); using var zip = ZipFile.OpenRead(path);
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(request.Settings, JsonExport.Options), Read(zip, "settings.oetgs"));
    }
}
