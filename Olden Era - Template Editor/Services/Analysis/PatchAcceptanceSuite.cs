using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services.GameData;
using Olden_Era___Template_Editor.Services.Generation;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.Analysis;

public sealed record PatchCorpusReceipt(string FileName, string SourceSha256, int Variants, bool PreservesValues,
    IReadOnlyList<TemplateRoundTripFinding> Differences, IReadOnlyList<TemplateRoundTripFinding> Normalizations,
    IReadOnlyList<TemplateValidationIssue> TolerantIssues, string? Error)
{
    public bool SafelyRejected { get; init; }
    public string? RejectionReason { get; init; }
    public bool PassedOfflineAudit => Error is null && (PreservesValues || SafelyRejected && Differences.Count > 0
        && !string.IsNullOrWhiteSpace(RejectionReason) && !string.IsNullOrWhiteSpace(SourceSha256));
}
public sealed record PatchGenerationReceipt(string Id, string Category, int Seed, bool Success, string? TemplateSha256,
    IReadOnlyList<TemplateValidationIssue> Issues, string? Error, string? ManualTemplateFile, string? ManualSettingsFile)
{
    public string? EconomyFingerprint { get; init; }
    public string? CombatFingerprint { get; init; }
}
public sealed record PatchAcceptanceProgress(string Stage, int Completed, int Total, string Id);
public sealed record PatchAcceptanceReceipt(string OutputDirectory, DateTime CompletedUtc, string SourceFingerprint,
    string SchemaFingerprint, string? GameVersion, string? SteamBuild, IReadOnlyList<PatchCorpusReceipt> Corpus,
    IReadOnlyList<PatchGenerationReceipt> Generation, bool Cancelled)
{
    public bool OfflinePassed => !Cancelled && Corpus.Count > 0 && Generation.Count > 0
        && Corpus.All(item => item.PassedOfflineAudit) && Generation.All(item => item.Success);
    public string GameAcceptanceStatus => "Pending manual checks; no game was launched by the offline suite.";
}
public sealed class PatchAcceptanceException(PatchAcceptanceReceipt receipt)
    : InvalidOperationException($"Offline acceptance failed: {receipt.Corpus.Count(item => !item.PassedOfflineAudit)} corpus and {receipt.Generation.Count(item => !item.Success)} generated cases. See {Path.Combine(receipt.OutputDirectory, "acceptance-report.json")}.")
{
    public PatchAcceptanceReceipt Receipt { get; } = receipt;
}

/// <summary>Sequential, read-only installed corpus audit and deterministic generator acceptance kit outside the game.</summary>
public static class PatchAcceptanceSuite
{
    public static readonly IReadOnlyList<int> FixedSeeds = Array.AsReadOnly(new[] { 1729, 424242 });
    private static readonly JsonSerializerOptions ReadOptions = new(JsonExport.Options) { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = 128 };

