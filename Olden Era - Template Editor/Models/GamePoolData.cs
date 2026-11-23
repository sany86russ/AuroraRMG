using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Olden_Era___Template_Editor.Services.ContentPools;
using Olden_Era___Template_Editor.Services.GameData;
using Olden_Era___Template_Editor.Services.Localization;

namespace OldenEraTemplateEditor.Models
{
    // ── Pool / content-list data model (mirrors the game's generator JSON) ─────────

    public class PoolGroup : RmgNode
    {
        [JsonPropertyName("weight")]
        public double Weight { get; set; }
        [JsonPropertyName("includeLists")]
        public List<string> IncludeLists { get; set; } = new();
        [JsonPropertyName("content")]
        public List<PoolContentItem>? Content { get; set; }
    }

    public class PoolContentItem : RmgNode
    {
        [JsonPropertyName("sid")]
        public string Sid { get; set; } = "";
        [JsonIgnore]
        public double Weight { get => SerializedWeight ?? 1; set => SerializedWeight = value; }
        // ContentWeight's native constructor uses 1f. Keep omission distinct from an explicit disabled weight of zero.
        [JsonInclude, JsonPropertyName("weight"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        internal double? SerializedWeight
        {
            get => _serializedWeight;
            set => _serializedWeight = value ?? throw new JsonException("A content weight must be a number; explicit null is invalid.");
        }
        private double? _serializedWeight;
        [JsonPropertyName("biome")]
        public string? Biome { get; set; }
        [JsonPropertyName("variant")]
        public int? Variant { get; set; }
    }

    public class GamePool : RmgNode
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("groups")]
        public List<PoolGroup> Groups { get; set; } = new();
        public override string ToString() => Name;
    }

