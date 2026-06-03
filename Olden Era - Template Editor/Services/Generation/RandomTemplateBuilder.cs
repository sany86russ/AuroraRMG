using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;
using OldenEraTemplateEditor.Services.ContentManagement;

namespace Olden_Era___Template_Editor.Services.Generation
{
    /// <summary>
    /// The engine of Simple Mode / Quick Generate. Turns a handful of player-facing
    /// <see cref="QuickGenerateOptions"/> (plus a deterministic <see cref="QuickGenerateOptions.Seed"/>)
    /// into a complete <see cref="GeneratorSettings"/> by randomising every advanced knob within
    /// safe, curated ranges keyed to the player's choices — so each seed yields a genuinely
    /// different but always-playable template, not one of a fixed set of presets.
    /// <para>
    /// The mapping is fully deterministic: the same options + seed always produce the same
    /// <see cref="GeneratorSettings"/>. (Full map-level seed reproducibility additionally requires
    /// threading the seed into <see cref="TemplateGenerator"/>'s internal placement RNG — a planned
    /// follow-up; today the structural configuration is reproducible, micro-placement is not.)
    /// </para>
    /// </summary>
    public static class RandomTemplateBuilder
    {
        private static readonly TerrainTheme[] SingleBiomes =
        [
            TerrainTheme.Grass, TerrainTheme.Snow, TerrainTheme.Lava, TerrainTheme.Sand,
            TerrainTheme.Dirt, TerrainTheme.Deathland, TerrainTheme.Autumn
        ];

        /// <summary>Builds a full <see cref="GeneratorSettings"/> from the simple options.</summary>
        public static GeneratorSettings Build(QuickGenerateOptions opts)
        {
            ArgumentNullException.ThrowIfNull(opts);
            var rng = new Random(opts.Seed);

            int playerCount = NormalisePlayerCount(opts.GameType, opts.PlayerCount);
            string victory = ResolveVictory(opts.VictoryCondition);
            bool tournament = victory == "win_condition_6";
            if (tournament) playerCount = 2; // RMG tournament = two isolated 1v1 clusters

            int mapSize = PickMapSize(opts.Scale, opts.Length, playerCount, rng);
            MapTopology topology = PickTopology(opts.GameType, opts.Chaos, rng);

            var settings = new GeneratorSettings
            {
                TemplateName = ResolveName(opts),
                GameMode = "Classic",
                SingleHeroMode = false,
                PlayerCount = playerCount,
                MapSize = mapSize,
                Topology = topology,
                Seed = opts.Seed, // so TemplateGenerator reproduces the same map for this seed

                HeroSettings = BuildHeroSettings(rng),
                GenerateRoads = true,
                MatchSpawnTerrainToFaction = true,            // each player's home terrain matches their faction
                PlayerZoneMandatoryContent = DefaultPlayerMines(), // guarantee starter mines, not just resource piles
                SpawnRemoteFootholds = true,
                NoDirectPlayerConnections = opts.GameType == QuickGameType.Duel,
                RandomPortals = opts.Portals,
                MaxPortalConnections = opts.Portals ? rng.Next(8, 33) : 32,
                Terrain = PickTerrain(opts.Chaos, rng),
                MonsterAggression = PickAggression(opts, rng),
                NeutralDiplomacyModifier = PickDiplomacy(opts, rng),
                WaterLevel = opts.Water ? PickWater(opts.Chaos, rng) : WaterLevel.None,
                EncounterHoles = opts.Chaos == QuickChaos.Wild && rng.NextDouble() < 0.3,
                TerrainRoughnessPercent = PickPercent(80, 140, opts.Chaos, rng),
                LakeAmountPercent = opts.Water ? PickPercent(100, 200, opts.Chaos, rng) : PickPercent(60, 120, opts.Chaos, rng),
                FactionLawsExpPercent = 100,
                AstrologyExpPercent = 100,
                GameEndConditions = new GameEndConditions { VictoryCondition = victory },
                TournamentRules = new TournamentRules { Enabled = tournament },
            };

            ConfigureZones(settings, opts, playerCount, mapSize, victory, rng);

            // NOTE: seed landmarks LAST so these rng draws come after every structural decision — that
            // keeps a given seed's map structure byte-for-byte unchanged vs. before this feature; only
            // the guaranteed landmark content is added on top.
            SeedSignatureLandmarks(settings, rng);
            return settings;
        }

