using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>The outcome of reading a HotA <c>.h3t</c> file: a template plus anything worth telling the user.</summary>
    public sealed record H3TImportResult(RmgTemplate Template, IReadOnlyList<string> Warnings, int ZoneCount, int ConnectionCount);

    /// <summary>
    /// Imports the <b>structure</b> of a Heroes III (HotA) template — its zones, their roles and sizes,
    /// the player starts and the guarded connections between zones — into an Olden Era template that
    /// can then be finished in the visual editor.
    /// <para>
    /// A <c>.h3t</c> file is tab-separated, one record per line and ~140 columns wide. Zone records and
    /// connection records live in the same table and are told apart by which columns are filled.
    /// Only the columns listed in <see cref="Field"/> carry meaning for us; HotA concepts that Olden
    /// Era has no equivalent for (underground levels, per-town building sets, HotA-specific object
    /// tables) are deliberately dropped rather than guessed at.
    /// </para>
    /// <para>
    /// <b>This is a structural import, not a conversion of game balance.</b> Content pools, guard
    /// budgets and biome details are filled with the same proven defaults a hand-added zone gets, so
    /// the result is a valid, playable Olden Era template shaped like the original — the numbers are
    /// then the author's to tune. Every assumption the importer had to make is reported as a warning.
    /// </para>
    /// </summary>
    public static class H3TImporter
    {
        private static string L(string key, params object[] args) => Localization.LocalizationManager.T(key, args);
        /// <summary>Column indices of the H3T table (0-based).</summary>
        private static class Field
        {
            public const int ZoneType = 2;
            public const int Name = 7;
            public const int TemplateName = 15;
            public const int Id = 28;
            public const int HumanStart = 29;      // "x" marker
            public const int ComputerStart = 30;   // "x" marker
            public const int Treasure = 31;
            public const int BaseSize = 33;
            public const int Size = 34;
            public const int Ownership = 38;
            public const int TownFirst = 48;       // 48..59 — one "x" per H3 town type
            public const int TownLast = 59;
            public const int TerrainFirst = 75;    // 75..84 — one "x" per H3 terrain
            public const int TerrainLast = 84;
            public const int Strength = 85;
            public const int ConnectionFrom = 127; // 1-based zone ordinal
            public const int ConnectionTo = 128;
            public const int ConnectionValue = 129;
            public const int ConnectionRoad = 132; // "+" marker
            public const int ConnectionType = 133;
            public const int Width = 134;          // widest column we ever read
        }

        /// <summary>H3T zone-type code → the Olden Era zone layout that plays the same role.</summary>
        private static readonly Dictionary<int, string> LayoutByZoneType = new()
        {
            [0] = "zone_layout_player_spawn",
            [1] = "zone_layout_ai_spawn",
            [2] = "zone_layout_start_zone",
            [3] = "zone_layout_side_zone",
            [4] = "zone_layout_treasure_zone",
            [5] = "zone_layout_supertreasure_zone",
            [6] = "zone_layout_center",
            [7] = "zone_layout_sides",
            [8] = "zone_layout_leaf",
            [9] = "zone_layout_back",
            [10] = "zone_layout_second_spawn",
            [11] = "zone_layout_side_spawn_zone",
            [12] = "zone_layout_wincondition_zone",
        };

        /// <summary>H3 terrain (columns 75..84) → the closest Olden Era biome.</summary>
        private static readonly string?[] BiomeByTerrainColumn =
        [
            "Dirt",       // Dirt
            "Sand",       // Sand
            "Grass",      // Grass
            "Snow",       // Snow
            "Deathland",  // Swamp   — no swamp in Olden Era; deathland is the closest mood
            "Dirt",       // Rough
            null,         // Cave    — underground only, no surface equivalent
            "Lava",       // Lava
            "Autumn",     // Highlands
            "Deathland",  // Wasteland
        ];

        /// <summary>HotA connection-type code → our connection type.</summary>
        private static string ConnectionTypeOf(string code) => code.Trim() switch
        {
            "1" => "Portal",
            "2" => "Proximity",
            "3" => "GladiatorArena",
            _ => "Direct",
        };

        public static H3TImportResult Parse(string path)
        {
            // H3T files are written by a Win32 tool in a single-byte codepage; Latin-1 maps every byte
            // to a character, so nothing is lost even when the names are not ASCII.
            string[] lines = File.ReadAllLines(path, Encoding.Latin1);
            H3TImportResult result = ParseLines(lines);
            if (string.IsNullOrWhiteSpace(result.Template.Name))
                result.Template.Name = Path.GetFileNameWithoutExtension(path);
            return result;
        }

        /// <summary>Parses already-read lines — the unit-testable entry point.</summary>
        public static H3TImportResult ParseLines(IReadOnlyList<string> lines)
        {
            var warnings = new List<string>();
            var zones = new List<Zone>();
            var connections = new List<Connection>();
            var nameByOrdinal = new Dictionary<int, string>();
            var rawZoneSizes = new List<double>();
            string templateName = "";

            var zoneRows = new List<string[]>();
            var connectionRows = new List<string[]>();

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split('\t');

                if (templateName.Length == 0)
                {
                    string candidate = At(parts, Field.TemplateName);
                    if (candidate.Length > 0 && candidate is not ("Name" or "Map")) templateName = candidate;
                }

                if (IsConnectionRow(parts)) connectionRows.Add(parts);
                else if (IsZoneRow(parts)) zoneRows.Add(parts);
            }

            if (zoneRows.Count == 0)
                throw new InvalidDataException(L("S.H3T.NoZones"));

            // ── Zones ────────────────────────────────────────────────────────────────
            int playerIndex = 0;
            foreach (string[] parts in zoneRows)
            {
                int ordinal = ParseInt(At(parts, Field.Id), zones.Count + 1);
                int typeCode = ParseInt(At(parts, Field.ZoneType), -1);

                if (!LayoutByZoneType.TryGetValue(typeCode, out string? layout))
                {
                    layout = "zone_layout_sides";
                    warnings.Add(L("S.H3T.UnknownType", ordinal, typeCode));
                }

                string name = At(parts, Field.Name);
                if (name.Length == 0) name = $"Zone-{ordinal}";
                name = UniqueName(name, zones);

                bool humanStart = At(parts, Field.HumanStart).Equals("x", StringComparison.OrdinalIgnoreCase);
                bool aiStart = At(parts, Field.ComputerStart).Equals("x", StringComparison.OrdinalIgnoreCase);

                double rawSize = ParseDouble(At(parts, Field.Size), ParseDouble(At(parts, Field.BaseSize), 0));
                rawZoneSizes.Add(rawSize);

                Zone zone = BuildZone(name, layout, parts, warnings);

                if (humanStart || aiStart)
                {
                    if (playerIndex < KnownValues.SpawnPlayers.Length)
                    {
                        zone.MainObjects ??= [];
                        zone.MainObjects.Insert(0, new MainObject
                        {
                            Type = "Spawn",
                            Spawn = KnownValues.SpawnPlayers[playerIndex++],
                            RemoveGuardIfHasOwner = true,
                            GuardChance = 1,
                            GuardValue = 5000,
                            GuardWeeklyIncrement = 0.10,
                            BuildingsConstructionSid = "default_buildings_construction",
                            Placement = "Uniform",
                            PlacementArgs = ["true", "0.7", "0"],
                        });
                    }
                    else warnings.Add(L("S.H3T.ExcessStarts", name, KnownValues.SpawnPlayers.Length));
                }

                zones.Add(zone);
                nameByOrdinal[ordinal] = zone.Name;
            }

            NormalizeZoneSizes(zones, rawZoneSizes);

            // ── Connections ──────────────────────────────────────────────────────────
            var seenPairs = new HashSet<(string, string)>();
            foreach (string[] parts in connectionRows)
            {
                int from = ParseInt(At(parts, Field.ConnectionFrom), 0);
                int to = ParseInt(At(parts, Field.ConnectionTo), 0);
                if (!nameByOrdinal.TryGetValue(from, out string? fromName) ||
                    !nameByOrdinal.TryGetValue(to, out string? toName))
                {
                    warnings.Add(L("S.H3T.MissingZone", from, to));
                    continue;
                }

                if (string.Equals(fromName, toName, StringComparison.Ordinal)) continue; // self-loop

                // The same edge may appear on a zone row AND in a trailing block; keep it once.
                var key = string.CompareOrdinal(fromName, toName) <= 0 ? (fromName, toName) : (toName, fromName);
                if (!seenPairs.Add(key)) continue;

                connections.Add(new Connection
                {
                    Name = $"H3T-{fromName}-{toName}",
                    From = fromName,
                    To = toName,
                    ConnectionType = ConnectionTypeOf(At(parts, Field.ConnectionType)),
                    GuardZone = fromName,
                    GuardEscape = false,
                    SimTurnSquad = true,
                    GuardValue = Math.Max(0, ParseInt(At(parts, Field.ConnectionValue), 0)),
                    GuardWeeklyIncrement = 0.15,
                    Road = At(parts, Field.ConnectionRoad) == "+" ? true : null,
                });
            }

            if (connections.Count == 0)
                warnings.Add(L("S.H3T.NoConnections"));

            var template = new RmgTemplate
            {
                Name = templateName,
                GameMode = "Classic",
                Description = "Imported from a HotA .h3t template (structure only — content and balance use AuroraRMG defaults).",
                DisplayWinCondition = "win_condition_1",
                SizeX = 160,
                SizeZ = 160,
                GameRules = new GameRules
                {
                    HeroCountMin = 3,
                    HeroCountMax = 8,
                    HeroCountIncrement = 1,
                    HeroHireBan = false,
                    EncounterHoles = false,
                    WinConditions = new WinConditions
                    {
                        Classic = true,
                        Desertion = true,
                        DesertionDay = 3,
                        DesertionValue = 3000,
                        HeroLighting = true,
                        HeroLightingDay = 1,
                        LostStartCity = false,
                        LostStartHero = false,
                    },
                },
                Variants = [new Variant { Zones = zones, Connections = connections }],
                ZoneLayouts = [],
                MandatoryContent = [],
                ContentCountLimits = [],
                ContentPools = [],
                ContentLists = [],
            };

            // Every referenced layout must be defined in the file or the engine cannot shape the zone.
            TemplateGenerator.EnsureZoneLayoutsDefined(template);

            if (playerIndex == 0)
                warnings.Add(L("S.H3T.NoStarts"));

            return new H3TImportResult(template, warnings, zones.Count, connections.Count);
        }

        // ── Row classification ───────────────────────────────────────────────────────

        private static bool IsConnectionRow(string[] parts) =>
            parts.Length > Field.ConnectionTo
            && ParseInt(At(parts, Field.ConnectionFrom), 0) > 0
            && ParseInt(At(parts, Field.ConnectionTo), 0) > 0;

        private static bool IsZoneRow(string[] parts) =>
            parts.Length > Field.Id
            && ParseInt(At(parts, Field.Id), 0) > 0
            && LayoutByZoneType.ContainsKey(ParseInt(At(parts, Field.ZoneType), -1));

        // ── Zone construction ────────────────────────────────────────────────────────

        /// <summary>
        /// Builds an Olden Era zone from an H3T row. Structure comes from the file; everything the
        /// H3 format has no equivalent for uses the same defaults a zone added by hand in the editor
        /// gets, so the result is immediately valid.
        /// </summary>
        private static Zone BuildZone(string name, string layout, string[] parts, List<string> warnings)
        {
            // HotA "monster strength" 0..3 (weak → strong) scales our guard multiplier.
            int strength = ParseInt(At(parts, Field.Strength), 1);
            double guardMultiplier = Math.Round(Math.Clamp(0.7 + strength * 0.3, 0.4, 2.5), 2);

            // "Treasure" is HotA's per-zone value budget; it is on a different scale from ours, so it
            // is used as a RELATIVE hint around our own baseline rather than copied verbatim.
            int treasure = ParseInt(At(parts, Field.Treasure), 0);
            double contentScale = treasure <= 0 ? 1.0 : Math.Clamp(treasure / 20000.0, 0.5, 2.5);

            var zone = new Zone
            {
                Name = name,
                Size = 1.0, // normalised later, once every zone's raw size is known
                Layout = layout,
                GuardCutoffValue = 2000,
                GuardRandomization = 0.05,
                GuardMultiplier = guardMultiplier,
                GuardWeeklyIncrement = 0.20,
                GuardReactionDistribution = [60, 20, 10, 10, 2, 0],
                DiplomacyModifier = -0.5,
                GuardedContentPool = ["classic_template_pool_random_t2_item"],
                UnguardedContentPool = ["classic_template_pool_random_unguarded_t2_item"],
                ResourcesContentPool = ["content_pool_general_resources_start_zone_poor"],
                MandatoryContent = [],
                ContentCountLimits = [],
                GuardedContentValue = (int)(150000 * contentScale),
                GuardedContentValuePerArea = 1000,
                UnguardedContentValue = (int)(35000 * contentScale),
                UnguardedContentValuePerArea = 1000,
                ResourcesValue = (int)(3000 * contentScale),
                ResourcesValuePerArea = 100,
                MainObjects = [],
                ZoneBiome = new BiomeSelector { Type = "MatchZone", Args = [] },
                ContentBiome = new BiomeSelector { Type = "MatchZone", Args = [] },
                MetaObjectsBiome = new BiomeSelector { Type = "MatchZone", Args = [] },
                CrossroadsPosition = 0,
            };

            // A zone that allows towns in H3 gets a capturable town here.
            if (HasAnyMarker(parts, Field.TownFirst, Field.TownLast))
                zone.MainObjects.Add(new MainObject
                {
                    Type = "City",
                    GuardChance = 1.0,
                    GuardValue = 15000,
                    GuardWeeklyIncrement = 0.10,
                    BuildingsConstructionSid = "poor_buildings_construction",
                    Faction = new TypedSelector { Type = "FromList", Args = [] },
                    Placement = "Uniform",
                    PlacementArgs = ["true", "0.8", "2"],
                });

            var biomes = TerrainBiomes(parts);
            if (biomes.Count > 0)
                zone.ZoneBiome = new BiomeSelector { Type = "FromList", Args = biomes };
            else if (HasAnyMarker(parts, Field.TerrainFirst, Field.TerrainLast))
                warnings.Add(L("S.H3T.NoTerrain", name));

            return zone;
        }

        /// <summary>Maps the row's H3 terrain flags onto Olden Era biomes, keeping the file's order.</summary>
        private static List<string> TerrainBiomes(string[] parts)
        {
            var biomes = new List<string>();
            for (int column = Field.TerrainFirst; column <= Field.TerrainLast; column++)
            {
                if (!At(parts, column).Equals("x", StringComparison.OrdinalIgnoreCase)) continue;
                string? biome = BiomeByTerrainColumn[column - Field.TerrainFirst];
                if (biome is not null && !biomes.Contains(biome)) biomes.Add(biome);
            }
            return biomes;
        }

        /// <summary>
        /// Turns HotA's absolute zone sizes into our relative <c>size</c> multiplier by scaling them
        /// around the file's own average, so the proportions between zones survive the import.
        /// </summary>
        private static void NormalizeZoneSizes(List<Zone> zones, List<double> rawSizes)
        {
            var positive = rawSizes.Where(s => s > 0).ToList();
            if (positive.Count == 0) return;

            double average = positive.Average();
            if (average <= 0) return;

            for (int i = 0; i < zones.Count && i < rawSizes.Count; i++)
            {
                double raw = rawSizes[i];
                zones[i].Size = raw <= 0 ? 1.0 : Math.Round(Math.Clamp(raw / average, 0.4, 2.0), 2);
            }
        }

        // ── Small helpers ────────────────────────────────────────────────────────────

        private static string At(string[] parts, int index) =>
            index >= 0 && index < parts.Length ? parts[index].Trim().Trim('"') : "";

        private static bool HasAnyMarker(string[] parts, int first, int last)
        {
            for (int i = first; i <= last; i++)
                if (At(parts, i).Equals("x", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static int ParseInt(string text, int fallback) =>
            int.TryParse(text, System.Globalization.NumberStyles.Integer,
                         System.Globalization.CultureInfo.InvariantCulture, out int value) ? value : fallback;

        private static double ParseDouble(string text, double fallback) =>
            double.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double value) ? value : fallback;

        private static string UniqueName(string preferred, List<Zone> existing)
        {
            var taken = new HashSet<string>(existing.Select(z => z.Name), StringComparer.Ordinal);
            if (!taken.Contains(preferred)) return preferred;
            for (int n = 2; ; n++)
                if (!taken.Contains($"{preferred} {n}")) return $"{preferred} {n}";
        }
    }
}
