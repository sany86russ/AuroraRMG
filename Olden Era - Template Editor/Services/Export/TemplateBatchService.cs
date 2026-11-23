using System.Security.Cryptography;
using System.Text.Json;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services.Analysis;
using Olden_Era___Template_Editor.Services.Generation;

namespace Olden_Era___Template_Editor.Services.Export;

public sealed record TemplateBatchRequest(GeneratorSettings Settings, IReadOnlyList<int> Seeds, string OutputDirectory)
{
    public TemplateReferenceCatalog? ValidationCatalog { get; init; }
    public bool OverwriteExisting { get; init; }
}
public sealed record QuickTemplateBatchRequest(QuickGenerateOptions Options, IReadOnlyList<int> Seeds, string OutputDirectory)
{
    public TemplateReferenceCatalog? ValidationCatalog { get; init; }
    public bool OverwriteExisting { get; init; }
}
public sealed record TemplateBatchCaseResult(int Seed, bool Success, string? TemplatePath, string? TemplateSha256,
    IReadOnlyList<TemplateValidationIssue> Issues, string? Error);
public sealed record TemplateBatchProgress(int Completed, int Total, TemplateBatchCaseResult Case);
public sealed record TemplateBatchReceipt(IReadOnlyList<TemplateBatchCaseResult> Cases, bool Cancelled)
{
    public int SuccessfulCount => Cases.Count(c => c.Success);
    public int FailedCount => Cases.Count(c => !c.Success);
}

/// <summary>Generate one case at a time; preserve successes and report every failed case without substituting output.</summary>
public static class TemplateBatchService
{
    public const int MaximumCases = 128;
    private static readonly JsonSerializerOptions CloneOptions = SettingsSnapshotMapper.SnapshotOptions;
    public static GeneratorSettings CloneSettings(GeneratorSettings settings) => SettingsSnapshotMapper.Clone(settings);

    public static Task<TemplateBatchReceipt> GenerateAsync(TemplateBatchRequest request, CancellationToken cancellationToken = default,
        IProgress<TemplateBatchProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Settings);
        byte[] snapshot = JsonSerializer.SerializeToUtf8Bytes(request.Settings, CloneOptions);
        return GenerateAsync(request.Seeds, request.OutputDirectory, request.ValidationCatalog, request.OverwriteExisting, seed =>
        {
            var settings = RestoreSnapshot<GeneratorSettings>(snapshot);
            settings.Seed = seed;
            settings.ProfileOptions.TemplateSeed = seed;
            return settings;
        }, cancellationToken, progress);
    }

    public static Task<TemplateBatchReceipt> GenerateQuickAsync(QuickTemplateBatchRequest request, CancellationToken cancellationToken = default,
        IProgress<TemplateBatchProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Options);
        byte[] snapshot = JsonSerializer.SerializeToUtf8Bytes(request.Options, CloneOptions);
        return GenerateAsync(request.Seeds, request.OutputDirectory, request.ValidationCatalog, request.OverwriteExisting, seed =>
        {
            var options = RestoreSnapshot<QuickGenerateOptions>(snapshot);
            options.Seed = seed;
            return RandomTemplateBuilder.Build(options);
        }, cancellationToken, progress);
    }

    private static T RestoreSnapshot<T>(byte[] snapshot) where T : class =>
        JsonSerializer.Deserialize<T>(snapshot, CloneOptions)
            ?? throw new InvalidOperationException("Generator settings snapshot could not be restored.");

    private static Task<TemplateBatchReceipt> GenerateAsync(IReadOnlyList<int> seeds, string outputDirectory,
        TemplateReferenceCatalog? catalog, bool overwriteExisting, Func<int, GeneratorSettings> buildSettings,
        CancellationToken token, IProgress<TemplateBatchProgress>? progress)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        var seedSnapshot = seeds.ToArray();
        if (seedSnapshot.Length is < 1 or > MaximumCases) throw new ArgumentOutOfRangeException(nameof(seeds), $"A batch must contain 1–{MaximumCases} seeds.");
        if (seedSnapshot.Distinct().Count() != seedSnapshot.Length) throw new ArgumentException("Batch seeds must be unique.", nameof(seeds));
        string directory = Path.GetFullPath(outputDirectory);
        ExportDestination.EnsureOutsideGame(directory);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("The selected batch directory does not exist.");
        return Task.Run(() => Generate(directory, seedSnapshot, catalog, overwriteExisting, buildSettings, token, progress), CancellationToken.None);
    }

    private static TemplateBatchReceipt Generate(string directory, int[] seeds, TemplateReferenceCatalog? catalog, bool overwriteExisting,
        Func<int, GeneratorSettings> buildSettings, CancellationToken token, IProgress<TemplateBatchProgress>? progress)
    {
        var results = new List<TemplateBatchCaseResult>();
        foreach (int seed in seeds)
        {
            if (token.IsCancellationRequested) return new(results, true);
            TemplateBatchCaseResult result;
            try
            {
                var settings = buildSettings(seed);
                var template = TemplateGenerator.Generate(settings);
                var issues = TemplateCompatibilityValidator.Validate(template, TemplateValidationMode.StrictExport, catalog);
                var errors = issues.Where(i => i.Severity == TemplateIssueSeverity.Error).ToArray();
                if (errors.Length > 0)
                    result = new(seed, false, null, null, issues, "Validation failed: " + string.Join("; ", errors.Select(i => i.Path + ": " + i.Message)));
                else
                {
                    string filename = "template-" + SafeName(settings.TemplateName) + "-" + seed.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".rmg.json";
                    string destination = Path.Combine(directory, filename);
                    if (File.Exists(destination) && !overwriteExisting) throw new IOException("A batch destination already exists: " + filename);
                    byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(template, JsonExport.Options);
                    token.ThrowIfCancellationRequested();
                    AtomicFile.WriteAllText(destination, System.Text.Encoding.UTF8.GetString(bytes), overwrite: overwriteExisting,
                        validateDestination: path => ExportDestination.EnsureOutsideGame(path));
                    result = new(seed, true, destination, Convert.ToHexString(SHA256.HashData(bytes)), issues, null);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return new(results, true); }
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not OperationCanceledException)
            {
                // A case failure is a visible receipt, never a substituted template or a claimed success.
                result = new(seed, false, null, null, Array.Empty<TemplateValidationIssue>(), ex.GetType().Name + ": " + ex.Message);
            }
            results.Add(result);
            progress?.Report(new(results.Count, seeds.Length, result));
        }
        return new(results, false);
    }

    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        string value = new(name.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).Take(64).ToArray());
        value = value.Trim().TrimEnd('.');
        return value.Length == 0 ? "map" : value;
    }
}