        // ── Curated landmarks (Phase 2: make a quick map feel "designed", not just budget-filled) ────

        // Signature high-end encounters. Per the real templates + ZoneContentManager tier notes, ONLY
        // high zones carry these. All are uncapped in contentCountLimits, so forcing one never conflicts
        // with a limit. Shapes mirror the game's own mandatory items (bare sid, engine-guarded internally).
        private static readonly string[] HighLandmarks =
            ["dragon_utopia", "research_laboratory", "unstable_ruins", "eternal_dragon"];

        // Mid-tier signature anchor (confirmed as mandatory content in the real corpus).
        private static readonly string[] MediumLandmarks =
            ["orb_observatory"];

        /// <summary>
        /// Adds one guaranteed signature landmark to the medium- and high-tier neutral mandatory-content
        /// lists. The generator only consumes a tier's list when a zone of that tier exists, so this is
        /// purely additive and safe on any map. Deterministic: same seed → same landmark.
        /// </summary>
        private static void SeedSignatureLandmarks(GeneratorSettings settings, Random rng)
        {
            settings.HighNeutralMandatoryContent.Add(Landmark(HighLandmarks[rng.Next(HighLandmarks.Length)]));
            settings.MediumNeutralMandatoryContent.Add(Landmark(MediumLandmarks[rng.Next(MediumLandmarks.Length)]));
        }

        /// <summary>A minimal, real-shaped mandatory landmark: bare sid, not RMG-guarded (it guards itself),
        /// no name (nothing references it) and no placement rules (placed anywhere in the zone).</summary>
        private static ContentItem Landmark(string sid) =>
            ContentItemBuilder.Create(sid).Guarded(false).Build();

        /// <summary>Standard guarded starter mines (Wood, Ore, Gold) anchored in every player's spawn zone,
        /// so a quick map has a real, ownable economy — not just one-shot resource piles that only reward
        /// the first visiting hero. Mirrors the Advanced default (`Presets.DefaultPlayerMines`) — the proven,
        /// in-range shape.</summary>
        private static List<ContentItem> DefaultPlayerMines() =>
        [
            ContentItemBuilder.Create(ContentIds.MineWood.Sid).WithName("name_mine_wood").Mine().Guarded().Build(),
            ContentItemBuilder.Create(ContentIds.MineOre.Sid).WithName("name_mine_ore").Mine().Guarded().Build(),
            ContentItemBuilder.Create(ContentIds.MineGold.Sid).WithName("name_mine_gold").Mine().Guarded().Build(),
        ];

        // ── Players / size / topology ─────────────────────────────────────────────

        private static int NormalisePlayerCount(QuickGameType type, int requested)
        {
            int pc = Math.Clamp(requested, 2, 8);
            return type switch
            {
                QuickGameType.Duel => 2,
                QuickGameType.Team => Math.Max(2, pc - (pc % 2)), // nearest even ≥ 2
                _ => pc,
            };
        }

