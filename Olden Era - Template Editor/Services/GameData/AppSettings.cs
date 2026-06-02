using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Olden_Era___Template_Editor.Services.GameData
{
    /// <summary>
    /// Global, machine-local application preferences (not part of a template's .oetgs).
    /// Persisted to <c>%LOCALAPPDATA%\AuroraRMG\settings.json</c>. Everything degrades gracefully.
    /// </summary>
    public sealed class AppSettings
    {
        /// <summary>
        /// Opt-in: read the installed game's assets (Core.zip + sharedassets) to show the full
        /// localized hero roster and real in-game icons. OFF by default — the app works fully
        /// out of the box without the game, using the built-in verified lists and coloured dots.
        /// </summary>
        [JsonPropertyName("useGameAssets")]
        public bool UseGameAssets { get; set; }

        /// <summary>UI language: "ru" / "en" / "" (empty = auto-detect from Windows locale on first run).</summary>
        [JsonPropertyName("language")]
        public string Language { get; set; } = "";

        /// <summary>
        /// Last-used editor mode: "simple" (Quick Generate) or "advanced" (the full editor).
        /// Default "simple" so newcomers land in the few-clicks experience; remembered across runs.
        /// </summary>
        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "simple";

        /// <summary>Last-used Simple Mode option selections, restored on the next launch.</summary>
        [JsonPropertyName("simple")]
        public SimpleModeState Simple { get; set; } = new();

        // ── Singleton-ish access ────────────────────────────────────────────────
        private static AppSettings? _current;
        public static AppSettings Current => _current ??= Load();

        private static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuroraRMG");
        private static string FilePath => Path.Combine(Dir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
            catch { /* corrupt/locked → defaults */ }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
            }
            catch { /* best effort */ }
        }
    }

    /// <summary>Persisted Simple Mode selections (combo indices + toggles). Seed is intentionally
    /// not stored — each launch starts on a fresh random seed.</summary>
    public sealed class SimpleModeState
    {
        [JsonPropertyName("players")] public int Players { get; set; } = 4;
        [JsonPropertyName("type")] public int Type { get; set; } = 1;       // Free-for-all
        [JsonPropertyName("scale")] public int Scale { get; set; } = 1;     // Medium
        [JsonPropertyName("length")] public int Length { get; set; } = 1;   // Medium
        [JsonPropertyName("chaos")] public int Chaos { get; set; } = 1;     // Normal
        [JsonPropertyName("victory")] public int Victory { get; set; }      // Classic
        [JsonPropertyName("water")] public bool Water { get; set; }
        [JsonPropertyName("portals")] public bool Portals { get; set; }
        [JsonPropertyName("strongNeutrals")] public bool StrongNeutrals { get; set; }
    }
}
