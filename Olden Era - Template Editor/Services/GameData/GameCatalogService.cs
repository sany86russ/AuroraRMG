using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Olden_Era___Template_Editor.Services.GameData;

/// <summary>Read the user's installed catalog; offline callers retain their bundled defaults.</summary>
public sealed class GameCatalogService
{
    public static GameCatalogService Instance { get; } = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, GameCatalog> _memory = new(StringComparer.OrdinalIgnoreCase);
    private readonly GameInstallationService _installations;
    private readonly string _cacheDir;
    public string? CoreZipPath { get; private set; }
    public bool GameFound => CoreZipPath is not null && File.Exists(CoreZipPath);

    public GameCatalogService(GameInstallationService? installations = null, string? cacheDirectory = null)
    {
        _installations = installations ?? GameInstallationService.Instance;
        _cacheDir = cacheDirectory ?? Path.Combine(AppDataPaths.Root, "catalog");
    }

    public async Task<GameCatalog> GetCatalogAsync(string? language = null, bool forceRefresh = false)
    {
        language = NormalizeLanguage(language);
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var source = await Task.Run(() => _installations.Discover(forceRefresh)).ConfigureAwait(false);
            CoreZipPath = source?.CoreZipPath;
            if (!forceRefresh && _memory.TryGetValue(language, out var cached) && SameSource(cached.Source, source)) return cached;
            var catalog = await Task.Run(() => LoadOrBuild(language, source, forceRefresh)).ConfigureAwait(false);
            _memory[language] = catalog;
            return catalog;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            var empty = new GameCatalog { Language = language, Warnings = ["Installed catalog could not be loaded: " + ex.Message] };
            _memory[language] = empty;
            return empty;
        }
        finally { _gate.Release(); }
    }

    public bool TryResolveHero(string sid, out string name, out string faction, out string iconSid, string? language = null)
    {
        name = ""; faction = ""; iconSid = "";
        if (!_memory.TryGetValue(NormalizeLanguage(language), out var catalog)) return false;
        var hero = catalog.Heroes.FirstOrDefault(h => h.Sid == sid);
        if (hero is null) return false;
        name = hero.Name; faction = hero.Faction; iconSid = hero.IconSid;
        return true;
    }

    public bool TryResolveContent(string sid, out GameContentEntry? entry, GameContentKind? kind = null, string? language = null)
    {
        entry = _memory.TryGetValue(NormalizeLanguage(language), out var catalog)
            ? catalog.Content.FirstOrDefault(e => e.Sid == sid && (kind is null || e.Kind == kind)) : null;
        return entry is not null;
    }

    public string? LocateCoreZip() => CoreZipPath = _installations.Discover()?.CoreZipPath;

    private GameCatalog LoadOrBuild(string language, GameInstallation? source, bool forceRefresh)
    {
        if (source is null) return new GameCatalog { Language = language, Warnings = _installations.LastWarnings.ToList() };
        var path = CachePath(language, source);
        string? cacheWarning = null;
        if (!forceRefresh)
        {
            var disk = TryLoadCache(path, out cacheWarning);
            if (disk?.SnapshotFormatVersion == 4 && disk.Language == language && SameSource(disk.Source, source) && !disk.IsEmpty)
            {
                // Source capability evidence always comes from the current locator, never a cache's editable metadata.
                disk.Source = source; disk.SourceStamp = source.SourceStamp;
                return disk;
            }
        }
        var catalog = new GameCatalog { Language = language, SourceStamp = source.SourceStamp, Source = source, Warnings = source.Warnings.ToList() };
        if (cacheWarning is not null) catalog.Warnings.Add(cacheWarning);
        using (var stream = new FileStream(source.CoreZipPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            catalog.Heroes = CatalogBuilder.BuildHeroes(zip, language, catalog.Warnings);
            catalog.Content = GameContentCatalogBuilder.Build(zip, catalog.Warnings);
            catalog.References = GameReferenceSnapshotBuilder.Build(zip, source, catalog.Warnings);
        }
        TrySaveCache(path, catalog);
        return catalog;
    }

    private string CachePath(string language, GameInstallation source)
    {
        // Separate installs with identical timestamps cannot accidentally share cached definitions.
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.CoreZipPath.ToUpperInvariant())))[..16];
        return Path.Combine(_cacheDir, $"catalog-{language}-{identity}.json");
    }
    private static GameCatalog? TryLoadCache(string path, out string? warning)
    {
        warning = null;
        if (!File.Exists(path)) return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!HasRequiredCacheMembers(document.RootElement))
                throw new JsonException("Required cache members are missing, null or have the wrong shape.");
            var catalog = document.RootElement.Deserialize<GameCatalog>();
            if (catalog is null || !HasValidCacheValues(catalog))
                throw new JsonException("Catalog cache contains invalid or null entries.");
            return catalog;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { warning = "Catalog disk cache was rejected; installed game data is being rebuilt. " + ex.Message; return null; }
    }

    private static bool HasRequiredCacheMembers(JsonElement root)
    {
        if (!Members(root, ("language", JsonValueKind.String), ("SnapshotFormatVersion", JsonValueKind.Number),
            ("heroes", JsonValueKind.Array), ("Content", JsonValueKind.Array), ("Warnings", JsonValueKind.Array),
            ("Source", JsonValueKind.Object), ("References", JsonValueKind.Object))) return false;
        var source = root.GetProperty("Source");
        if (!Members(source, ("InstallPath", JsonValueKind.String), ("CoreZipPath", JsonValueKind.String),
            ("TemplatesPath", JsonValueKind.String), ("SchemaPath", JsonValueKind.String), ("VersionEvidence", JsonValueKind.String),
            ("SourceFingerprint", JsonValueKind.String), ("SchemaFingerprint", JsonValueKind.String),
            ("Profile", JsonValueKind.Object), ("Warnings", JsonValueKind.Array))) return false;
        if (!Members(source.GetProperty("Profile"), ("Id", JsonValueKind.String), ("Status", JsonValueKind.Number),
            ("ConfirmedCapabilities", JsonValueKind.Array), ("Evidence", JsonValueKind.String))) return false;
        var references = root.GetProperty("References");
        if (!Members(references, ("Pools", JsonValueKind.Array), ("Lists", JsonValueKind.Array), ("Layouts", JsonValueKind.Array),
            ("Objects", JsonValueKind.Array), ("NativeLayouts", JsonValueKind.Array), ("NativeMandatory", JsonValueKind.Array),
            ("NativeCountLimits", JsonValueKind.Array), ("AiEvidence", JsonValueKind.String))) return false;
        if (!references.TryGetProperty("CoreReadComplete", out var complete) || complete.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
        if (!references.TryGetProperty("CustomAI", out var ai) || ai.ValueKind is not (JsonValueKind.Null or JsonValueKind.Array)) return false;
        foreach (var hero in root.GetProperty("heroes").EnumerateArray())
            if (!Members(hero, ("sid", JsonValueKind.String), ("name", JsonValueKind.String), ("faction", JsonValueKind.String), ("icon", JsonValueKind.String))) return false;
        foreach (var content in root.GetProperty("Content").EnumerateArray())
        {
            if (!Members(content, ("Sid", JsonValueKind.String), ("Kind", JsonValueKind.Number), ("NameRu", JsonValueKind.String),
                ("NameEn", JsonValueKind.String), ("Category", JsonValueKind.String), ("Faction", JsonValueKind.String),
                ("SourceEntry", JsonValueKind.String), ("RewardVariants", JsonValueKind.Array), ("GrantedSpells", JsonValueKind.Array))) return false;
            foreach (var variant in content.GetProperty("RewardVariants").EnumerateArray())
            {
                if (!Members(variant, ("Index", JsonValueKind.Number), ("RewardTypes", JsonValueKind.Array),
                    ("RewardParameters", JsonValueKind.Array), ("Spells", JsonValueKind.Array))) return false;
                foreach (var spell in variant.GetProperty("Spells").EnumerateArray())
                    if (!Members(spell, ("Sid", JsonValueKind.String), ("NameRu", JsonValueKind.String), ("NameEn", JsonValueKind.String))) return false;
            }
        }
        return true;
        static bool Members(JsonElement element, params (string Name, JsonValueKind Kind)[] members) => element.ValueKind == JsonValueKind.Object
            && members.All(member => element.TryGetProperty(member.Name, out var value) && value.ValueKind == member.Kind);
    }

    private static bool HasValidCacheValues(GameCatalog catalog)
    {
        var source = catalog.Source; var profile = source?.Profile;
        if (source is null || profile is null || source.Warnings is null || source.Warnings.Any(w => w is null)
            || profile.ConfirmedCapabilities is null || !Enum.IsDefined(profile.Status) || profile.ConfirmedCapabilities.Any(c => !Enum.IsDefined(c))
            || catalog.Heroes is null || catalog.Content is null || catalog.Warnings is null || catalog.Warnings.Any(w => w is null)
            || catalog.References is not { } references) return false;
        if (catalog.Heroes.Any(h => h is null || h.Sid.Length == 0)
            || catalog.Heroes.Select(h => h.Sid).Distinct(StringComparer.Ordinal).Count() != catalog.Heroes.Count) return false;
        if (catalog.Content.Any(c => c is null || c.Sid.Length == 0 || !Enum.IsDefined(c.Kind) || c.PlacementVerified || !Strings(c.GrantedSpells)
            || c.RewardVariants is null || c.RewardVariants.Any(v => v is null || v.Index < 0 || !Strings(v.RewardTypes) || !Strings(v.RewardParameters)
                || v.RollChance is { } chance && !double.IsFinite(chance) || v.Value is { } value && !double.IsFinite(value)
                || v.Spells is null || v.Spells.Any(s => s is null || s.Sid.Length == 0))
            || c.RewardVariants.Select(v => v.Index).Distinct().Count() != c.RewardVariants.Count)
            || catalog.Content.Select(c => (c.Kind, c.Sid)).Distinct().Count() != catalog.Content.Count) return false;
        return Identifiers(references.Pools) && Identifiers(references.Lists) && Identifiers(references.Layouts) && Identifiers(references.Objects)
            && Identifiers(references.NativeLayouts) && Identifiers(references.NativeMandatory) && Identifiers(references.NativeCountLimits)
            && (references.CustomAI is null || Identifiers(references.CustomAI));
        static bool Strings(IEnumerable<string>? values) => values is not null && values.All(value => value is not null);
        static bool Identifiers(List<string>? values) => values is not null && values.All(value => !string.IsNullOrEmpty(value))
            && values.Distinct(StringComparer.Ordinal).Count() == values.Count;
    }
    private static void TrySaveCache(string path, GameCatalog catalog)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(path)!); AtomicFile.WriteAllText(path, JsonSerializer.Serialize(catalog)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { catalog.Warnings.Add("Catalog disk cache could not be saved: " + ex.Message); }
    }
    private static bool SameSource(GameInstallation? a, GameInstallation? b) => a is null ? b is null : b is not null &&
        string.Equals(a.CoreZipPath, b.CoreZipPath, StringComparison.OrdinalIgnoreCase) && a.SourceFingerprint == b.SourceFingerprint &&
        a.SteamBuildId == b.SteamBuildId && a.VersionMarker == b.VersionMarker && a.SchemaFingerprint == b.SchemaFingerprint && a.Profile.Id == b.Profile.Id;
    private static string NormalizeLanguage(string? language) => string.IsNullOrWhiteSpace(language)
        ? (Localization.LocalizationManager.Instance.CurrentLanguage == Localization.AppLanguage.En ? "english" : "russian")
        : language.Equals("russian", StringComparison.OrdinalIgnoreCase) ? "russian" : "english";
}