        private static int PickMapSize(QuickMapScale scale, QuickGameLength length, int playerCount, Random rng)
        {
            int[] band = scale switch
            {
                QuickMapScale.Small => [64, 80, 96],
                QuickMapScale.Large => [176, 192, 208, 240],
                // Experimental large maps (above the official 240 cap). Biased to the 300–400
                // sweet spot players ask for; the game engine handles these even though no
                // official template ships at this size.
                QuickMapScale.Huge => [256, 288, 320, 352, 384, 400],
                _ => [112, 128, 144, 160],
            };
            // Game length biases the pick within the band: short → smaller end, long → larger end.
            int idx = length switch
            {
                QuickGameLength.Short => rng.Next(0, Math.Max(1, band.Length / 2)),
                QuickGameLength.Long => rng.Next((band.Length + 1) / 2, band.Length),
                _ => rng.Next(0, band.Length),
            };
            int size = band[Math.Clamp(idx, 0, band.Length - 1)];

            // Don't let the player zones alone overcrowd a small map (the editor warns below
            // 1024 map-area per zone). Bump to the smallest official size that fits the players
            // plus at least one neutral zone — so e.g. 8 players never land on 64×64.
            return Math.Max(size, MinSizeForZones(playerCount + 1));
        }

        /// <summary>Smallest official map size giving ≥ 1024 area per zone for <paramref name="zones"/> zones.</summary>
        private static int MinSizeForZones(int zones)
        {
            foreach (int s in KnownValues.MapSizes) // ascending
                if ((long)s * s / Math.Max(1, zones) >= 1024) return s;
            return KnownValues.MapSizes[^1];
        }

        private static MapTopology PickTopology(QuickGameType type, QuickChaos chaos, Random rng)
        {
            var pool = type switch
            {
                QuickGameType.Duel => new List<MapTopology> { MapTopology.Default, MapTopology.Chain, MapTopology.HubAndSpoke, MapTopology.Balanced },
                QuickGameType.Pve => new List<MapTopology> { MapTopology.HubAndSpoke, MapTopology.Balanced, MapTopology.Default },
                QuickGameType.Team => new List<MapTopology> { MapTopology.Balanced, MapTopology.SharedWeb, MapTopology.Default },
                _ => new List<MapTopology> { MapTopology.Default, MapTopology.Balanced, MapTopology.HubAndSpoke },
            };
            if (chaos == QuickChaos.Wild)
                pool.Add(MapTopology.Random);
            return pool[rng.Next(pool.Count)];
        }

        // ── Environment ───────────────────────────────────────────────────────────

        private static TerrainTheme PickTerrain(QuickChaos chaos, Random rng)
        {
            double r = rng.NextDouble();
            return chaos switch
            {
                QuickChaos.Tame => r < 0.8 ? TerrainTheme.FactionBased : RandomBiome(rng),
                QuickChaos.Wild => r < 0.4 ? TerrainTheme.Random : (r < 0.8 ? RandomBiome(rng) : TerrainTheme.FactionBased),
                _ => r < 0.6 ? TerrainTheme.FactionBased : (r < 0.9 ? RandomBiome(rng) : TerrainTheme.Random),
            };
        }

        private static TerrainTheme RandomBiome(Random rng) => SingleBiomes[rng.Next(SingleBiomes.Length)];

        private static MonsterAggression PickAggression(QuickGenerateOptions opts, Random rng)
        {
            if (opts.StrongNeutrals || opts.GameType == QuickGameType.Pve)
                return rng.NextDouble() < 0.75 ? MonsterAggression.Aggressive : MonsterAggression.Normal;
            return opts.Chaos switch
            {
                QuickChaos.Tame => rng.NextDouble() < 0.5 ? MonsterAggression.Passive : MonsterAggression.Normal,
                QuickChaos.Wild => (MonsterAggression)rng.Next(0, 3),
                _ => MonsterAggression.Normal,
            };
        }

        private static double PickDiplomacy(QuickGenerateOptions opts, Random rng)
        {
            // Engine default is -0.5 (lower = neutrals less willing to join the player).
            double v = opts.StrongNeutrals || opts.GameType == QuickGameType.Pve
                ? -0.5 - rng.NextDouble() * 0.25   // -0.75 .. -0.5
                : -0.5 + rng.NextDouble() * 0.25;  // -0.5 .. -0.25
            return Math.Round(v, 2);
        }

