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
        [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; } = 2;
        [JsonPropertyName("gameInstallPath")] public string GameInstallPath { get; set; } = "";
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

        private static string Dir => AppDataPaths.Root;
        private static string FilePath => Path.Combine(Dir, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
                    loaded.Simple ??= new SimpleModeState();
                    loaded.Simple.MigrateSelections();
                    loaded.SchemaVersion = 2;
                    return loaded;
                }
            }
            catch { /* corrupt/locked → defaults */ }
            var fresh = new AppSettings();
            fresh.Simple.ProfileOptions = Models.GenerationProfileOptions.CurrentDefaults();
            fresh.Simple.MigrateSelections();
            return fresh;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(this));
            }
            catch { /* best effort */ }
        }
    }

    /// <summary>Persisted Simple Mode selections (combo indices + toggles). Seed is intentionally
    /// not stored — each launch starts on a fresh random seed.</summary>
    public sealed class SimpleModeState
    {
        [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; } = 2;
        [JsonPropertyName("typeId")] public string? TypeId { get; set; }
        [JsonPropertyName("scaleId")] public string? ScaleId { get; set; }
        [JsonPropertyName("lengthId")] public string? LengthId { get; set; }
        [JsonPropertyName("chaosId")] public string? ChaosId { get; set; }
        [JsonPropertyName("victoryId")] public string? VictoryId { get; set; }
        [JsonPropertyName("guardsId")] public string? GuardsId { get; set; }
        [JsonPropertyName("castleFactionId")] public string? CastleFactionId { get; set; }
        [JsonPropertyName("profileOptions")] public Models.GenerationProfileOptions? ProfileOptions { get; set; }
        [JsonPropertyName("players")] public int Players { get; set; } = 4;
        [JsonPropertyName("type")] public int Type { get; set; } = 1;       // Free-for-all
        [JsonPropertyName("scale")] public int Scale { get; set; } = 1;     // Medium
        [JsonPropertyName("length")] public int Length { get; set; } = 1;   // Medium
        [JsonPropertyName("chaos")] public int Chaos { get; set; } = 1;     // Normal
        [JsonPropertyName("victory")] public int Victory { get; set; }      // Classic
        [JsonPropertyName("water")] public bool Water { get; set; }
        [JsonPropertyName("portals")] public bool Portals { get; set; }
        [JsonPropertyName("strongNeutrals")] public bool StrongNeutrals { get; set; }
        [JsonPropertyName("guards")] public int Guards { get; set; } = 1;   // border-guard level: Normal
        [JsonPropertyName("castleFaction")] public int CastleFaction { get; set; } // 0 = Random neutral-castle faction

        private static readonly string[] TypeIds = ["duel","ffa","pve","teams","lanes","solo"];
        private static readonly string[] ScaleIds = ["small","medium","large","huge"];
        private static readonly string[] LengthIds = ["short","medium","long"];
        private static readonly string[] ChaosIds = ["tame","normal","wild"];
        private static readonly string[] GuardIds = ["weak","normal","strong","fortress","impassable","extreme"];
        private static readonly string[] FactionIds = ["","Human","Undead","Dungeon","Nature","Demon","Unfrozen"];

        public void MigrateSelections()
        {
            Type = Resolve(TypeId, Type, TypeIds); TypeId ??= Id(Type, TypeIds);
            Scale = Resolve(ScaleId, Scale, ScaleIds); ScaleId ??= Id(Scale, ScaleIds);
            Length = Resolve(LengthId, Length, LengthIds); LengthId ??= Id(Length, LengthIds);
            Chaos = Resolve(ChaosId, Chaos, ChaosIds); ChaosId ??= Id(Chaos, ChaosIds);
            Guards = Resolve(GuardsId, Guards, GuardIds); GuardsId ??= Id(Guards, GuardIds);
            CastleFaction = Resolve(CastleFactionId, CastleFaction, FactionIds); CastleFactionId ??= Id(CastleFaction, FactionIds);
            Victory = Resolve(VictoryId, Victory, OldenEraTemplateEditor.Models.KnownValues.VictoryConditionIds);
            VictoryId ??= Id(Victory, OldenEraTemplateEditor.Models.KnownValues.VictoryConditionIds);
            ProfileOptions ??= new Models.GenerationProfileOptions();
            SchemaVersion = 2;
        }

        public void CaptureStableIds()
        {
            TypeId = Id(Type, TypeIds) ?? TypeId; ScaleId = Id(Scale, ScaleIds) ?? ScaleId;
            LengthId = Id(Length, LengthIds) ?? LengthId; ChaosId = Id(Chaos, ChaosIds) ?? ChaosId;
            GuardsId = Id(Guards, GuardIds) ?? GuardsId; CastleFactionId = Id(CastleFaction, FactionIds) ?? CastleFactionId;
            VictoryId = Id(Victory, OldenEraTemplateEditor.Models.KnownValues.VictoryConditionIds) ?? VictoryId;
            SchemaVersion = 2;
        }
        private static int Resolve(string? id, int legacy, string[] ids) => id is null
            ? (legacy >= 0 && legacy < ids.Length ? legacy : -1) : Array.IndexOf(ids, id);
        private static string? Id(int index, string[] ids) => index >= 0 && index < ids.Length ? ids[index] : null;
    }
}
