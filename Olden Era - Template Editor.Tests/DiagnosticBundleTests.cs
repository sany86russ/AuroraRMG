using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Export;
using Olden_Era___Template_Editor.Services.GameData;
using Olden_Era___Template_Editor.Services.NativeSchema;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public class DiagnosticBundleTests
{
    private static string Read(ZipArchive zip, string name)
    { using var reader = new StreamReader(zip.GetEntry(name)!.Open()); return reader.ReadToEnd(); }
    [Fact]
    public async Task BundleIncludesOnlyExplicitLogsAndSanitizesSecretsAndMachinePaths()
    {
        using var fixture = new SyntheticInstalledGame();
        string chosen = Path.Combine(fixture.Root, "selected.log"), unselected = Path.Combine(fixture.Root, "unselected.log"), archive = Path.Combine(fixture.Root, "diagnostics.zip");
        File.WriteAllText(chosen, "Seed=123\npassword=synthetic-password\ntoken=synthetic-token\nAuthorization: Bearer abcdef123456\n" +
            "path=C:\\Users\\FixtureOwner\\private\\file.json\nowner=person@example.com\n");
        File.WriteAllText(unselected, "THIS_WAS_NOT_SELECTED");
        var template = new RmgTemplate
        {
            Name = "Seed fixture", Extra = new() { ["password"] = JsonSerializer.SerializeToElement("synthetic-nested-secret") },
            Description = "Data from C:\\Users\\FixtureOwner\\private\\file.json"
        };
        var receipt = await DiagnosticBundleService.CreateAsync(archive, new()
        {
            Template = template, Settings = new SettingsFile(), SelectedLogPaths = [chosen], TemplateSeed = 123,
            Installation = new GameInstallation { InstallPath = fixture.InstallPath, CoreZipPath = fixture.CorePath, SteamBuildId = "25672315", VersionMarker = "0.81.04", SourceFingerprint = new string('A', 64) }
        });
        using var zip = ZipFile.OpenRead(archive);
        string contents = string.Join("\n", zip.Entries.Select(e => Read(zip, e.FullName)));
        Assert.DoesNotContain("synthetic-password", contents); Assert.DoesNotContain("synthetic-token", contents);
        Assert.DoesNotContain("abcdef123456", contents); Assert.DoesNotContain("synthetic-nested-secret", contents);
        Assert.DoesNotContain("FixtureOwner", contents); Assert.DoesNotContain("person@example.com", contents);
        Assert.DoesNotContain(fixture.InstallPath, contents); Assert.DoesNotContain("THIS_WAS_NOT_SELECTED", contents);
        Assert.Contains("Seed=123", contents); Assert.Contains("[REDACTED]", contents); Assert.Contains("[PATH]", contents);
        Assert.Single(receipt.Logs); Assert.True(receipt.Redactions > 0);
        Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains("selected.log") || e.FullName.Contains("Core.zip"));
        Assert.Equal("synthetic-nested-secret", template.Extra!["password"].GetString()); // the owner's source remains untouched
        using var manifest = JsonDocument.Parse(Read(zip, "manifest.json"));
        Assert.Equal(receipt.Redactions, manifest.RootElement.GetProperty("RedactionCount").GetInt32());
        Assert.False(manifest.RootElement.GetProperty("SentExternally").GetBoolean());
    }
    [Fact]
    public async Task SelectedLargeLogUsesBoundedTailAndReportsTruncation()
    {
        using var fixture = new SyntheticInstalledGame(); string log = Path.Combine(fixture.Root, "large.log"), archive = Path.Combine(fixture.Root, "bundle.zip");
        File.WriteAllText(log, "OLD_PREFIX" + new string('x', DiagnosticBundleService.MaximumLogBytes + 500) + "\nRECENT_TAIL\n");
        var receipt = await DiagnosticBundleService.CreateAsync(archive, new() { SelectedLogPaths = [log] });
        var result = Assert.Single(receipt.Logs); Assert.True(result.Truncated);
        Assert.True(result.BytesIncluded <= DiagnosticBundleService.MaximumLogBytes);
        using var zip = ZipFile.OpenRead(archive); string contents = Read(zip, "logs/selected-1.txt");
        Assert.Contains("RECENT_TAIL", contents); Assert.DoesNotContain("OLD_PREFIX", contents);
    }
    [Fact]
    public async Task ExplicitUtf16LogIsConvertedToBoundedUtf8AndKeepsRecentTail()
    {
        using var fixture = new SyntheticInstalledGame(); string log = Path.Combine(fixture.Root, "utf16.log"), archive = Path.Combine(fixture.Root, "bundle.zip");
        File.WriteAllText(log, "СТАРОЕ_НАЧАЛО" + new string('я', 40_000) + "\r\nСВЕЖИЙ_КОНЕЦ\r\n", Encoding.Unicode);
        var receipt = await DiagnosticBundleService.CreateAsync(archive, new() { SelectedLogPaths = [log] });
        var result = Assert.Single(receipt.Logs); Assert.Null(result.Error); Assert.True(result.Truncated);
        Assert.True(result.BytesIncluded <= DiagnosticBundleService.MaximumLogBytes);
        using var zip = ZipFile.OpenRead(archive); string text = Read(zip, "logs/selected-1.txt");
        Assert.Contains("СВЕЖИЙ_КОНЕЦ", text); Assert.DoesNotContain("СТАРОЕ_НАЧАЛО", text);
    }
    [Fact]
    public async Task MissingSelectedLogAndRejectedBinaryAreVisibleAndNoImplicitLogsAreAdded()
    {
        using var fixture = new SyntheticInstalledGame(); string missing = Path.Combine(fixture.Root, "missing.log"), binary = Path.Combine(fixture.Root, "binary.log");
        File.WriteAllBytes(binary, [1, 0, 2, 3]); string archive = Path.Combine(fixture.Root, "bundle.zip");
        var receipt = await DiagnosticBundleService.CreateAsync(archive, new() { SelectedLogPaths = [missing, binary] });
        Assert.Equal(2, receipt.Logs.Count); Assert.All(receipt.Logs, l => Assert.NotNull(l.Error));
        using var zip = ZipFile.OpenRead(archive); Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith("logs/"));
    }
    [Fact]
    public async Task NoLogSelectionMeansNoLogReadsAndCancellationKeepsPreviousArchive()
    {
        using var fixture = new SyntheticInstalledGame(); string path = Path.Combine(fixture.Root, "bundle.zip");
        var receipt = await DiagnosticBundleService.CreateAsync(path, new()); Assert.Empty(receipt.Logs);
        using (var zip = ZipFile.OpenRead(path)) Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith("logs/"));
        byte[] original = File.ReadAllBytes(path); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => DiagnosticBundleService.CreateAsync(path, new(), cancel.Token));
        Assert.Equal(original, File.ReadAllBytes(path));
    }
    [Theory]
    [InlineData("password=secret", "secret")]
    [InlineData("{\"accessToken\":\"secret-value\"}", "secret-value")]
    [InlineData("Bearer verysecret123", "verysecret123")]
    [InlineData("https://user:secret@example.com/path", "user:secret")]
    [InlineData("/home/FixtureOwner/private/file.log", "FixtureOwner")]
    public void SanitizerRemovesCredentialAndIdentityForms(string source, string forbidden)
    {
        string safe = DiagnosticSanitizer.SanitizeText(source, out int redactions);
        Assert.DoesNotContain(forbidden, safe); Assert.True(redactions > 0);
    }

    [Fact]
    public async Task DiagnosticManifestSeparatesActualGenerationProfileFromInstalledDataProfile()
    {
        using var fixture = new SyntheticInstalledGame(); string path = Path.Combine(fixture.Root, "bundle.zip");
        var settings = SettingsSnapshotMapper.FromGeneratorSettings(new GeneratorSettings());
        settings.ProfileOptions.ProfileId = GenerationProfileOptions.CurrentProfile;
        await DiagnosticBundleService.CreateAsync(path, new()
        {
            Settings = settings,
            Installation = new GameInstallation { Profile = new("inspected-fixture", GameProfileStatus.InspectedGameData, [], "fixture") },
        });
        using var zip = ZipFile.OpenRead(path); using var manifest = JsonDocument.Parse(Read(zip, "manifest.json"));
        Assert.Equal(GenerationProfileOptions.LegacyProfile, manifest.RootElement.GetProperty("GenerationProfileId").GetString());
        Assert.Equal("inspected-fixture", manifest.RootElement.GetProperty("GameDataProfileId").GetString());
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf16le")]
    [InlineData("utf16be")]
    public async Task TruncatedLogDropsCredentialSuffixWhosePrefixWasOutsideTheTail(string encodingName)
    {
        using var fixture = new SyntheticInstalledGame(); string log = Path.Combine(fixture.Root, "chosen.log"), archive = Path.Combine(fixture.Root, "bundle.zip");
        var encoding = encodingName switch { "utf16le" => Encoding.Unicode, "utf16be" => Encoding.BigEndianUnicode, _ => new UTF8Encoding(false) };
        string text = "password=" + new string('x', DiagnosticBundleService.MaximumLogBytes + 100) + "SENSITIVE_SUFFIX\r\nRECENT_COMPLETE_LINE\r\n";
        File.WriteAllText(log, text, encoding); byte[] source = File.ReadAllBytes(log);
        var receipt = await DiagnosticBundleService.CreateAsync(archive, new() { SelectedLogPaths = [log] });
        using var zip = ZipFile.OpenRead(archive); string included = Read(zip, "logs/selected-1.txt");
        Assert.DoesNotContain("SENSITIVE_SUFFIX", included); Assert.Contains("RECENT_COMPLETE_LINE", included);
        Assert.True(Assert.Single(receipt.Logs).Truncated); Assert.Equal(source, File.ReadAllBytes(log));
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf16le")]
    [InlineData("utf16be")]
    public async Task OversizedSinglePhysicalLineHasNoSafelyRedactableTail(string encodingName)
    {
        using var fixture = new SyntheticInstalledGame(); string log = Path.Combine(fixture.Root, "single.log"), archive = Path.Combine(fixture.Root, "bundle.zip");
        var encoding = encodingName switch { "utf16le" => Encoding.Unicode, "utf16be" => Encoding.BigEndianUnicode, _ => new UTF8Encoding(false) };
        File.WriteAllText(log, "token=" + new string('x', DiagnosticBundleService.MaximumLogBytes + 100) + "SENSITIVE_SUFFIX", encoding);
        var receipt = await DiagnosticBundleService.CreateAsync(archive, new() { SelectedLogPaths = [log] });
        var logReceipt = Assert.Single(receipt.Logs); Assert.True(logReceipt.Truncated); Assert.Equal(0, logReceipt.BytesIncluded); Assert.Null(logReceipt.Error);
        using var zip = ZipFile.OpenRead(archive); Assert.Empty(Read(zip, "logs/selected-1.txt"));
    }

    [Fact]
    public async Task FullSnapshotUnknownFieldsAndPropertyNamesAreSanitizedWithCollisionSafeKeysWithoutMutatingSource()
    {
        using var fixture = new SyntheticInstalledGame(); string archive = Path.Combine(fixture.Root, "bundle.zip");
        var original = new GeneratorSettings();
        original.ProfileOptions.ImportedNativeSchema = System.Text.Json.Nodes.JsonNode.Parse("""{"access_token":"hidden-access","CLIENT-SECRET":"hidden-client","password.privateKey":"hidden-private","C:\\Users\\Fixture Owner\\private-a":{"v":1},"C:\\Users\\Fixture Owner\\private-b":{"v":2},"owner@example.invalid":"owner-value","[REDACTED_KEY_1]":"keep"}""")!.AsObject();
        original.ProfileOptions.ImportedNativeConfiguration = System.Text.Json.Nodes.JsonNode.Parse("""{"refresh-token":"hidden-refresh","nested":{"db_password":"hidden-db"}}""")!.AsObject();
        foreach (var property in original.ProfileOptions.ImportedNativeSchema)
            original.ProfileOptions.ImportedNativeConfiguration[property.Key] = property.Value?.DeepClone();
        var settings = SettingsSnapshotMapper.FromGeneratorSettings(original);
        await DiagnosticBundleService.CreateAsync(archive, new() { Settings = settings });
        using var zip = ZipFile.OpenRead(archive); string json = Read(zip, "settings.sanitized.oetgs");
        foreach (string secret in new[] { "hidden-access", "hidden-client", "hidden-private", "hidden-refresh", "hidden-db", "Fixture Owner", "owner@example.invalid" }) Assert.DoesNotContain(secret, json);
        using var document = JsonDocument.Parse(json);
        var profile = document.RootElement.GetProperty("generatorSnapshot").GetProperty("ProfileOptions");
        Assert.False(profile.TryGetProperty("ImportedNativeSchema", out _));
        var raw = profile.GetProperty("ImportedNativeConfiguration");
        Assert.Equal(9, raw.EnumerateObject().Count()); Assert.Equal("keep", raw.GetProperty("[REDACTED_KEY_1]").GetString());
        Assert.Equal("hidden-access", original.ProfileOptions.ImportedNativeSchema["access_token"]!.GetValue<string>());
        using var manifest = JsonDocument.Parse(Read(zip, "manifest.json"));
        Assert.True(manifest.RootElement.GetProperty("PrivacyReviewRequiredBeforeSharing").GetBoolean());
        Assert.False(manifest.RootElement.TryGetProperty("IncludesMachinePaths", out _));
    }

    [Fact]
    public async Task LargeImportedNativeSchemaIsOmittedFromBothDiagnosticSettingsCopiesWithCompactHashReceipts()
    {
        using var fixture = new SyntheticInstalledGame(); string archive = Path.Combine(fixture.Root, "bundle.zip");
        var schemaRoot = new JsonObject
        {
            ["zoneClasses"] = new JsonArray(new JsonObject { ["name"] = "NativeZone", ["nativeOnlyText"] = "SCHEMA_PAYLOAD_MARKER" + new string('x', DiagnosticBundleService.MaximumJsonBytes + 1000) }),
            ["mainObjectClasses"] = new JsonArray(new JsonObject { ["name"] = "NativeTown", ["type"] = "City" }),
        };
        var schema = NativeSchemaDocument.Parse(schemaRoot.ToJsonString());
        var imported = NativeConfigurationImporter.Import(NativeConfigurationCodec.ParseJson("""{"flags":4,"size":128,"spawnCount":2}"""), new GeneratorSettings { Seed = 42 }, schema: schema);
        imported.Settings.ProfileOptions.ImportedNativeSchema = imported.RawSchema;
        imported.Settings.ProfileOptions.ImportedNativeConfiguration = imported.RawConfiguration;
        imported.Settings.ProfileOptions.ImportedNativeConfiguration["access_token"] = "ORDINARY_SETTINGS_CREDENTIAL";
        var settings = SettingsSnapshotMapper.FromGeneratorSettings(imported.Settings);
        byte[] before = JsonSerializer.SerializeToUtf8Bytes(settings, JsonExport.Options);
        byte[] originalSchema = JsonSerializer.SerializeToUtf8Bytes(schema.Root, new JsonSerializerOptions(JsonExport.Options) { WriteIndented = false, MaxDepth = 128 });
        string expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(originalSchema));
        Assert.True(originalSchema.Length > DiagnosticBundleService.MaximumJsonBytes);
        var receipt = await DiagnosticBundleService.CreateAsync(archive, new() { Settings = settings, TemplateSeed = 42 });
        Assert.Equal(2, receipt.Omissions.Count);
        Assert.Equal(new[] { "profileOptions.ImportedNativeSchema", "generatorSnapshot.ProfileOptions.ImportedNativeSchema" }, receipt.Omissions.Select(o => o.Field));
        Assert.All(receipt.Omissions, omission =>
        {
            Assert.Equal(originalSchema.Length, omission.CompactJsonByteCount); Assert.Equal(expectedHash, omission.CompactJsonSha256);
            Assert.Contains("omitted", omission.Reason, StringComparison.Ordinal);
        });
        Assert.Equal(before, JsonSerializer.SerializeToUtf8Bytes(settings, JsonExport.Options));
        using var zip = ZipFile.OpenRead(archive);
        string contents = string.Join("\n", zip.Entries.Select(e => Read(zip, e.FullName)));
        Assert.DoesNotContain("SCHEMA_PAYLOAD_MARKER", contents); Assert.DoesNotContain("ORDINARY_SETTINGS_CREDENTIAL", contents);
        using var diagnosticSettings = JsonDocument.Parse(Read(zip, "settings.sanitized.oetgs"));
        Assert.False(diagnosticSettings.RootElement.GetProperty("profileOptions").TryGetProperty("ImportedNativeSchema", out _));
        Assert.False(diagnosticSettings.RootElement.GetProperty("generatorSnapshot").GetProperty("ProfileOptions").TryGetProperty("ImportedNativeSchema", out _));
        Assert.Equal(42, diagnosticSettings.RootElement.GetProperty("seed").GetInt32());
        Assert.True(zip.GetEntry("settings.sanitized.oetgs")!.Length < DiagnosticBundleService.MaximumJsonBytes);
        using var manifest = JsonDocument.Parse(Read(zip, "manifest.json"));
        var omissions = manifest.RootElement.GetProperty("OmittedPayloads"); Assert.Equal(2, omissions.GetArrayLength());
        Assert.Equal(expectedHash, omissions[0].GetProperty("CompactJsonSha256").GetString());
        Assert.Equal(GenerationProfileOptions.CurrentProfile, manifest.RootElement.GetProperty("GenerationProfileId").GetString());
        Assert.Contains("native-схемы исключены", Read(zip, "readme.ru.txt"));
    }

    [Fact]
    public async Task OrdinaryOversizedSettingsStillFailWithoutReplacingAnExistingDiagnosticArchive()
    {
        using var fixture = new SyntheticInstalledGame(); string archive = Path.Combine(fixture.Root, "bundle.zip"); byte[] previous = [5, 4, 3, 2, 1];
        File.WriteAllBytes(archive, previous);
        var settings = new SettingsFile { TemplateName = new string('x', DiagnosticBundleService.MaximumJsonBytes + 1) };
        await Assert.ThrowsAsync<InvalidDataException>(() => DiagnosticBundleService.CreateAsync(archive, new() { Settings = settings }));
        Assert.Equal(previous, File.ReadAllBytes(archive));
    }

    [Theory]
    [InlineData("client_secret=hidden-client", "hidden-client")]
    [InlineData("DB-PASSWORD=hidden-db", "hidden-db")]
    [InlineData("private_key=hidden-key", "hidden-key")]
    [InlineData("C:\\Users\\Fixture Owner\\private\\file.txt", "Fixture Owner")]
    public void CompoundCredentialLabelsAndSpaceContainingPathsAreRedacted(string input, string sensitive)
    { string safe = DiagnosticSanitizer.SanitizeText(input, out int redactions); Assert.DoesNotContain(sensitive, safe); Assert.True(redactions > 0); }

    [Theory]
    [InlineData("Authorization: Bearer HEADER_VALUE_ONE HEADER_VALUE_TWO")]
    [InlineData("proxy-authorization=Basic HEADER_VALUE_ONE HEADER_VALUE_TWO")]
    [InlineData("HTTP_AUTHORIZATION: Digest username=HEADER_VALUE_ONE, response=HEADER_VALUE_TWO")]
    [InlineData("Cookie: session=HEADER_VALUE_ONE; refresh=HEADER_VALUE_TWO")]
    [InlineData("Set-Cookie: session=HEADER_VALUE_ONE; Domain=HEADER_VALUE_TWO; Secure")]
    [InlineData("{\"Authorization\":\"Bearer HEADER_VALUE_ONE HEADER_VALUE_TWO\"}")]
    [InlineData("Authorization: \"HEADER_VALUE_ONE\" HEADER_VALUE_TWO")]
    public void EntireCredentialHeaderIsRedactedBeforeGenericKeyValueMasking(string header)
    {
        string safe = DiagnosticSanitizer.SanitizeText(header + "\r\nNEXT_COMPLETE_LINE\r\n", out int redactions);
        Assert.DoesNotContain("HEADER_VALUE_ONE", safe); Assert.DoesNotContain("HEADER_VALUE_TWO", safe);
        Assert.Contains("NEXT_COMPLETE_LINE", safe); Assert.True(redactions > 0);
    }

    [Theory]
    [InlineData("Authorization:\r\n  Basic FOLDED_VALUE_ONE FOLDED_VALUE_TWO\r\n")]
    [InlineData("Proxy-Authorization:\n\tBearer FOLDED_VALUE_ONE\n\tFOLDED_VALUE_TWO\n")]
    [InlineData("HTTP_AUTHORIZATION: Digest username=FOLDED_VALUE_ONE,\r\n response=FOLDED_VALUE_TWO\r\n")]
    [InlineData("Cookie: session=FOLDED_VALUE_ONE;\n refresh=FOLDED_VALUE_TWO\n")]
    [InlineData("Set-Cookie: session=FOLDED_VALUE_ONE;\r\tPath=FOLDED_VALUE_TWO\r")]
    public void FoldedCredentialHeaderIncludesEveryIndentedContinuationAndKeepsTheNextHeader(string header)
    {
        string safe = DiagnosticSanitizer.SanitizeText(header + "Content-Length: 7\r\nNEXT_COMPLETE_LINE\r\n", out int redactions);
        Assert.DoesNotContain("FOLDED_VALUE_ONE", safe); Assert.DoesNotContain("FOLDED_VALUE_TWO", safe);
        Assert.Contains("Content-Length: 7", safe); Assert.Contains("NEXT_COMPLETE_LINE", safe); Assert.True(redactions > 0);
    }

    [Fact]
    public async Task TruncatedHeaderTailAlsoDropsCompleteContinuationsWhoseCredentialPrefixWasCutOff()
    {
        using var fixture = new SyntheticInstalledGame(); string log = Path.Combine(fixture.Root, "folded.log"), archive = Path.Combine(fixture.Root, "bundle.zip");
        File.WriteAllText(log, "Authorization: Digest " + new string('x', DiagnosticBundleService.MaximumLogBytes + 100)
            + "\r\n  PREFIXLESS_FOLDED_CREDENTIAL\r\n\tSECOND_FOLDED_CREDENTIAL\r\nNEXT_COMPLETE_LINE\r\n");
        byte[] original = File.ReadAllBytes(log);
        var receipt = await DiagnosticBundleService.CreateAsync(archive, new() { SelectedLogPaths = [log] });
        using var zip = ZipFile.OpenRead(archive); string included = Read(zip, "logs/selected-1.txt");
        Assert.DoesNotContain("PREFIXLESS_FOLDED_CREDENTIAL", included); Assert.DoesNotContain("SECOND_FOLDED_CREDENTIAL", included);
        Assert.Contains("NEXT_COMPLETE_LINE", included); Assert.True(Assert.Single(receipt.Logs).Truncated);
        Assert.Equal(original, File.ReadAllBytes(log));
    }

    [Theory]
    [InlineData(".log")]
    [InlineData(".bin")]
    public async Task RejectedSelectedFileHasAnErrorReceiptWhileLaterTextLogsAreIncluded(string extension)
    {
        using var fixture = new SyntheticInstalledGame();
        string rejected = Path.Combine(fixture.Root, "rejected" + extension), good = Path.Combine(fixture.Root, "good.log"), archive = Path.Combine(fixture.Root, "bundle.zip");
        byte[] original = [1, 0, 2, 3];
        File.WriteAllBytes(rejected, original); File.WriteAllText(good, "HEALTHY_LOG_LINE\n");
        var receipt = await DiagnosticBundleService.CreateAsync(archive, new() { SelectedLogPaths = [rejected, good] });
        Assert.Equal(2, receipt.Logs.Count); Assert.NotNull(receipt.Logs[0].Error); Assert.Equal(0, receipt.Logs[0].BytesIncluded);
        Assert.Null(receipt.Logs[1].Error); Assert.Equal(original, File.ReadAllBytes(rejected));
        using var zip = ZipFile.OpenRead(archive); Assert.Null(zip.GetEntry("logs/selected-1.txt"));
        Assert.Contains("HEALTHY_LOG_LINE", Read(zip, "logs/selected-2.txt"));
    }
}