        private static WaterLevel PickWater(QuickChaos chaos, Random rng)
        {
            // Wild biases toward wider water; otherwise even across the three non-None levels.
            if (chaos == QuickChaos.Wild)
                return rng.NextDouble() < 0.5 ? WaterLevel.Large : WaterLevel.Medium;
            return (WaterLevel)rng.Next(1, 4); // Small / Medium / Large
        }

        // ── Heroes / victory ────────────────────────────────────────────────────────

        private static HeroSettings BuildHeroSettings(Random rng)
        {
            int min = rng.Next(2, 5);            // 2..4
            int max = Math.Clamp(min + rng.Next(3, 8), min, 12); // min+3 .. min+7, capped 12
            return new HeroSettings { HeroCountMin = min, HeroCountMax = max, HeroCountIncrement = 1, HeroHireBan = false };
        }

        /// <summary>Validates the requested win condition against the known set; falls back to classic.</summary>
        private static string ResolveVictory(string? id) =>
            Array.IndexOf(KnownValues.VictoryConditionIds, id) >= 0 ? id! : "win_condition_1";

        // ── Zones / neutrals / density ──────────────────────────────────────────────

        private static void ConfigureZones(GeneratorSettings settings, QuickGenerateOptions opts, int playerCount, int mapSize, string victory, Random rng)
        {
            bool dense = opts.StrongNeutrals || opts.GameType == QuickGameType.Pve;

            var cfg = settings.ZoneCfg;
            cfg.PlayerZoneCastles = 1;
            cfg.NeutralZoneCastles = rng.Next(1, 3); // 1..2
            cfg.ResourceDensityPercent = PickPercent(LengthBase(opts.Length, 80, 100, 120), LengthBase(opts.Length, 110, 130, 170), opts.Chaos, rng);
            cfg.StructureDensityPercent = PickPercent(LengthBase(opts.Length, 80, 95, 110), LengthBase(opts.Length, 110, 125, 160), opts.Chaos, rng);
            cfg.NeutralStackStrengthPercent = dense ? PickPercent(130, 220, opts.Chaos, rng) : PickPercent(80, 130, opts.Chaos, rng);
            cfg.BorderGuardStrengthPercent = PickPercent(80, 140, opts.Chaos, rng);

            // How many neutral zones can the map hold? Two hard limits the generator/UI respect:
            //   - at most (32 - players) named zones are available;
            //   - each zone needs ≥ 1024 map area, else the UI flags the template as overcrowded.
            int areaCap = (mapSize * mapSize) / 1024 - playerCount;
            int maxNeutrals = Math.Max(0, Math.Min(32 - playerCount, areaCap));

            double lengthFactor = opts.Length switch
            {
                QuickGameLength.Short => 0.8,
                QuickGameLength.Long => 1.8,
                _ => 1.3,
            };
            int target = (int)Math.Round(playerCount * lengthFactor);
            if (opts.GameType == QuickGameType.Duel) target += 1; // duels still want a contested middle

            // Some win conditions need a minimum number of neutral zones:
            //   City Hold (_5)   → a neutral hold city;   Tournament (_6) → neutrals to split per cluster.
            int floor = opts.GameType == QuickGameType.Duel ? 1 : 0;
            if (victory == "win_condition_5") floor = Math.Max(floor, 1);
            if (victory == "win_condition_6") floor = Math.Max(floor, 2);

            target += rng.Next(-1, 2); // ±1 jitter
            int neutrals = Math.Clamp(target, Math.Min(floor, maxNeutrals), maxNeutrals);

            DistributeNeutrals(cfg.Advanced, neutrals, dense, opts.Length, rng);

            // City Hold needs the hold city to be an actual castle zone — guarantee one.
            if (victory == "win_condition_5" && neutrals > 0)
            {
                var adv = cfg.Advanced;
                int castles = adv.NeutralLowCastleCount + adv.NeutralMediumCastleCount + adv.NeutralHighCastleCount;
                if (castles == 0)
                {
                    if (adv.NeutralMediumNoCastleCount > 0) { adv.NeutralMediumNoCastleCount--; adv.NeutralMediumCastleCount++; }
                    else if (adv.NeutralHighNoCastleCount > 0) { adv.NeutralHighNoCastleCount--; adv.NeutralHighCastleCount++; }
                    else if (adv.NeutralLowNoCastleCount > 0) { adv.NeutralLowNoCastleCount--; adv.NeutralLowCastleCount++; }
                }
            }

            cfg.Advanced.Enabled = neutrals > 0;
            cfg.NeutralZoneCount = neutrals; // kept in sync for the simple-mode summary / UI
        }

