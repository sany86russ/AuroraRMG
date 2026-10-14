using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        private static GamePoolCatalog _catalog = new();
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
                var customFile = new FileInfo(CustomPoolsPath);
                var identity = source?.CoreZipPath + "|" + source?.SourceFingerprint + "|" + source?.SchemaFingerprint + "|" + source?.Profile.Id + "|" + source?.SteamBuildId + "|" + source?.VersionMarker +
                    "|custom:" + (customFile.Exists ? customFile.LastWriteTimeUtc.Ticks + ":" + customFile.Length : "none");
                if (_loaded && !forceRefresh && identity == _sourceIdentity) return;
                _loaded = true;
                _sourceIdentity = identity;
                _catalog = new GamePoolCatalog();
                _warnings = source?.Warnings.ToList() ?? GameInstallationService.Instance.LastWarnings.ToList();
                try
                {
                    if (source is null) SetStatus("S.PV.StatusGameNotFound");
                    else
                    {
                        using var coreStream = new FileStream(source.CoreZipPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        using var zip = new ZipArchive(coreStream, ZipArchiveMode.Read);
                        _catalog = GamePoolCatalog.Read(zip, source);
                        _warnings.AddRange(_catalog.Warnings);
                        SetStatus("S.PV.StatusLoaded", _catalog.Pools.Count, 0, _catalog.ContentLists.Count);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
                {
                    _loaded = false; // a transient read failure is not a permanent source snapshot
                    _warnings.Add("Game pools could not be loaded: " + ex.Message);
                    SetStatus("S.PV.StatusError", ex.Message);
                }
                _allPools = _catalog.Pools.ToList();
                _allContentLists = _catalog.ContentLists.ToList();
                LoadCustomPools(); // local definitions remain available when the game is absent
                if (_allContentLists.Count > 0)
                    SetStatus("S.PV.StatusLoaded", _catalog.Pools.Count, _customPools.Count, _allContentLists.Count);
            }
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
                _sourceIdentity = "";
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

        public static List<PoolItemInfo> GetPoolItems(GamePool pool, RmgTemplate? template = null)
        {
            return InspectPool(pool, template).Items.Select(item =>
            {
                if (item.IsMissing) item.Sid = LocalizationManager.T("S.PV.ListNotFound");
                if (item.ListName.Length == 0) item.ListName = LocalizationManager.T("S.PV.GroupWeight", item.GroupWeight);
                return item;
            }).ToList();
        }

        public static Olden_Era___Template_Editor.Services.ContentPools.PoolContentInspection InspectPool(GamePool pool, RmgTemplate? template = null)
        { Load(); return Olden_Era___Template_Editor.Services.ContentPools.PoolContentInspector.Inspect(pool, _catalog, template); }
    }
}
