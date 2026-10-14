using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services.Analysis;
using Olden_Era___Template_Editor.Services.GameData;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.Export;

public sealed record TemplatePackageRequest(RmgTemplate Template, SettingsFile Settings)
{
    public int? TemplateSeed { get; init; }
    public int? GameSeed { get; init; }
    public string AlgorithmVersion { get; init; } = "aurora-generator-2";
    public GameCompatibilityProfile? Profile { get; init; }
    public byte[]? PreviewPng { get; init; }
    public bool IncludeSettings { get; init; } = true;
    public TemplateReferenceCatalog? ValidationCatalog { get; init; }
}
public sealed record TemplatePackageManifest
{
    public int FormatVersion { get; init; } = 3;
    public string AuroraVersion { get; init; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
    public string AlgorithmVersion { get; init; } = "";
    public string ProfileId { get; init; } = "";
    /// <summary>The selected Aurora generation behavior, independent of the installed data audit.</summary>
    public string GenerationProfileId { get; init; } = "";
    /// <summary>The compatibility audit of the local game data. ProfileId remains its legacy alias.</summary>
    public string GameDataProfileId { get; init; } = "";
    public string ProfileEvidence { get; init; } = "";
    public string TemplateFile { get; init; } = "template.rmg.json";
    public string? SettingsFile { get; init; } = "settings.oetgs";
    public string TemplateSha256 { get; init; } = "";
    public string? SettingsSha256 { get; init; }
    public int? TemplateSeed { get; init; }
    public int? GameSeed { get; init; }
    public int SettingsSchemaVersion { get; init; }
    public int ValidationWarningCount { get; init; }
    public List<string> ValidationWarningCodes { get; init; } = [];
    public bool GamePlayabilityVerified { get; init; }
}
public sealed record TemplatePackageReceipt(string Path, string TemplateSha256, TemplatePackageManifest Manifest);

public static class TemplatePackageService
{
    public static async Task<TemplatePackageReceipt> CreateAsync(string zipPath, TemplatePackageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Template); ArgumentNullException.ThrowIfNull(request.Settings);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] templateBytes = JsonSerializer.SerializeToUtf8Bytes(request.Template, JsonExport.Options);
        byte[]? settingsBytes = request.IncludeSettings ? JsonSerializer.SerializeToUtf8Bytes(request.Settings, JsonExport.Options) : null;
        if (settingsBytes is not null && DiagnosticSanitizer.HasKnownCredentials(System.Text.Json.Nodes.JsonNode.Parse(settingsBytes)))
            throw new InvalidOperationException(Services.Localization.LocalizationManager.T("S.Patch.PackageSensitiveSettings"));
        var snapshot = JsonSerializer.Deserialize<RmgTemplate>(templateBytes, JsonExport.Options)!;
        var issues = TemplateCompatibilityValidator.Validate(snapshot, TemplateValidationMode.StrictExport, request.ValidationCatalog);
        var errors = issues.Where(i => i.Severity == TemplateIssueSeverity.Error).ToArray();
        if (errors.Length > 0) throw new InvalidOperationException("Template validation failed: " + string.Join("; ", errors.Select(i => i.Path + ": " + i.Message)));
        var preview = request.PreviewPng?.ToArray();
        if (preview is not null) ValidatePreview(preview);
        var profile = request.Profile ?? GameCompatibilityProfile.Legacy;
        var actualSettings = SettingsSnapshotMapper.RestoreSnapshot(request.Settings);
        var manifest = new TemplatePackageManifest
        {
            AlgorithmVersion = request.AlgorithmVersion,
            ProfileId = profile.Id,
            GenerationProfileId = actualSettings?.ProfileOptions.ProfileId ?? request.Settings.ProfileOptions.ProfileId,
            GameDataProfileId = profile.Id,
            ProfileEvidence = profile.Evidence,
            SettingsFile = request.IncludeSettings ? "settings.oetgs" : null,
            TemplateSha256 = Convert.ToHexString(SHA256.HashData(templateBytes)),
            SettingsSha256 = settingsBytes is null ? null : Convert.ToHexString(SHA256.HashData(settingsBytes)),
            TemplateSeed = request.TemplateSeed, GameSeed = request.GameSeed,
            SettingsSchemaVersion = request.Settings.SchemaVersion,
            ValidationWarningCount = issues.Count(i => i.Severity == TemplateIssueSeverity.Warning),
            ValidationWarningCodes = issues.Where(i => i.Severity == TemplateIssueSeverity.Warning).Select(i => i.Code).Distinct().ToList(),
            GamePlayabilityVerified = false
        };
        string notesRu = $"AuroraRMG — пакет шаблона\n\nШаблон: {snapshot.Name}\n" +
            $"Профиль генерации Aurora: {manifest.GenerationProfileId}\nПрофиль проверки данных игры: {manifest.GameDataProfileId}\nАлгоритм: {manifest.AlgorithmVersion}\n" +
            $"Seed структуры шаблона: {request.TemplateSeed?.ToString() ?? "не записан"}\nSeed карты в игре: {request.GameSeed?.ToString() ?? "выбирается отдельно в игре"}\n\n" +
            "Скопируйте template.rmg.json в map_templates установленной игры, затем выберите его при настройке матча. " +
            (request.IncludeSettings ? "settings.oetgs открывается в AuroraRMG. " : "Настройки генератора в этот пакет не включены. ") + "Выбор фракций и людей/ИИ выполняется в игре.\n" +
            "Seed Aurora определяет структуру шаблона. Seed игры отдельно определяет размещение на карте.\n" +
            "Статическая проверка JSON выполнена; успешная генерация и завершение матча требуют проверки в игре.\n";
        string notesEn = $"AuroraRMG — template package\n\nTemplate: {snapshot.Name}\n" +
            $"Aurora generation profile: {manifest.GenerationProfileId}\nGame data audit profile: {manifest.GameDataProfileId}\nAlgorithm: {manifest.AlgorithmVersion}\n" +
            $"Template structure seed: {request.TemplateSeed?.ToString() ?? "not recorded"}\nIn-game map seed: {request.GameSeed?.ToString() ?? "selected separately in game"}\n\n" +
            "Copy template.rmg.json into the installed game's map_templates folder, then select it when configuring the match. " +
            (request.IncludeSettings ? "Open settings.oetgs in AuroraRMG. " : "Generator settings are not included in this package. ") + "Select factions and human/AI slots in the game.\n" +
            "The Aurora seed determines template structure. The game seed separately determines map placement.\n" +
            "Static JSON validation passed; map generation and match completion still require in-game validation.\n";
        await AtomicArchive.WriteAsync(zipPath, async (zip, token) =>
        {
            await AtomicArchive.AddAsync(zip, "template.rmg.json", templateBytes, token);
            if (settingsBytes is not null) await AtomicArchive.AddAsync(zip, "settings.oetgs", settingsBytes, token);
            if (preview is not null) await AtomicArchive.AddAsync(zip, "preview.png", preview, token);
            await AtomicArchive.AddTextAsync(zip, "run.ru.txt", notesRu, token);
            await AtomicArchive.AddTextAsync(zip, "run.en.txt", notesEn, token);
            await AtomicArchive.AddAsync(zip, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest, JsonExport.Options), token);
        }, cancellationToken).ConfigureAwait(false);
        return new(Path.GetFullPath(zipPath), manifest.TemplateSha256, manifest);
    }

    private static void ValidatePreview(byte[] png)
    {
        byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (png.Length is < 33 or > 8 * 1024 * 1024 || !png.AsSpan(0, 8).SequenceEqual(signature) ||
            !png.AsSpan(12, 4).SequenceEqual("IHDR"u8) || BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(8, 4)) != 13)
            throw new InvalidDataException("Preview must be a PNG image no larger than 8 MiB.");
        uint width = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)), height = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4));
        if (width == 0 || height == 0 || width > 8192 || height > 8192 || (long)width * height > 32_000_000)
            throw new InvalidDataException("Preview dimensions exceed the package limit.");
    }
}