        /// <summary>Splits <paramref name="total"/> neutral zones across the six low/medium/high × castle tiers.</summary>
        private static void DistributeNeutrals(AdvancedSettings adv, int total, bool dense, QuickGameLength length, Random rng)
        {
            adv.NeutralLowNoCastleCount = adv.NeutralLowCastleCount = 0;
            adv.NeutralMediumNoCastleCount = adv.NeutralMediumCastleCount = 0;
            adv.NeutralHighNoCastleCount = adv.NeutralHighCastleCount = 0;
            if (total <= 0) return;

            // Quality weights: normal maps lean low/medium; dense (strong-neutral / PvE) maps lean medium/high.
            (double low, double med, double high) w = dense ? (0.2, 0.4, 0.4) : (0.4, 0.4, 0.2);
            int lowN = (int)Math.Round(total * w.low);
            int medN = (int)Math.Round(total * w.med);
            int highN = total - lowN - medN; // remainder keeps the sum exact
            if (highN < 0) { medN += highN; highN = 0; }

            // Longer games + dense maps place more castle zones; weight ~0.3 (0.45 for long).
            double castleFraction = length == QuickGameLength.Long ? 0.45 : 0.3;
            int CastleSplit(int n) => Math.Clamp((int)Math.Round(n * castleFraction), 0, n);

            int lowC = CastleSplit(lowN), medC = CastleSplit(medN), highC = CastleSplit(highN);
            adv.NeutralLowNoCastleCount = lowN - lowC; adv.NeutralLowCastleCount = lowC;
            adv.NeutralMediumNoCastleCount = medN - medC; adv.NeutralMediumCastleCount = medC;
            adv.NeutralHighNoCastleCount = highN - highC; adv.NeutralHighCastleCount = highC;
            _ = rng; // reserved for future jittered splits; keeps the signature seed-aware
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────

        /// <summary>Picks a percentage in [min,max]; <see cref="QuickChaos.Wild"/> widens the band by 20%.</summary>
        private static int PickPercent(int min, int max, QuickChaos chaos, Random rng)
        {
            if (chaos == QuickChaos.Wild)
            {
                int pad = (max - min) / 5;
                min = Math.Max(20, min - pad);
                max += pad;
            }
            else if (chaos == QuickChaos.Tame)
            {
                int squeeze = (max - min) / 4;
                min += squeeze;
                max -= squeeze;
            }
            if (max <= min) return min;
            return rng.Next(min, max + 1);
        }

        private static int LengthBase(QuickGameLength length, int shortV, int medV, int longV) =>
            length switch { QuickGameLength.Short => shortV, QuickGameLength.Long => longV, _ => medV };

        private static string ResolveName(QuickGenerateOptions opts)
        {
            if (!string.IsNullOrWhiteSpace(opts.TemplateName))
                return opts.TemplateName!.Trim();
            string type = opts.GameType switch
            {
                QuickGameType.Duel => "Duel",
                QuickGameType.Pve => "PvE",
                QuickGameType.Team => "Team",
                _ => "FFA",
            };
            // Latin name keyed to the seed so it's recognisable and shareable.
            return $"Quick {type} {opts.Seed.ToString("X8", CultureInfo.InvariantCulture)}";
        }
    }
}
