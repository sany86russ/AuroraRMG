using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Olden_Era___Template_Editor.Services.GameData
{
    /// <summary>One pickable hero, resolved from the game's <c>Core.zip</c> data.</summary>
    public sealed class HeroCatalogEntry
    {
        [JsonPropertyName("sid")]     public string Sid     { get; set; } = "";
        [JsonPropertyName("name")]    public string Name    { get; set; } = "";
        [JsonPropertyName("faction")] public string Faction { get; set; } = "";
        /// <summary>Sprite SID referenced by the hero (e.g. <c>hero_human_8_lord_edgar</c>); used to resolve an icon.</summary>
        [JsonPropertyName("icon")]    public string IconSid { get; set; } = "";
    }

    /// <summary>
    /// A snapshot of game data extracted from the installed game's <c>Core.zip</c>.
    /// Persisted to disk so it is built once and loaded instantly afterwards.
    /// </summary>
    public sealed class GameCatalog
    {
        [JsonPropertyName("language")]   public string Language { get; set; } = "russian";
        [JsonPropertyName("sourceStamp")] public long  SourceStamp { get; set; }
        [JsonPropertyName("heroes")]     public List<HeroCatalogEntry> Heroes { get; set; } = [];

        [JsonIgnore] public bool IsEmpty => Heroes.Count == 0;
    }

    /// <summary>
    /// Pure parsing of the game's <c>Core.zip</c> into a <see cref="GameCatalog"/>.
    /// No file-system / install knowledge here — feed it an open <see cref="ZipArchive"/>
    /// so the logic is unit-testable with a synthetic archive.
    /// </summary>
    public static class CatalogBuilder
    {
        // DB/heroes subfolders that are NOT regular, pickable heroes.
        private static readonly string[] ExcludedHeroFolders =
            ["campaign", "campaign_tutorial", "custom_maps"];

        private static readonly JsonDocumentOptions DocOptions = new()
        {
            AllowTrailingCommas = true,
            CommentHandling     = JsonCommentHandling.Skip,
        };

        /// <summary>Builds the hero catalog from an open Core.zip archive for the given language.</summary>
        public static List<HeroCatalogEntry> BuildHeroes(ZipArchive core, string language)
        {
            var names = ReadHeroNames(core, language);

            var heroes = new List<HeroCatalogEntry>();
            var seen   = new HashSet<string>();

            foreach (var entry in core.Entries)
            {
                if (!entry.FullName.StartsWith("DB/heroes/", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (!entry.FullName.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase)) continue;

                var segments = entry.FullName.Split('/');
                // DB/heroes/<folder>/<file>.json
                if (segments.Length < 4) continue;
                string folder = segments[2];
                if (ExcludedHeroFolders.Contains(folder, System.StringComparer.OrdinalIgnoreCase)) continue;

                foreach (var hero in ParseHeroEntries(entry))
                {
                    if (string.IsNullOrEmpty(hero.Sid) || !seen.Add(hero.Sid)) continue;
                    if (names.TryGetValue(hero.Sid, out var localized) && localized.Length > 0)
                        hero.Name = localized;
                    if (hero.Name.Length == 0)
                        hero.Name = Prettify(hero.Sid);
                    heroes.Add(hero);
                }
            }

            return [.. heroes
                .OrderBy(h => h.Faction, System.StringComparer.OrdinalIgnoreCase)
                .ThenBy(h => h.Name,    System.StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>Reads localized hero display names (<c>sid → text</c>) from Lang/&lt;lang&gt;/texts/heroInfo.json.</summary>
        private static Dictionary<string, string> ReadHeroNames(ZipArchive core, string language)
        {
            var map = new Dictionary<string, string>(System.StringComparer.Ordinal);
            var entry = core.GetEntry($"Lang/{language}/texts/heroInfo.json")
                     ?? core.GetEntry("Lang/english/texts/heroInfo.json");
            if (entry is null) return map;

            try
            {
                using var doc = ReadJson(entry);
                if (doc.RootElement.TryGetProperty("tokens", out var tokens) &&
                    tokens.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tok in tokens.EnumerateArray())
                    {
                        if (tok.TryGetProperty("sid",  out var sid) &&
                            tok.TryGetProperty("text", out var text) &&
                            sid.ValueKind == JsonValueKind.String)
                        {
                            // Store every key; the join only ever looks up bare hero ids,
                            // so _motto / _description / _spec_* keys are harmless noise.
                            string key = sid.GetString() ?? "";
                            if (key.Length > 0)
                                map[key] = text.GetString() ?? "";
                        }
                    }
                }
            }
            catch { /* malformed loc file → fall back to prettified ids */ }
            return map;
        }

        private static IEnumerable<HeroCatalogEntry> ParseHeroEntries(ZipArchiveEntry entry)
        {
            JsonDocument doc;
            try { doc = ReadJson(entry); }
            catch { yield break; }

            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("array", out var arr) ||
                    arr.ValueKind != JsonValueKind.Array)
                    yield break;

                foreach (var h in arr.EnumerateArray())
                {
                    string id      = GetString(h, "id");
                    if (id.Length == 0) continue;
                    string faction = GetString(h, "fraction"); // note: game spells it "fraction"
                    string icon    = GetString(h, "icon");
                    yield return new HeroCatalogEntry
                    {
                        Sid     = id,
                        Faction = Capitalize(faction),
                        IconSid = icon,
                        Name    = "",
                    };
                }
            }
        }

        private static JsonDocument ReadJson(ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            return JsonDocument.Parse(reader.ReadToEnd(), DocOptions);
        }

        private static string GetString(JsonElement obj, string prop)
            => obj.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? ""
                : "";

        private static string Capitalize(string s)
            => s.Length == 0 ? "" : char.ToUpperInvariant(s[0]) + s[1..];

        private static string Prettify(string sid)
        {
            var s = sid.Replace('_', ' ').Trim();
            return s.Length == 0 ? sid : char.ToUpperInvariant(s[0]) + s[1..];
        }
    }
}