    public class ContentListEntry : RmgNode
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("content")]
        public List<PoolContentItem> Content { get; set; } = new();
        public override string ToString() => Name;
    }

    public class PoolItemInfo
    {
        public int GroupIndex { get; set; }
        public string ListName { get; set; } = "";
        public string Sid { get; set; } = "";
        public double Weight { get; set; }
        public string? Biome { get; set; }
        public double GroupWeight { get; set; }
        public bool IsMissing { get; set; }
        public int? Variant { get; set; }
        /// <summary>Relative configured weight; it does not guarantee a generated object count.</summary>
        public double? RelativeGroupShare { get; set; }
        public double? RelativeWithinListShare { get; set; }
    }

    /// <summary>
    /// Reads the game's content pools and content lists straight from the installed
    /// <c>Core.zip</c> (<c>generator/content_pools/*.json</c> + <c>generator/content_lists/*.json</c>).
    /// No game data is bundled with the app — it is read at runtime from the user's own install
    /// (the same approach as <see cref="GameCatalogService"/>). User-created pools are persisted to
    /// <c>%LOCALAPPDATA%\AuroraRMG\custom_pools.json</c>.
    /// </summary>
    public static class GamePoolDataLoader
    {
        private static List<GamePool>? _allPools;
        private static List<ContentListEntry>? _allContentLists;
        private static List<GamePool> _customPools = new();
        private static bool _loaded;
        private static readonly object Gate = new();
        private static string _sourceIdentity = "";
        private static string _customIdentity = "";
        private static GamePoolCatalog _catalog = new();
        private static List<string> _installedWarnings = [];
        private static List<string> _warnings = [];
        public static GameInstallation? Source { get { Load(); return _catalog.Source; } }
        public static IReadOnlyList<string> Warnings { get { Load(); return _warnings.ToArray(); } }
        private static string _statusKey = "";
        private static object[] _statusArgs = [];
        private static void SetStatus(string key, params object[] args) { _statusKey = key; _statusArgs = args; }

        public static string Status => LocalizationManager.T(_statusKey, _statusArgs);

        private static string CustomPoolsPath =>
            Path.Combine(AppDataPaths.Root, "custom_pools.json");

        private static readonly JsonSerializerOptions ReadOptions = new(Olden_Era___Template_Editor.Services.JsonExport.Options)
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static void Load(bool forceRefresh = false)
        {
            lock (Gate)
            {
                var source = GameInstallationService.Instance.Discover(forceRefresh);
                bool installedChanged = !_loaded || forceRefresh || SourceIdentity(source) != _sourceIdentity;
                var customFile = new FileInfo(CustomPoolsPath);
                string customIdentity = customFile.FullName + "|" +
                    (customFile.Exists ? customFile.LastWriteTimeUtc.Ticks + ":" + customFile.Length : "none");
                if (!installedChanged && customIdentity == _customIdentity) return;
                if (installedChanged) LoadInstalledPools(source);

                _warnings = _installedWarnings.ToList();
                _allPools = _catalog.Pools.ToList();
                _allContentLists = _catalog.ContentLists.ToList();
                LoadCustomPools(); // local definitions remain available when the game is absent
                _customIdentity = customIdentity;
                if (_catalog.Source is not null)
                    SetStatus("S.PV.StatusLoaded", _catalog.Pools.Count, _customPools.Count, _allContentLists.Count);
            }
        }

        private static string SourceIdentity(GameInstallation? source) =>
            source?.CoreZipPath + "|" + source?.SourceFingerprint + "|" + source?.SchemaFingerprint + "|" +
            source?.Profile.Id + "|" + source?.SteamBuildId + "|" + source?.VersionMarker;

        private static void LoadInstalledPools(GameInstallation? source)
        {
            var installations = GameInstallationService.Instance;
            _loaded = false;
            _catalog = new GamePoolCatalog();
            try
            {
                try { _catalog = ReadInstalledPools(source); }
                catch (InstalledGameDataChangedException)
                {
                    source = installations.Discover(forceRefresh: true);
                    _catalog = ReadInstalledPools(source);
                }
                _sourceIdentity = SourceIdentity(source);
                _installedWarnings = source?.Warnings.ToList() ?? installations.LastWarnings.ToList();
                _installedWarnings.AddRange(_catalog.Warnings);
                _loaded = true;
                if (source is null) SetStatus("S.PV.StatusGameNotFound");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
            {
                _installedWarnings = source?.Warnings.ToList() ?? installations.LastWarnings.ToList();
                _installedWarnings.Add("Game pools could not be loaded: " + ex.Message);
                SetStatus("S.PV.StatusError", ex.Message);
            }
        }

        private static GamePoolCatalog ReadInstalledPools(GameInstallation? source)
        {
            if (source is null) return new();
            using var stream = GameInstallationService.OpenVerifiedDataFile(source.CoreZipPath, source.SourceFingerprint);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            return GamePoolCatalog.Read(archive, source);
        }

        public static PoolReferenceResolution ResolvePool(string sid, RmgTemplate? template = null)
        {
            Load();
            return _catalog.ResolvePool(sid, template, _customPools);
        }
        public static IReadOnlyList<string> ValidateReferences(RmgTemplate template)
        { Load(); return _catalog.ValidateReferences(template, _customPools); }
        public static void EmbedSelected(RmgTemplate template, IEnumerable<string> selected)
        { Load(); _catalog.EmbedSelected(template, selected, _customPools); }

        public static void AddPool(GamePool pool)
        {
            lock (Gate)
            {
                Load();
                _allPools ??= new List<GamePool>();
                if (_allPools.Any(p => p.Name == pool.Name))
                    throw new InvalidOperationException(LocalizationManager.T("S.PC.Duplicate"));
                var updated = _customPools.Append(pool).ToList();
                SaveCustomPools(updated); // persist first; a failure must reach the caller
                _customPools = updated;
                _allPools.Add(pool);
                _customIdentity = "";
            }
        }

        private static void LoadCustomPools()
        {
            _customPools = new List<GamePool>();
            var path = CustomPoolsPath;
            if (!File.Exists(path)) return;
            try
            {
                var pools = JsonSerializer.Deserialize<List<GamePool>>(File.ReadAllText(path), ReadOptions);
                if (pools != null)
                {
                    _customPools = FilterCustomPools(pools, _warnings);
                    foreach (var pool in _customPools)
                    {
                        _allPools!.RemoveAll(p => p.Name == pool.Name);
                        _allPools.Add(pool);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            { _warnings.Add("Custom pools could not be loaded: " + ex.Message); }
        }

        internal static List<GamePool> FilterCustomPools(IEnumerable<GamePool> pools, ICollection<string> warnings)
        {
            var result = new List<GamePool>();
            foreach (var pool in pools)
                if (pool is null) warnings.Add("A null custom pool entry was ignored.");
                else if (string.IsNullOrWhiteSpace(pool.Name)) warnings.Add("A custom pool with no name was ignored.");
                else result.Add(pool);
            return result;
        }

        private static void SaveCustomPools(List<GamePool> pools)
        {
            var path = CustomPoolsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            Olden_Era___Template_Editor.Services.AtomicFile.WriteAllText(path, JsonSerializer.Serialize(pools, Olden_Era___Template_Editor.Services.JsonExport.Options));
        }

        public static List<GamePool> GetAllPools()
        {
            Load();
            return _allPools ?? new List<GamePool>();
        }

        public static List<ContentListEntry> GetAllContentLists()
        {
            Load();
            return _allContentLists ?? new List<ContentListEntry>();
        }

        public static ContentListEntry? GetContentList(string name)
        {
            Load();
            return _allContentLists?.FirstOrDefault(l => l.Name == name);
        }

        public static List<PoolItemInfo> GetPoolItems(GamePool pool, RmgTemplate? template = null) =>
            FormatPoolItems(InspectPool(pool, template));

        internal static List<PoolItemInfo> FormatPoolItems(PoolContentInspection inspection)
        {
            return inspection.Items.Select(item =>
            {
                if (item.IsMissing) item.Sid = LocalizationManager.T("S.PV.ListNotFound");
                if (item.ListName.Length == 0) item.ListName = LocalizationManager.T("S.PV.GroupWeight", item.GroupWeight);
                return item;
            }).ToList();
        }

        public static PoolContentInspection InspectPool(GamePool pool, RmgTemplate? template = null)
        { Load(); return PoolContentInspector.Inspect(pool, _catalog, template); }
    }
}
