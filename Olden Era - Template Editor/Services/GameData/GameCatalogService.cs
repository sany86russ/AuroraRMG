using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Olden_Era___Template_Editor.Services.GameData
{
    /// <summary>
    /// Locates the installed game's <c>Core.zip</c>, builds a <see cref="GameCatalog"/> from it
    /// (heroes with localized names, factions, icon SIDs) and caches the result on disk so the
    /// expensive extraction happens only once. Everything degrades gracefully: if the game is not
    /// installed, callers get an empty catalog and fall back to the built-in verified lists.
    /// </summary>
    public sealed class GameCatalogService
    {
        public static GameCatalogService Instance { get; } = new();

        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly Dictionary<string, GameCatalog> _memory = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Relative path from a Steam library root to the game's Core.zip.</summary>
        private const string RelativeCorePath =
            @"steamapps\common\Heroes of Might and Magic Olden Era\HeroesOldenEra_Data\StreamingAssets\Core.zip";

        public string? CoreZipPath { get; private set; }
        public bool GameFound => CoreZipPath is not null && File.Exists(CoreZipPath);

        private static string CacheDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "AuroraRMG", "catalog");

        /// <summary>
        /// Returns the catalog for <paramref name="language"/>. Uses an in-memory copy, then a fresh
        /// on-disk cache, then builds from the game files. Never throws — returns an empty catalog on failure.
        /// </summary>
        public async Task<GameCatalog> GetCatalogAsync(string language = "russian")
        {
            language = string.IsNullOrWhiteSpace(language) ? "russian" : language.ToLowerInvariant();

            if (_memory.TryGetValue(language, out var cached))
                return cached;

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_memory.TryGetValue(language, out cached))
                    return cached;

                var catalog = await Task.Run(() => LoadOrBuild(language)).ConfigureAwait(false);
                _memory[language] = catalog;
                return catalog;
            }
            catch
            {
                var empty = new GameCatalog { Language = language };
                _memory[language] = empty;
                return empty;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Non-blocking lookup of a hero's localized name + faction from an already-loaded catalog.
        /// Returns false if the catalog for that language is not in memory yet, or the hero is unknown.
        /// </summary>
        public bool TryResolveHero(string sid, out string name, out string faction, out string iconSid, string language = "russian")
        {
            name = ""; faction = ""; iconSid = "";
            language = string.IsNullOrWhiteSpace(language) ? "russian" : language.ToLowerInvariant();
            if (!_memory.TryGetValue(language, out var cat)) return false;
            foreach (var h in cat.Heroes)
            {
                if (string.Equals(h.Sid, sid, StringComparison.Ordinal))
                {
                    name = h.Name; faction = h.Faction; iconSid = h.IconSid;
                    return true;
                }
            }
            return false;
        }

        private GameCatalog LoadOrBuild(string language)
        {
            string? core = LocateCoreZip();
            CoreZipPath = core;

            long stamp = core is not null && File.Exists(core)
                ? File.GetLastWriteTimeUtc(core).Ticks
                : 0;

            // 1) Fresh disk cache?
            var disk = TryLoadCache(language);
            if (disk is not null && disk.SourceStamp == stamp && !disk.IsEmpty)
                return disk;

            // 2) No game → empty catalog (callers fall back to built-in lists).
            if (core is null || !File.Exists(core))
                return new GameCatalog { Language = language };

            // 3) Build from Core.zip.
            var catalog = new GameCatalog { Language = language, SourceStamp = stamp };
            using (var fs = new FileStream(core, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                catalog.Heroes = CatalogBuilder.BuildHeroes(zip, language);
            }

            TrySaveCache(language, catalog);
            return catalog;
        }

        // ── Disk cache ─────────────────────────────────────────────────────────

        private static string CachePath(string language) =>
            Path.Combine(CacheDir, $"heroes-{language}.json");

        private static GameCatalog? TryLoadCache(string language)
        {
            try
            {
                string path = CachePath(language);
                if (!File.Exists(path)) return null;
                return JsonSerializer.Deserialize<GameCatalog>(File.ReadAllText(path));
            }
            catch { return null; }
        }

        private static void TrySaveCache(string language, GameCatalog catalog)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                File.WriteAllText(CachePath(language),
                    JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = false }));
            }
            catch { /* cache is best-effort */ }
        }

        // ── Install location ─────────────────────────────────────────────────────

        /// <summary>Finds Core.zip across Steam libraries, the registry and common drives. Null if not installed.</summary>
        public string? LocateCoreZip()
        {
            foreach (var root in CandidateLibraryRoots())
            {
                try
                {
                    string candidate = Path.Combine(root, RelativeCorePath);
                    if (File.Exists(candidate)) return candidate;
                }
                catch { /* skip malformed path */ }
            }
            return null;
        }

        private static IEnumerable<string> CandidateLibraryRoots()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string? steam = ReadSteamPath();
            if (steam is not null && seen.Add(steam))
            {
                yield return steam;
                foreach (var lib in ReadSteamLibraries(steam))
                    if (seen.Add(lib)) yield return lib;
            }

            // Common fallbacks (covers the documented G:\Steam install and typical layouts).
            foreach (var drive in new[] { "C", "D", "E", "F", "G", "H" })
            {
                foreach (var rel in new[] { @":\Steam", @":\SteamLibrary", @":\Games\Steam", @":\Program Files (x86)\Steam" })
                {
                    string p = drive + rel;
                    if (seen.Add(p)) yield return p;
                }
            }
        }

        private static string? ReadSteamPath()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key?.GetValue("SteamPath") is string p && p.Length > 0)
                    return p.Replace('/', '\\');
            }
            catch { /* registry unavailable */ }
            return null;
        }

        private static IEnumerable<string> ReadSteamLibraries(string steamPath)
        {
            string vdf = Path.Combine(steamPath, @"steamapps\libraryfolders.vdf");
            if (!File.Exists(vdf)) yield break;

            string text;
            try { text = File.ReadAllText(vdf); }
            catch { yield break; }

            // Lines look like:  "path"   "D:\\SteamLibrary"
            foreach (Match m in Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\""))
            {
                string path = m.Groups[1].Value.Replace(@"\\", @"\");
                if (path.Length > 0) yield return path;
            }
        }
    }
}