    public static Task<PatchAcceptanceReceipt> RunAsync(string outputDirectory, GameCatalog catalog,
        CancellationToken cancellationToken = default, IProgress<PatchAcceptanceProgress>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory); ArgumentNullException.ThrowIfNull(catalog);
        var source = catalog.Source ?? throw new ArgumentException("The offline suite requires an installed-game catalog with a source.", nameof(catalog));
        var references = catalog.GetReferenceCatalog();
        if (!references.IsComplete) throw new InvalidOperationException("The installed reference catalog is incomplete; strict acceptance cannot be claimed.");
        string directory = Path.GetFullPath(outputDirectory);
        EnsureOutside(directory, source.InstallPath);
        EnsureOutside(directory, source.TemplatesPath);
        if (!Directory.Exists(source.TemplatesPath)) throw new DirectoryNotFoundException("Installed template folder was not found.");
        var core = new FileInfo(source.CoreZipPath);
        if (!core.Exists || core.Length != source.SourceLength || core.LastWriteTimeUtc.Ticks != source.SourceStamp)
            throw new InvalidOperationException("The installed catalog source changed or is unavailable; refresh it before acceptance.");
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
            throw new IOException("Acceptance output must be a new or empty directory; existing results are never overwritten.");
        Directory.CreateDirectory(directory);
        // A caller's process controls Idle priority/CPU affinity. Work inside this service stays strictly sequential.
        return Task.Run(() => Run(directory, source, references, cancellationToken, progress), CancellationToken.None);
    }

    private static PatchAcceptanceReceipt Run(string directory, GameInstallation source, TemplateReferenceCatalog references,
        CancellationToken token, IProgress<PatchAcceptanceProgress>? progress)
    {
        var corpus = new List<PatchCorpusReceipt>(); var generated = new List<PatchGenerationReceipt>();
        bool cancelled = false; int completed = 0, total = 0;
        var manual = new List<object>(); var fileHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            token.ThrowIfCancellationRequested(); VerifySourceFingerprints(source);
            var files = EnumerateCorpus(source.TemplatesPath);
            var cases = Cases(references).ToArray(); total = files.Length + cases.Length * FixedSeeds.Count;
            foreach (string file in files)
            {
                token.ThrowIfCancellationRequested(); string filename = Path.GetRelativePath(source.TemplatesPath, file).Replace('\\', '/'); string sha = "";
                try
                {
                    byte[] bytes = File.ReadAllBytes(file); sha = Hash(bytes); string json = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
                    var template = TemplateJsonReader.Deserialize(json, ReadOptions) ?? throw new JsonException("Null template root.");
                    string exported = JsonSerializer.Serialize(template, JsonExport.Options);
                    var preservation = TemplateRoundTripAudit.Compare(json, exported);
                    var importedAgain = JsonSerializer.Deserialize<RmgTemplate>(exported, ReadOptions) ?? throw new JsonException("Null re-imported template.");
                    if (JsonSerializer.Serialize(importedAgain, JsonExport.Options) != exported) throw new InvalidOperationException("The second typed round-trip is not stable.");
                    if (Hash(File.ReadAllBytes(file)) != sha) throw new IOException("The installed source changed during its read-only audit.");
                    corpus.Add(new(filename, sha, template.Variants?.Count ?? 0, preservation.PreservesValues,
                        preservation.Differences, preservation.Normalizations,
                        TemplateCompatibilityValidator.Validate(template, TemplateValidationMode.TolerantImport, references), null));
                }
                catch (DuplicateJsonPropertyException ex)
                {
                    string? error = null;
                    try { if (sha.Length == 0 || Hash(File.ReadAllBytes(file)) != sha) error = "The installed source changed during its rejected-input audit."; }
                    catch (Exception readError) when (readError is IOException or UnauthorizedAccessException) { error = readError.GetType().Name + ": " + readError.Message; }
                    string reason = "Repeated JSON property '" + ex.PropertyName + "' at " + (ex.Path ?? "$") + "; no merge or last-value selection was performed.";
                    corpus.Add(new(filename, sha, 0, false, [new(ex.Path ?? "$", reason)], [], [], error)
                    { SafelyRejected = error is null, RejectionReason = reason });
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not OperationCanceledException)
                { corpus.Add(new(filename, sha, 0, false, [], [], [], ex.GetType().Name + ": " + ex.Message)); }
                progress?.Report(new("corpus", ++completed, total, filename));
            }
            foreach (var item in cases)
            foreach (int seed in FixedSeeds)
            {
                token.ThrowIfCancellationRequested(); IReadOnlyList<TemplateValidationIssue> issues = []; string? hash = null, templateFile = null, settingsFile = null;
                try
                {
                    var persisted = PatchPresets.Clone(item.Settings); persisted.Seed = seed; persisted.ProfileOptions.TemplateSeed = seed;
                    var settings = Presets.ToGeneratorSettings(persisted); settings.Seed = seed; settings.ProfileOptions.TemplateSeed = seed;
                    settings.TemplateName = "Acceptance " + item.Id + " " + seed.ToString(CultureInfo.InvariantCulture);
                    var originalSettings = SettingsSnapshotMapper.Clone(settings);
                    byte[] inputBytes = JsonSerializer.SerializeToUtf8Bytes(originalSettings, SettingsSnapshotMapper.SnapshotOptions);
                    var template = TemplateGenerator.Generate(settings);
                    if (!inputBytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(settings, SettingsSnapshotMapper.SnapshotOptions)))
                        throw new InvalidOperationException("Generation mutated the caller's settings.");
                    issues = TemplateCompatibilityValidator.Validate(template, TemplateValidationMode.StrictExport, references);
                    var errors = issues.Where(issue => issue.Severity == TemplateIssueSeverity.Error).ToArray();
                    if (errors.Length > 0) throw new InvalidOperationException(string.Join("; ", errors.Select(issue => issue.Path + ": " + issue.Message)));
                    VerifySettingsContract(template, originalSettings);
                    byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(template, JsonExport.Options); hash = Hash(bytes);
                    var repeated = TemplateGenerator.Generate(SettingsSnapshotMapper.Clone(originalSettings));
                    if (!bytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(repeated, JsonExport.Options)))
                        throw new InvalidOperationException("Identical settings and seed produced different template JSON.");
                    var restored = JsonSerializer.Deserialize<RmgTemplate>(bytes, ReadOptions) ?? throw new JsonException("Generated template could not be re-imported.");
                    if (!bytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(restored, JsonExport.Options)))
                        throw new InvalidOperationException("Generated JSON round-trip changed the template.");
                    if (item.Manual && seed == FixedSeeds[0])
                    {
                        var kit = Path.Combine(directory, "manual-kit"); Directory.CreateDirectory(kit);
                        templateFile = item.Id + ".rmg.json"; settingsFile = item.Id + ".oetgs";
                        byte[] settingsBytes = JsonSerializer.SerializeToUtf8Bytes(SettingsSnapshotMapper.FromGeneratorSettings(originalSettings), JsonExport.Options);
                        Write(Path.Combine(kit, templateFile), Encoding.UTF8.GetString(bytes)); Write(Path.Combine(kit, settingsFile), Encoding.UTF8.GetString(settingsBytes));
                        fileHashes["manual-kit/" + templateFile] = hash; fileHashes["manual-kit/" + settingsFile] = Hash(settingsBytes);
                        manual.Add(new { item.Id, item.NameRu, item.NameEn, TemplateSeed = seed, GameSeed = (int?)null, ObservedGameSeeds = Array.Empty<string>(), Template = templateFile, Settings = settingsFile, Status = "pending", Mode = "lobby or hotseat", GameVersion = source.VersionMarker, Observations = "" });
                    }
                    generated.Add(new(item.Id, item.Category, seed, true, hash, issues, null, templateFile, settingsFile)
                    { EconomyFingerprint = NumericFingerprint(template, false), CombatFingerprint = NumericFingerprint(template, true) });
                }
                catch (Exception ex) when (ex is not OutOfMemoryException && ex is not OperationCanceledException)
                { generated.Add(new(item.Id, item.Category, seed, false, hash, issues, ex.GetType().Name + ": " + ex.Message, templateFile, settingsFile)); }
                progress?.Report(new("generation", ++completed, total, item.Id + ":" + seed.ToString(CultureInfo.InvariantCulture)));
            }
            VerifyTuningEffects(generated); VerifySourceFingerprints(source);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { cancelled = true; }
        catch (Exception ex) when (ex is not OutOfMemoryException && ex is not OperationCanceledException)
        { generated.Add(new("suite-prerequisite", "preflight", 0, false, null, [], ex.GetType().Name + ": " + ex.Message, null, null)); }
        var receipt = new PatchAcceptanceReceipt(directory, DateTime.UtcNow, source.SourceFingerprint, source.SchemaFingerprint,
            source.VersionMarker, source.SteamBuildId, corpus.ToArray(), generated.ToArray(), cancelled);
        Write(Path.Combine(directory, "acceptance-report.json"), JsonSerializer.Serialize(receipt, JsonExport.Options));
        Write(Path.Combine(directory, "manual-results.json"), JsonSerializer.Serialize(new { GameAcceptance = "pending", Cases = manual }, JsonExport.Options));
        Write(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(new { SchemaVersion = 1, receipt.CompletedUtc,
            receipt.SourceFingerprint, receipt.SchemaFingerprint, receipt.GameVersion, receipt.SteamBuild, Files = fileHashes, GameAcceptance = "pending" }, JsonExport.Options));
        Write(Path.Combine(directory, "README-RU.md"), Checklist(receipt, manual.Count, false));
        Write(Path.Combine(directory, "README-EN.md"), Checklist(receipt, manual.Count, true));
        if (cancelled) throw new OperationCanceledException("Offline acceptance cancelled; partial receipts were saved.", token);
        if (!receipt.OfflinePassed) throw new PatchAcceptanceException(receipt);
        return receipt;
    }

    private static IEnumerable<AcceptanceCase> Cases(TemplateReferenceCatalog references)
    {
        foreach (var preset in PatchPresets.Current.Concat(PatchPresets.Cooperative))
            yield return new(preset.Id, "patch-preset", preset.NameRu, preset.NameEn, preset.Settings, false);
        foreach (var pair in Enum.GetValues<MapRichness>())
            yield return Focus("richness-" + pair, "Богатство " + pair, "Richness " + pair, file => file.ProfileOptions.Richness = pair, false);
        foreach (var pair in Enum.GetValues<MapChallenge>())
            yield return Focus("challenge-" + pair, "Опасность " + pair, "Challenge " + pair, file => file.ProfileOptions.Challenge = pair, false);
        yield return Focus("classic", "Классическая карта", "Classic map", _ => { });
        yield return Focus("single-hero", "Один герой", "Single hero", file => { file.SingleHeroMode = true; file.HeroCountMin = file.HeroCountMax = 1; file.HeroCountIncrement = 0; });
        yield return Focus("capital-hold", "Удержание столицы", "Capital hold", file => { file.VictoryCondition = "win_condition_3"; file.LostStartCityDay = 3; });
        yield return Focus("city-hold-reset", "Удержание города со сбросом таймера", "City hold with timer reset", file => { file.VictoryCondition = "win_condition_5"; file.ProfileOptions.ResetCityHoldTimer = true; });
        yield return Focus("guardian", "Падение стража", "Guardian's Demise", file => file.VictoryCondition = "win_condition_8");
        yield return Focus("final-day17", "Решающая битва на день 17", "Final battle on day 17", file => { file.VictoryCondition = "win_condition_4"; file.ProfileOptions.UseNativeFinalBattleSchedule = true; file.ProfileOptions.FinalBattleDay = 17; file.ProfileOptions.ClassicVictoryEnabled = false; });
        yield return Focus("tournament-save-army", "Турнир с сохранением армии", "Tournament keeping army", file => { file.VictoryCondition = "win_condition_6"; file.TournamentSaveArmy = true; file.ProfileOptions.TournamentBattleDays = [14, 21, 28]; file.ProfileOptions.ClassicVictoryEnabled = false; });
        yield return Focus("tournament-adjacent", "Турнир в соседние дни без сохранения армии", "Adjacent tournament days without army preservation", file => { file.VictoryCondition = "win_condition_6"; file.TournamentSaveArmy = false; file.ProfileOptions.TournamentBattleDays = [2, 3, 4]; file.ProfileOptions.ClassicVictoryEnabled = false; });
        foreach (var (composition, layout) in new[] { ("2v2", AllianceLayout.TeamRegions), ("2v1", AllianceLayout.TeamRegions), ("2v2v1", AllianceLayout.TeamArena), ("1v2v2v2", AllianceLayout.TeamRegions), ("3v3", AllianceLayout.TeamLanes), ("4v4", AllianceLayout.TeamArena) })
            yield return Focus("team-" + composition, "Альянсы " + composition, "Alliances " + composition, file =>
            {
                file.PlayerCount = composition.Split('v').Select(int.Parse).Sum(); file.NeutralZoneCount = file.PlayerCount * 2;
                file.NoDirectPlayerConn = true; file.MinNeutralZonesBetweenPlayers = 1;
                file.ProfileOptions.Symmetry = MapSymmetry.Teams; file.ProfileOptions.Alliances = new() { Enabled = true, Preset = composition, Layout = layout };
            });
        yield return Focus("rivers-growth-off", "Реки без еженедельного роста", "Rivers without weekly growth", file => { file.ProfileOptions.EnableRivers = true; file.ProfileOptions.RiverTributarySpacing = 24; file.ProfileOptions.EnableWeeklyUnitIncrement = false; file.ProfileOptions.InitialUnitIncrement = 0; });
        yield return Focus("growth-full-season", "Рост модификаторов каждую неделю", "Growth modifiers each week", file => file.ProfileOptions.WeekOneOnlyModificatorGrowth = false);
        yield return Focus("magic-disabled", "Отключённое обучение магии и законы", "Disabled spell learning and laws", file => { file.ProfileOptions.DisableMagicGuild = true; file.ProfileOptions.DisableMagicCustomLearning = true; file.ProfileOptions.DisableFactionLaws = true; });
        yield return Focus("area-rich", "Богатая карта с бюджетами по площади", "Rich map with area budgets", file => { file.ProfileOptions.UseAreaBudgets = true; file.ProfileOptions.Richness = MapRichness.VeryRich; });
        yield return Focus("portal-endpoints", "Порталовые объекты у концов связи", "Portal objects at connection endpoints", file => { file.RandomPortals = true; file.MaxPortalConnections = 8; file.ProfileOptions.PortalFromEnabled = false; file.ProfileOptions.PortalToEnabled = true; });
        string ai = references.CustomAI?.Contains("multi_hero_normal") == true ? "multi_hero_normal"
            : throw new InvalidOperationException("Confirmed multi_hero_normal identifier is required for the acceptance AI case.");
        yield return Focus("custom-ai", "Подтверждённый профиль ИИ", "Confirmed AI profile", file => file.ProfileOptions.CustomAI = ai);
    }

    private static AcceptanceCase Focus(string id, string ru, string en, Action<SettingsFile> change, bool manual = true)
    {
        var settings = new SettingsFile { TemplateName = id, MapSize = 192, PlayerCount = 2, NeutralZoneCount = 6,
            NeutralZoneCastles = 1, PlayerStartsWithCastles = true, PlayerZoneCastles = 1, HeroCountMin = 4, HeroCountMax = 10,
            Topology = MapTopology.HubAndSpoke, HubZoneCastles = 1, ProfileOptions = GenerationProfileOptions.CurrentDefaults() };
        settings.ProfileOptions.Tempo = MapTempo.Legacy; change(settings);
        return new(id, "focused", ru, en, settings, manual);
    }

    internal static void VerifySettingsContract(RmgTemplate template, GeneratorSettings settings)
    {
        var profile = settings.ProfileOptions; var rules = template.GameRules ?? throw new InvalidOperationException("Generated rules are absent.");
        var win = rules.WinConditions ?? throw new InvalidOperationException("Generated victory rules are absent.");
        if (template.SizeX != settings.MapSize || template.SizeZ != settings.MapSize) Fail("Map dimensions changed.");
        if (win.Classic != profile.ClassicVictoryEnabled) Fail("Classic victory differs from its requested setting.");
        bool capital = settings.GameEndConditions.LostStartCity || settings.GameEndConditions.VictoryCondition == "win_condition_3";
        bool city = settings.GameEndConditions.CityHold || settings.GameEndConditions.VictoryCondition == "win_condition_5";
        bool guardian = settings.GameEndConditions.LostStartHero || settings.GameEndConditions.VictoryCondition == "win_condition_8";
        if (capital && (win.LostStartCity != true || win.LostStartCityDay != Math.Clamp(settings.GameEndConditions.LostStartCityDay, 1, 30))) Fail("Requested capital hold rules are inactive or changed.");
        if (city && (win.CityHold != true || win.CityHoldDays != Math.Clamp(settings.GameEndConditions.CityHoldDays, 1, 30))) Fail("Requested city hold rules are inactive or changed.");
        if (guardian && win.LostStartHero != true) Fail("Requested starting-hero loss condition is inactive.");
        if (settings.SingleHeroMode && (template.GameMode != "SingleHero" || rules.HeroCountMin != 1 || rules.HeroCountMax != 1 || rules.HeroHireBan != true)) Fail("Single-hero rules were not exported.");
        if (profile.DisableMagicGuild != rules.DisableMagicGuild || profile.DisableMagicCustomLearning != rules.DisableMagicCustomLearning || profile.DisableFactionLaws != rules.DisableFactionLaws) Fail("Magic/faction toggles differ from requested values.");
        if (profile.CustomAI is { Length: > 0 } && rules.CustomAI != profile.CustomAI) Fail("Custom AI identifier changed.");
        if (profile.ResetCityHoldTimer.HasValue && win.IsResetColdownCityHold != profile.ResetCityHoldTimer) Fail("City hold reset setting changed.");
        if (settings.GameEndConditions.VictoryCondition == "win_condition_8" && win.LostStartHero != true) Fail("Guardian victory is inactive.");
        if (profile.UseNativeFinalBattleSchedule && (win.GladiatorArenaDaysDelayStart != profile.FinalBattleDay || win.GladiatorArenaCountDay != 1
            || win.GladiatorArenaRegistrationStartWork != true || win.GladiatorArenaRegistrationStartFight != true)) Fail("Native final battle calendar changed.");
        if (profile.TournamentBattleDays.Count > 0)
        {
            var battles = (win.TournamentAnnounceDays ?? []).Zip(win.TournamentDays ?? [], (announce, duration) => checked(announce + duration));
            if (!battles.SequenceEqual(profile.TournamentBattleDays) || win.TournamentSaveArmy != settings.TournamentRules.SaveArmy) Fail("Tournament dates or army preservation changed.");
        }
        foreach (var variant in template.Variants ?? [])
        {
            var objects = (variant.Zones ?? []).SelectMany(zone => zone.MainObjects ?? []).ToArray();
            if (city && !objects.Any(obj => obj.HoldCityWinCon == true && obj.Type is "City" or "Spawn")) Fail("Requested city hold has no marked town.");
            foreach (int player in Enumerable.Range(1, settings.PlayerCount))
            {
                string spawn = "Player" + player;
                var start = (variant.Zones ?? []).Single(zone => zone.MainObjects?.Any(obj => obj.Type == "Spawn" && obj.Spawn == spawn) == true);
                int towns = (start.MainObjects ?? []).Count(obj => obj.Type == "Spawn" && obj.Spawn == spawn || obj.Type == "City" && obj.Owner == spawn);
                int requested = settings.PlayerStartsWithCastles ? Math.Max(1, settings.ZoneCfg.PlayerZoneCastles) : 1;
                if (towns != requested) Fail("Starting owned-town count differs from the requested count.");
                if ((capital || guardian) && start.MainObjects?.Any(obj => obj.Type == "Spawn" && obj.Spawn == spawn && obj.IsKeyObject == true) != true) Fail("Starting loss objective is not marked as a key object.");
            }
            if (profile.EnableRivers && (variant.River?.CreateRiverSystem != true || variant.River.TributarySpacing != profile.RiverTributarySpacing)) Fail("River settings changed.");
            foreach (var zone in variant.Zones ?? [])
            {
                if (profile.UseAreaBudgets && (zone.GuardedContentValue != 0 || zone.UnguardedContentValue != 0 || zone.ResourcesValue != 0)) Fail("Area-only export retained absolute budgets.");
                if (profile.UseAreaBudgets && settings.ZoneCfg.StructureDensityPercent > 0 && (zone.GuardedContentValuePerArea is not > 0 || zone.UnguardedContentValuePerArea is not > 0)) Fail("Positive area structure density has no guarded/unguarded coefficients.");
                if (profile.UseAreaBudgets && settings.ZoneCfg.ResourceDensityPercent > 0 && zone.ResourcesValuePerArea is not > 0) Fail("Positive area resource density has no resource coefficient.");
                if (profile.EnableWeeklyUnitIncrement is { } weekly && (zone.RandomHireEnableWeeklyUnitIncrement?.Count != 7 || zone.RandomHireEnableWeeklyUnitIncrement.Any(value => value != weekly))) Fail("Weekly hire growth differs from the seven-tier request.");
                if (profile.InitialUnitIncrement is { } initial && (zone.RandomHireInitialUnitIncrement?.Count != 7 || zone.RandomHireInitialUnitIncrement.Any(value => value != initial))) Fail("Initial hire growth changed.");
                foreach (var obj in zone.MainObjects ?? [])
                {
                    if (obj.Type is "Spawn" or "City" or "AbandonedOutpost")
                    {
                        if (profile.EnableWeeklyUnitIncrement.HasValue && obj.EnableWeeklyUnitIncrement != profile.EnableWeeklyUnitIncrement) Fail("Town weekly growth changed.");
                        if (profile.WeekOneOnlyModificatorGrowth.HasValue && obj.WeekOneOnlyModificatorGrowth != profile.WeekOneOnlyModificatorGrowth) Fail("Week-one modifier growth changed.");
                        if (profile.InitialUnitIncrement.HasValue && obj.InitialUnitIncrement != profile.InitialUnitIncrement) Fail("Town initial growth changed.");
                    }
                    if (settings.GameEndConditions.VictoryCondition == "win_condition_8" && obj.Type == "Spawn" && obj.IsKeyObject != true) Fail("Guardian start is not a key object.");
                }
            }
            foreach (var portal in (variant.Connections ?? []).Where(connection => connection.ConnectionType == "Portal"))
                if (portal.PortalFromEnabled != profile.PortalFromEnabled || portal.PortalToEnabled != profile.PortalToEnabled) Fail("Portal direction flags changed.");
        }
        if (profile.Alliances.Enabled)
        {
            var alliance = AlliancePlan.Resolve(settings.PlayerCount, profile.Alliances, settings.Seed);
            if (template.SpawnInfos is null || !JsonSerializer.Serialize(alliance.ToSpawnInfos(), JsonExport.Options)
                .Equals(JsonSerializer.Serialize(template.SpawnInfos, JsonExport.Options), StringComparison.Ordinal)
                || template.MaxAlliancesCount != alliance.MaximumAlliances) Fail("Alliance memberships, locks or maximum count differ from the request.");
        }
        static void Fail(string message) => throw new InvalidOperationException(message);
    }

    private static string Checklist(PatchAcceptanceReceipt receipt, int manualCount, bool english)
    {
        var text = new StringBuilder();
        text.AppendLine(english ? "# Olden Era patch acceptance kit" : "# Набор приёмки обновления Olden Era");
        text.AppendLine(); text.AppendLine(english ? $"Offline result: {receipt.OfflinePassed}. Corpus: {receipt.Corpus.Count}; generated cases: {receipt.Generation.Count}; manual maps: {manualCount}." : $"Офлайн результат: {receipt.OfflinePassed}. Корпус: {receipt.Corpus.Count}; случаев генерации: {receipt.Generation.Count}; карт для игры: {manualCount}.");
        int preserved = receipt.Corpus.Count(item => item.PreservesValues && item.Error is null), rejected = receipt.Corpus.Count(item => item.SafelyRejected && item.Error is null);
        text.AppendLine(english ? $"Corpus values preserved: {preserved}; ambiguous inputs safely rejected without changing the source: {rejected}. Rejection is not a lossless round-trip." : $"Значения корпуса сохранены: {preserved}; неоднозначных файлов безопасно отклонено без изменения оригинала: {rejected}. Отказ от импорта не является round-trip без потерь.");
        text.AppendLine(english ? "Game acceptance is pending. The suite did not launch the game or write into its installation." : "Игровая приёмка ожидается. Проверка не запускала игру и не записывала файлы в её папку.");
        text.AppendLine(); text.AppendLine(english ? "1. Read acceptance-report.json. Legacy corpus warnings are separate from generated-template strict failures." : "1. Прочитайте acceptance-report.json. Предупреждения исходного корпуса отделены от строгих ошибок новых шаблонов.");
        text.AppendLine(english ? "2. Load the .oetgs files from manual-kit into Aurora; confirm all visible options and preview, then inspect the paired .rmg.json." : "2. Загрузите .oetgs из manual-kit в Aurora; проверьте видимые настройки и предпросмотр, затем соответствующий .rmg.json.");
        text.AppendLine(english ? "3. Manually install selected copies using your normal workflow. Select the map in lobby/hotseat, verify card/tags and unlocked/locked teams." : "3. Установите выбранные копии вручную обычным способом. Выберите карту в lobby/hotseat, проверьте карточку, теги и закрепление альянсов.");
        text.AppendLine(english ? "4. Generate at least three actual maps per scenario; Aurora template seeds do not fix the game's map seed. Verify start towns/heroes, neutral travel, portals, rivers and balanced team branches." : "4. Сгенерируйте минимум три настоящие карты на сценарий: seed шаблона Aurora не фиксирует seed карты игры. Проверьте города и героев на старте, проходы, порталы, реки и условия команд.");
        text.AppendLine(english ? "5. Advance to scheduled battle days and at least the second week. Verify army preservation, growth, spell learning, capital/city timers and Guardian defeat." : "5. Дойдите до дат боёв и минимум второй недели. Проверьте сохранение армии, прирост, обучение магии, таймеры столицы/города и поражение при потере стража.");
        text.AppendLine(english ? "6. Save, reload and continue; record exact game version/seed, result and observations in manual-results.json. Keep screenshots/logs for failures. Release requires separate approval." : "6. Сохраните игру, загрузите и продолжите; внесите точную версию/seed, результат и наблюдения в manual-results.json. Для ошибок сохраните скриншоты/логи. Релиз требует отдельного согласования.");
        text.AppendLine(); foreach (var failure in receipt.Generation.Where(item => !item.Success)) text.AppendLine($"- {failure.Id}:{failure.Seed}: {failure.Error}");
        foreach (var item in receipt.Corpus.Where(item => item.SafelyRejected)) text.AppendLine($"- {item.FileName}: {(english ? "safely rejected" : "безопасно отклонён")}; {item.RejectionReason}");
        foreach (var failure in receipt.Corpus.Where(item => !item.PassedOfflineAudit)) text.AppendLine($"- {failure.FileName}: {failure.Error ?? string.Join("; ", failure.Differences.Select(item => item.Path + ": " + item.Message))}");
        return text.ToString();
    }
    private static void EnsureOutside(string destination, string root)
    {
        var game = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (destination.Equals(game, StringComparison.OrdinalIgnoreCase) || destination.StartsWith(game + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Acceptance output cannot write inside the installed game or source corpus.");
        for (var current = new DirectoryInfo(destination); current is not null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Acceptance output must not traverse a directory link.");
    }
    private static void VerifySourceFingerprints(GameInstallation source)
    {
        if (string.IsNullOrEmpty(source.SourceFingerprint) || string.IsNullOrEmpty(source.SchemaFingerprint))
            throw new InvalidOperationException("The acceptance source needs actual Core and native schema fingerprints.");
        foreach (var (path, expected) in new[] { (source.CoreZipPath, source.SourceFingerprint), (source.SchemaPath, source.SchemaFingerprint) })
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Installed source fingerprint changed: " + Path.GetFileName(path) + "; refresh the catalog before acceptance.");
        }
    }
    internal static string[] EnumerateCorpus(string directory) => Directory.EnumerateFiles(directory, "*.rmg.json", SearchOption.AllDirectories)
        .Order(StringComparer.OrdinalIgnoreCase).ToArray();
    private static string NumericFingerprint(RmgTemplate template, bool combat)
    {
        var values = new List<object>();
        foreach (var variant in template.Variants ?? [])
        {
            foreach (var zone in (variant.Zones ?? []).OrderBy(zone => zone.Name, StringComparer.Ordinal))
                if (combat) values.Add(new { zone.Name, zone.GuardMultiplier, zone.GuardWeeklyIncrement, Objects = (zone.MainObjects ?? []).Select(obj => new { obj.GuardValue, obj.GuardWeeklyIncrement }).ToArray() });
                else values.Add(new { zone.Name, zone.GuardedContentValue, zone.GuardedContentValuePerArea, zone.UnguardedContentValue, zone.UnguardedContentValuePerArea, zone.ResourcesValue, zone.ResourcesValuePerArea });
            if (combat) foreach (var edge in (variant.Connections ?? []).OrderBy(edge => edge.Name, StringComparer.Ordinal)) values.Add(new { edge.Name, edge.GuardValue, edge.GuardWeeklyIncrement });
        }
        return Hash(JsonSerializer.SerializeToUtf8Bytes(values, JsonExport.Options));
    }
    private static void VerifyTuningEffects(List<PatchGenerationReceipt> generated)
    {
        foreach (int seed in FixedSeeds)
        foreach (var (prefix, expected, combat) in new[] { ("richness-", 5, false), ("challenge-", 6, true) })
        {
            var indices = Enumerable.Range(0, generated.Count).Where(index => generated[index].Seed == seed && generated[index].Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            if (indices.Length != expected || indices.Any(index => !generated[index].Success)) continue;
            int distinct = indices.Select(index => combat ? generated[index].CombatFingerprint : generated[index].EconomyFingerprint).Distinct(StringComparer.Ordinal).Count();
            if (distinct == expected) continue;
            foreach (int index in indices) generated[index] = generated[index] with { Success = false, Error = "The selected " + prefix.TrimEnd('-') + " levels do not produce distinct numerical effects." };
        }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Write(string path, string content)
    { if (File.Exists(path)) throw new IOException("Acceptance output already exists: " + path); AtomicFile.WriteAllText(path, content); }
    private sealed record AcceptanceCase(string Id, string Category, string NameRu, string NameEn, SettingsFile Settings, bool Manual);
}
