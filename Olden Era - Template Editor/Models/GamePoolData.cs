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
        public int Weight { get; set; }
        [JsonPropertyName("includeLists")]
        public List<string> IncludeLists { get; set; } = new();
        [JsonPropertyName("content")]
        public List<PoolContentItem>? Content { get; set; }
    }

    public class PoolContentItem : RmgNode
    {
        [JsonPropertyName("sid")]
        public string Sid { get; set; } = "";
        [JsonPropertyName("weight")]
        public int Weight { get; set; }
        [JsonPropertyName("biome")]
        public string? Biome { get; set; }
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
        public string ListName { get; set; } = "";
        public string Sid { get; set; } = "";
        public int Weight { get; set; }
        public string? Biome { get; set; }
        public int GroupWeight { get; set; }
        public bool IsMissing { get; set; }
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
        private static string _statusKey = "";
        private static object[] _statusArgs = [];
        private static void SetStatus(string key, params object[] args) { _statusKey = key; _statusArgs = args; }

        public static string Status => LocalizationManager.T(_statusKey, _statusArgs);

        private static string CustomPoolsPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AuroraRMG", "custom_pools.json");

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            _allPools = new List<GamePool>();
            _allContentLists = new List<ContentListEntry>();

            try
            {
                var core = GameCatalogService.Instance.LocateCoreZip();
                if (core is null || !File.Exists(core))
                {
                    SetStatus("S.PV.StatusGameNotFound");
                }
                else
                {
                    using var zip = ZipFile.OpenRead(core);
                    foreach (var entry in zip.Entries)
                    {
                        var name = entry.FullName.Replace('\\', '/');
                        if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

                        if (name.StartsWith("generator/content_pools/", StringComparison.OrdinalIgnoreCase))
                        {
                            var pools = DeserializeEntry<List<GamePool>>(entry);
                            if (pools != null) _allPools.AddRange(pools.Where(p => !string.IsNullOrWhiteSpace(p.Name)));
                        }
                        else if (name.StartsWith("generator/content_lists/", StringComparison.OrdinalIgnoreCase))
                        {
                            var lists = DeserializeEntry<List<ContentListEntry>>(entry);
                            if (lists != null) _allContentLists.AddRange(lists.Where(l => !string.IsNullOrWhiteSpace(l.Name)));
                        }
                    }

                    SetStatus("S.PV.StatusLoaded",
                        _allPools.Count, _customPools.Count, _allContentLists.Count);
                }
            }
            catch (Exception ex)
            {
                SetStatus("S.PV.StatusError", ex.Message);
            }
            LoadCustomPools(); // local definitions remain available even when the game is not installed
            if (_allContentLists.Count > 0)
                SetStatus("S.PV.StatusLoaded", _allPools.Count, _customPools.Count, _allContentLists.Count);
        }

        private static T? DeserializeEntry<T>(ZipArchiveEntry entry) where T : class
        {
            try
            {
                using var s = entry.Open();
                using var reader = new StreamReader(s);
                return JsonSerializer.Deserialize<T>(reader.ReadToEnd(), ReadOptions);
            }
            catch
            {
                return null; // skip malformed files, keep going
            }
        }

        public static void AddPool(GamePool pool)
        {
            Load();
            _allPools ??= new List<GamePool>();
            if (_allPools.Any(p => p.Name == pool.Name))
                throw new InvalidOperationException(LocalizationManager.T("S.PC.Duplicate"));
            _allPools.Add(pool);
            _customPools.Add(pool);
            SaveCustomPools();
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
                    _customPools = pools;
                    _allPools!.AddRange(_customPools);
                }
            }
            catch { /* ignore corrupt custom pools */ }
        }

        private static void SaveCustomPools()
        {
            try
            {
                var path = CustomPoolsPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                Olden_Era___Template_Editor.Services.AtomicFile.WriteAllText(path, JsonSerializer.Serialize(_customPools, Olden_Era___Template_Editor.Services.JsonExport.Options));
            }
            catch { /* best effort */ }
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

        public static List<PoolItemInfo> GetPoolItems(GamePool pool)
        {
            var result = new List<PoolItemInfo>();
            if (pool.Groups == null) return result;

            foreach (var group in pool.Groups)
            {
                foreach (var listName in group.IncludeLists)
                {
                    var list = GetContentList(listName);
                    if (list != null)
                    {
                        foreach (var item in list.Content)
                        {
                            result.Add(new PoolItemInfo
                            {
                                ListName = listName,
                                Sid = item.Sid,
                                Weight = item.Weight,
                                Biome = item.Biome,
                                GroupWeight = group.Weight,
                            });
                        }
                    }
                    else
                    {
                        result.Add(new PoolItemInfo
                        {
                            ListName = listName,
                            Sid = LocalizationManager.T("S.PV.ListNotFound"),
                            Weight = group.Weight,
                            GroupWeight = group.Weight,
                            IsMissing = true,
                        });
                    }
                }

                if (group.Content != null)
                {
                    foreach (var item in group.Content)
                    {
                        result.Add(new PoolItemInfo
                        {
                            ListName = LocalizationManager.T("S.PV.GroupWeight", group.Weight),
                            Sid = item.Sid,
                            Weight = item.Weight,
                            Biome = item.Biome,
                            GroupWeight = group.Weight,
                        });
                    }
                }
            }

            return result;
        }
    }
}
