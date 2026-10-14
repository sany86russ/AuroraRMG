using System.Text.Json;
using System.Security.Cryptography;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Analysis;
using Olden_Era___Template_Editor.Services.GameData;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Models;

namespace Olden_Era___Template_Editor.Tests;

public sealed class PatchAcceptanceSuiteTests
{
    [Fact]
    public void CorpusComparisonAcceptsOnlyTypedCollapsedArraysAndNumericStringArguments()
    {
        const string source = """{"name":"Exact ","sizeX":128,"sizeZ":128,"mandatoryContent":{"name":"Mine","content":{"sid":"mine","rules":{"type":"MainObject","args":[0]}}},"variants":{"zones":{"name":"A","size":1,"randomHireInitialUnitIncrement":1,"randomHireEnableWeeklyUnitIncrement":true}}}""";
        var template = JsonSerializer.Deserialize<RmgTemplate>(source, JsonExport.Options)!;
        var receipt = TemplateRoundTripAudit.Compare(source, JsonSerializer.Serialize(template, JsonExport.Options));
        Assert.True(receipt.PreservesValues, string.Join("; ", receipt.Differences));
        Assert.True(receipt.Normalizations.Count >= 6);
    }

    [Fact]
    public void UnknownValuesNamesAndEveryVariantMustRemainExact()
    {
        const string source = """{"name":"Exact ","sizeX":128,"sizeZ":128,"future":1,"variants":[{"zones":[{"name":"A ","size":1}]},{"zones":[{"name":"B","size":1,"newField":{"values":[true,3,"text"]}}]}]}""";
        var changed = source.Replace("\"Exact \",", "\"Exact\",", StringComparison.Ordinal).Replace("\"future\":1", "\"future\":[1]", StringComparison.Ordinal).Replace("\"values\":[true,3,\"text\"]", "\"values\":[true,4,\"text\"]", StringComparison.Ordinal);
        var receipt = TemplateRoundTripAudit.Compare(source, changed);
        Assert.False(receipt.PreservesValues); Assert.Equal(3, receipt.Differences.Count);
        Assert.Contains(receipt.Differences, item => item.Path.StartsWith("$.variants[1]", StringComparison.Ordinal));
    }

    [Fact]
    public void OptionalModeledNullCanBeOmittedButUnknownNullCannotDisappear()
    {
        var accepted = TemplateRoundTripAudit.Compare("""{"name":"Test","sizeX":128,"sizeZ":128,"gameRules":null}""", """{"name":"Test","sizeX":128,"sizeZ":128}""");
        Assert.True(accepted.PreservesValues); Assert.Single(accepted.Normalizations);
        Assert.False(TemplateRoundTripAudit.Compare("""{"name":"Test","sizeX":128,"sizeZ":128,"future":null}""", """{"name":"Test","sizeX":128,"sizeZ":128}""").PreservesValues);
        Assert.False(TemplateRoundTripAudit.Compare("""{"mandatoryContent":null}""", """{"mandatoryContent":[null]}""").PreservesValues);
    }

    [Fact]
    public void NonfiniteTypedNumbersAndChangedHighPrecisionUnknownNumbersCannotPassPreservation()
    {
        Assert.False(TemplateRoundTripAudit.Compare("""{"variants":[{"zones":[{"guardWeeklyIncrement":1e309}]}]}""", """{"variants":[{"zones":[{"guardWeeklyIncrement":1e310}]}]}""").PreservesValues);
        Assert.False(TemplateRoundTripAudit.Compare("""{"future":0.1234567890123456789012345678901}""", """{"future":0.1234567890123456789012345678902}""").PreservesValues);
    }

    [Fact]
    public void CorpusIncludesNestedTemplatesWithDistinctRelativePaths()
    {
        string root = Path.Combine(Path.GetTempPath(), "aurora-corpus-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "nested")); File.WriteAllText(Path.Combine(root, "same.rmg.json"), "{}");
            File.WriteAllText(Path.Combine(root, "nested", "same.rmg.json"), "invalid");
            var files = PatchAcceptanceSuite.EnumerateCorpus(root); Assert.Equal(2, files.Length);
            Assert.Equal(2, files.Select(file => Path.GetRelativePath(root, file)).Distinct(StringComparer.Ordinal).Count());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task UnavailableAiPrerequisiteProducesAFailedReceiptInsteadOfAnEmptySuccessfulDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "aurora-prerequisite-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            string game = Path.Combine(root, "game"); string templates = Path.Combine(game, "templates"); Directory.CreateDirectory(templates);
            string core = Path.Combine(game, "Core.zip"), schema = Path.Combine(game, "Default.mrmg.json");
            File.WriteAllText(core, "synthetic"); File.WriteAllText(schema, "{}"); var info = new FileInfo(core);
            var source = new GameInstallation { InstallPath = game, CoreZipPath = core, SchemaPath = schema, TemplatesPath = templates,
                SourceLength = info.Length, SourceStamp = info.LastWriteTimeUtc.Ticks, SourceFingerprint = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(core))), SchemaFingerprint = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(schema))) };
            var catalog = new GameCatalog { Source = source, References = new() { CoreReadComplete = true } };
            string output = Path.Combine(root, "out");
            var exception = await Assert.ThrowsAsync<PatchAcceptanceException>(() => PatchAcceptanceSuite.RunAsync(output, catalog));
            Assert.False(exception.Receipt.OfflinePassed); Assert.Equal("suite-prerequisite", Assert.Single(exception.Receipt.Generation).Id);
            Assert.True(File.Exists(Path.Combine(output, "acceptance-report.json")));
            File.WriteAllText(schema, "{\"changed\":true}");
            var changed = await Assert.ThrowsAsync<PatchAcceptanceException>(() => PatchAcceptanceSuite.RunAsync(Path.Combine(root, "changed"), catalog));
            Assert.Contains("fingerprint", Assert.Single(changed.Receipt.Generation).Error!, StringComparison.OrdinalIgnoreCase);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void RequestedAlliancesAndVictoryActivationCannotPassOnlyByKeepingSlotCounts()
    {
        var settings = new GeneratorSettings { Seed = 4, PlayerCount = 4, PlayerStartsWithCastles = true,
            ProfileOptions = GenerationProfileOptions.CurrentDefaults(), ZoneCfg = new() { NeutralZoneCount = 8 } };
        settings.ProfileOptions.Tempo = MapTempo.Legacy;
        settings.ProfileOptions.Alliances = new() { Enabled = true, Preset = "2v2", Layout = AllianceLayout.TeamRegions };
        var template = TemplateGenerator.Generate(settings);
        foreach (var slot in template.SpawnInfos!) slot.AllianceIndex = -1;
        Assert.Throws<InvalidOperationException>(() => PatchAcceptanceSuite.VerifySettingsContract(template, settings));
        var capital = new GeneratorSettings { Seed = 5, GameEndConditions = new() { VictoryCondition = "win_condition_3" }, ProfileOptions = GenerationProfileOptions.CurrentDefaults(), ZoneCfg = new() { NeutralZoneCount = 2 } };
        capital.ProfileOptions.Tempo = MapTempo.Legacy;
        var held = TemplateGenerator.Generate(capital); held.GameRules!.WinConditions!.LostStartCity = false;
        Assert.Throws<InvalidOperationException>(() => PatchAcceptanceSuite.VerifySettingsContract(held, capital));
    }

    [Fact]
    public async Task CancelledSuiteSavesAPartialReceiptAndNeverTouchesTheInstalledCorpus()
    {
        string root = Path.Combine(Path.GetTempPath(), "aurora-acceptance-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            string game = Path.Combine(root, "game"); string templates = Path.Combine(game, "templates"); Directory.CreateDirectory(templates);
            string core = Path.Combine(game, "Core.zip"); File.WriteAllText(core, "synthetic");
            string sourcePath = Path.Combine(templates, "original.rmg.json"); const string original = "{\"name\":\"Untouched\",\"sizeX\":128,\"sizeZ\":128}"; File.WriteAllText(sourcePath, original);
            var info = new FileInfo(core);
            var catalog = new GameCatalog { Source = new() { InstallPath = game, TemplatesPath = templates, CoreZipPath = core, SourceStamp = info.LastWriteTimeUtc.Ticks, SourceLength = info.Length },
                References = new() { CoreReadComplete = true, CustomAI = ["multi_hero_normal"] } };
            string output = Path.Combine(root, "output"); using var token = new CancellationTokenSource(); token.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PatchAcceptanceSuite.RunAsync(output, catalog, token.Token));
            Assert.Equal(original, File.ReadAllText(sourcePath));
            using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "acceptance-report.json")));
            Assert.True(report.RootElement.GetProperty("Cancelled").GetBoolean()); Assert.False(report.RootElement.GetProperty("OfflinePassed").GetBoolean());
            Assert.DoesNotContain(Directory.GetFiles(templates), file => file != sourcePath);
            await Assert.ThrowsAsync<InvalidOperationException>(() => PatchAcceptanceSuite.RunAsync(Path.Combine(game, "forbidden"), catalog));
            await Assert.ThrowsAsync<IOException>(() => PatchAcceptanceSuite.RunAsync(output, catalog));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
