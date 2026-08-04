using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

/// <summary>
/// Structural integrity sweep over the FULL Advanced settings space (Simple Mode has its own
/// sweeps in <see cref="TemplateGeneratorTests"/>). Every generated template must satisfy the
/// invariants the game engine relies on: no dangling references, no negative/NaN numbers,
/// a connected graph, and a contiguous Player1..PlayerN spawn chain.
/// </summary>
public class GeneratorIntegrityTests
{
    /// <summary>A deterministic spread of Advanced-mode settings: topologies × players × zone mixes × flags.</summary>
    private static IEnumerable<GeneratorSettings> AdvancedMatrix()
    {
        int[] playerCounts = [1, 2, 3, 5, 8];
        (int low, int lowC, int med, int medC, int high, int highC)[] neutralMixes =
        [
            (0, 0, 0, 0, 0, 0),   // no neutrals at all
            (1, 0, 0, 0, 0, 0),   // a single low neutral
            (2, 1, 2, 1, 1, 1),   // full tier mix
            (0, 0, 0, 3, 0, 0),   // castle-only neutrals
            (4, 0, 4, 0, 4, 0),   // many castle-less neutrals
        ];

        int seed = 0;
        foreach (MapTopology topology in Enum.GetValues<MapTopology>())
        foreach (int players in playerCounts)
        foreach (var mix in neutralMixes)
        {
            seed++;
            var pick = new Random(seed);
            yield return new GeneratorSettings
            {
                TemplateName = $"Sweep {topology} p{players} #{seed}",
                Seed = seed,
                PlayerCount = players,
                MapSize = 160 + pick.Next(0, 5) * 32,
                Topology = topology,
                NoDirectPlayerConnections = pick.Next(2) == 0,
                RandomPortals = pick.Next(3) == 0,
                GenerateRoads = pick.Next(4) != 0,
                SpawnRemoteFootholds = pick.Next(3) != 0,
                PlayerStartsWithCastles = pick.Next(2) == 0,
                MatchPlayerCastleFactions = pick.Next(2) == 0,
                MatchSpawnTerrainToFaction = pick.Next(2) == 0,
                MinNeutralZonesBetweenPlayers = pick.Next(0, 3),
                EncounterHoles = pick.Next(2) == 0,
                WaterLevel = (WaterLevel)pick.Next(0, Enum.GetValues<WaterLevel>().Length),
                Terrain = (TerrainTheme)pick.Next(0, Enum.GetValues<TerrainTheme>().Length),
                MonsterAggression = (MonsterAggression)pick.Next(0, Enum.GetValues<MonsterAggression>().Length),
                SingleHeroMode = pick.Next(5) == 0,
                HeroSettings = new HeroSettings
                {
                    HeroCountMin = pick.Next(1, 9),
                    HeroCountMax = pick.Next(9, 13),
                    HeroCountIncrement = pick.Next(0, 6),
                },
                GameEndConditions = new GameEndConditions
                {
                    VictoryCondition = KnownValues.VictoryConditionIds[pick.Next(KnownValues.VictoryConditionIds.Length)],
                },
                ZoneCfg = new ZoneConfiguration
                {
                    PlayerZoneCastles = pick.Next(0, 4),
                    NeutralZoneCastles = pick.Next(0, 4),
                    HubZoneCastles = pick.Next(0, 3),
                    ResourceDensityPercent = pick.Next(20, 401),
                    StructureDensityPercent = pick.Next(20, 201),
                    NeutralStackStrengthPercent = pick.Next(25, 301),
                    BorderGuardStrengthPercent = pick.Next(25, 501),
                    Advanced = new AdvancedSettings
                    {
                        Enabled = true,
                        NeutralLowNoCastleCount = mix.low,
                        NeutralLowCastleCount = mix.lowC,
                        NeutralMediumNoCastleCount = mix.med,
                        NeutralMediumCastleCount = mix.medC,
                        NeutralHighNoCastleCount = mix.high,
                        NeutralHighCastleCount = mix.highC,
                        PlayerZoneSize = 0.5 + pick.NextDouble(),
                        NeutralZoneSize = 0.5 + pick.NextDouble(),
                        GuardRandomization = pick.NextDouble() * 0.5,
                    },
                },
            };
        }
    }

    [Fact]
    public void Generate_AdvancedSweep_ProducesSelfConsistentTemplates()
    {
        var failures = new List<string>();

        foreach (GeneratorSettings settings in AdvancedMatrix())
        {
            RmgTemplate tpl = TemplateGenerator.Generate(settings);
            var adv = settings.ZoneCfg.Advanced;
            int neutralCount = adv.NeutralLowNoCastleCount + adv.NeutralLowCastleCount
                             + adv.NeutralMediumNoCastleCount + adv.NeutralMediumCastleCount
                             + adv.NeutralHighNoCastleCount + adv.NeutralHighCastleCount;
            string id = $"{settings.TemplateName} [n={neutralCount} iso={settings.NoDirectPlayerConnections} "
                      + $"portals={settings.RandomPortals} win={settings.GameEndConditions.VictoryCondition}]";
            void Fail(string message) => failures.Add($"{id}: {message}");

            Variant variant = Assert.Single(tpl.Variants ?? []);
            List<Zone> zones = variant.Zones ?? [];
            List<Connection> conns = variant.Connections ?? [];
            Assert.NotEmpty(zones);

            var zoneNames = new HashSet<string>(zones.Select(z => z.Name), StringComparer.Ordinal);
            if (zoneNames.Count != zones.Count) Fail("duplicate zone names");
            if (zones.Any(z => string.IsNullOrWhiteSpace(z.Name))) Fail("blank zone name");

            // ── Referenced definition blocks must exist in the file ──────────────
            var layoutNames = new HashSet<string>((tpl.ZoneLayouts ?? []).Select(l => l.Name ?? ""), StringComparer.Ordinal);
            var mcNames = new HashSet<string>((tpl.MandatoryContent ?? []).Select(g => g.Name ?? ""), StringComparer.Ordinal);
            var clNames = new HashSet<string>((tpl.ContentCountLimits ?? []).Select(g => g.Name ?? ""), StringComparer.Ordinal);

            foreach (Zone z in zones)
            {
                if (string.IsNullOrWhiteSpace(z.Layout)) Fail($"zone {z.Name} has no layout");
                else if (!layoutNames.Contains(z.Layout!)) Fail($"zone {z.Name} references undefined layout '{z.Layout}'");

                foreach (string n in z.MandatoryContent ?? [])
                    if (!mcNames.Contains(n)) Fail($"zone {z.Name} references undefined mandatoryContent '{n}'");
                foreach (string n in z.ContentCountLimits ?? [])
                    if (!clNames.Contains(n)) Fail($"zone {z.Name} references undefined contentCountLimits '{n}'");

                // Content pools must be names the game actually ships.
                foreach (string p in z.GuardedContentPool ?? [])
                    if (!KnownValues.GuardedContentPoolSids.Contains(p)) Fail($"zone {z.Name} unknown guarded pool '{p}'");
                foreach (string p in z.UnguardedContentPool ?? [])
                    if (!KnownValues.UnguardedContentPoolSids.Contains(p)) Fail($"zone {z.Name} unknown unguarded pool '{p}'");
                foreach (string p in z.ResourcesContentPool ?? [])
                    if (!KnownValues.ResourcesContentPoolSids.Contains(p)) Fail($"zone {z.Name} unknown resources pool '{p}'");

                // Numbers the engine reads must stay sane.
                if (z.Size is { } size && (double.IsNaN(size) || size <= 0)) Fail($"zone {z.Name} size {size}");
                foreach ((string field, int? value) in new (string, int?)[]
                         {
                             ("guardedContentValue", z.GuardedContentValue),
                             ("guardedContentValuePerArea", z.GuardedContentValuePerArea),
                             ("unguardedContentValue", z.UnguardedContentValue),
                             ("unguardedContentValuePerArea", z.UnguardedContentValuePerArea),
                             ("resourcesValue", z.ResourcesValue),
                             ("resourcesValuePerArea", z.ResourcesValuePerArea),
                             ("guardCutoffValue", z.GuardCutoffValue),
                         })
                    if (value is < 0) Fail($"zone {z.Name} negative {field} = {value}");

                // A zone biome that copies "itself" is circular and never resolves.
                if (string.Equals(z.ZoneBiome?.Type, "MatchZone", StringComparison.Ordinal)
                    && (z.ZoneBiome!.Args?.FirstOrDefault() == z.Name))
                    Fail($"zone {z.Name} zoneBiome MatchZone points at itself");

                foreach (BiomeSelector? sel in new[] { z.ZoneBiome, z.ContentBiome, z.MetaObjectsBiome })
                {
                    if (sel is null) continue;
                    if (!string.Equals(sel.Type, "MatchZone", StringComparison.Ordinal)) continue;
                    string? target = sel.Args?.FirstOrDefault();
                    if (target is not null && !zoneNames.Contains(target))
                        Fail($"zone {z.Name} biome MatchZone references missing zone '{target}'");
                }

                // Roads must point at real connections / main-object indices.
                int moCount = z.MainObjects?.Count ?? 0;
                foreach (Road r in z.Roads ?? [])
                foreach (RoadEndpoint? ep in new[] { r.From, r.To })
                {
                    if (ep is null) continue;
                    string? arg = ep.Args?.FirstOrDefault();
                    if (string.Equals(ep.Type, "MainObject", StringComparison.Ordinal)
                        && int.TryParse(arg, out int idx) && (idx < 0 || idx >= moCount))
                        Fail($"zone {z.Name} road endpoint MainObject[{idx}] out of range (count {moCount})");
                    if (string.Equals(ep.Type, "Connection", StringComparison.Ordinal)
                        && arg is not null && !conns.Any(c => string.Equals(c.Name, arg, StringComparison.Ordinal)))
                        Fail($"zone {z.Name} road endpoint references missing connection '{arg}'");
                }
            }

            // ── Connections ──────────────────────────────────────────────────────
            var connNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (Connection c in conns)
            {
                if (string.IsNullOrWhiteSpace(c.Name)) Fail($"connection {c.From}->{c.To} has no name");
                else if (!connNames.Add(c.Name!)) Fail($"duplicate connection name '{c.Name}'");
                if (!zoneNames.Contains(c.From)) Fail($"connection '{c.Name}' from missing zone '{c.From}'");
                if (!zoneNames.Contains(c.To)) Fail($"connection '{c.Name}' to missing zone '{c.To}'");
                if (string.Equals(c.From, c.To, StringComparison.Ordinal)) Fail($"connection '{c.Name}' is a self-loop");
                if (c.GuardValue is < 0) Fail($"connection '{c.Name}' negative guardValue");
                if (c.GuardZone is { } gz && !zoneNames.Contains(gz)) Fail($"connection '{c.Name}' guardZone missing '{gz}'");
            }

            // ── The whole map must be reachable from the first spawn ─────────────
            // Tournament (2 players + win_condition_6) is the one deliberate exception: it builds two
            // fully isolated clusters, so a disconnected graph is the feature there.
            bool tournamentLayout = settings.PlayerCount == 2
                                 && settings.GameEndConditions.VictoryCondition == "win_condition_6";
            if (zones.Count > 1 && !tournamentLayout)
            {
                var adjacency = zones.ToDictionary(z => z.Name, _ => new List<string>(), StringComparer.Ordinal);
                foreach (Connection c in conns)
                {
                    if (!adjacency.TryGetValue(c.From, out var a) || !adjacency.TryGetValue(c.To, out var b)) continue;
                    a.Add(c.To);
                    b.Add(c.From);
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                var queue = new Queue<string>();
                string start = zones[0].Name;
                queue.Enqueue(start);
                seen.Add(start);
                while (queue.Count > 0)
                    foreach (string nb in adjacency[queue.Dequeue()])
                        if (seen.Add(nb)) queue.Enqueue(nb);

                if (seen.Count != zones.Count)
                    Fail($"graph is disconnected — {zones.Count - seen.Count} of {zones.Count} zones unreachable "
                       + $"({string.Join(", ", zoneNames.Except(seen).Take(4))})");
            }

            // ── Spawns: one per player, distinct, contiguous Player1..PlayerN ────
            var spawns = zones.SelectMany(z => z.MainObjects ?? [])
                              .Where(o => o.Type == "Spawn" && o.Spawn is not null)
                              .Select(o => o.Spawn!)
                              .ToList();
            if (spawns.Count != settings.PlayerCount) Fail($"{spawns.Count} spawns for {settings.PlayerCount} players");
            if (spawns.Distinct().Count() != spawns.Count) Fail("duplicate player spawns");
            for (int i = 0; i < settings.PlayerCount && i < KnownValues.SpawnPlayers.Length; i++)
                if (!spawns.Contains(KnownValues.SpawnPlayers[i]))
                    Fail($"spawn chain gap — {KnownValues.SpawnPlayers[i]} missing");

            // ── Game rules ───────────────────────────────────────────────────────
            GameRules rules = tpl.GameRules!;
            if (rules.HeroCountMin is < 0) Fail($"negative heroCountMin {rules.HeroCountMin}");
            if (rules.HeroCountMin > rules.HeroCountMax) Fail($"heroCountMin {rules.HeroCountMin} > heroCountMax {rules.HeroCountMax}");
            if (rules.HeroCountIncrement is < 0) Fail($"negative heroCountIncrement {rules.HeroCountIncrement}");
            if (!KnownValues.VictoryConditionIds.Contains(tpl.DisplayWinCondition ?? "")) Fail($"unknown displayWinCondition '{tpl.DisplayWinCondition}'");

            // ── Variant orientation must be a finite number ──────────────────────
            double step = variant.Orientation?.RandomAngleStep ?? 0;
            if (double.IsNaN(step) || double.IsInfinity(step)) Fail($"randomAngleStep is {step}");
        }

        Assert.True(failures.Count == 0,
            $"{failures.Count} integrity failure(s):\n" + string.Join("\n", failures.Take(40)));
    }

    [Fact]
    public void Generate_LargestSupportedMap_StaysCheap()
    {
        // Guards the interactive feel of the Generate button and the Simple-mode 🎲 reroll: the worst
        // case the UI allows is 8 players + 40 neutrals on a 512² map. The budget is deliberately loose
        // (a cold JIT on a CI runner is slower than a warm desktop) — it only catches a real regression,
        // e.g. an accidental O(n³) pass over the zone graph.
        var settings = new GeneratorSettings
        {
            Seed = 42,
            PlayerCount = 8,
            MapSize = 512,
            Topology = MapTopology.Balanced,
            RandomPortals = true,
            ZoneCfg = new ZoneConfiguration
            {
                Advanced = new AdvancedSettings
                {
                    Enabled = true,
                    NeutralLowNoCastleCount = 8, NeutralLowCastleCount = 6,
                    NeutralMediumNoCastleCount = 8, NeutralMediumCastleCount = 6,
                    NeutralHighNoCastleCount = 6, NeutralHighCastleCount = 6,
                },
            },
        };

        TemplateGenerator.Generate(settings); // warm up the JIT before timing

        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 20; i++) TemplateGenerator.Generate(settings);
        watch.Stop();

        Assert.True(watch.ElapsedMilliseconds < 4000,
            $"20 worst-case generations took {watch.ElapsedMilliseconds} ms (budget 4000 ms)");
    }

    [Theory]
    [InlineData(1, 1, 0)]   // increment above the starting limit used to emit heroCountMin = -4
    [InlineData(1, 5, 0)]
    [InlineData(4, 1, 3)]   // the common default: start with 4 heroes on one town → min 3 + increment 1
    [InlineData(8, 3, 5)]
    public void GameRules_HeroCountMin_IsNeverNegative(int uiMin, int increment, int expected)
    {
        var settings = new GeneratorSettings
        {
            Seed = 7,
            HeroSettings = new HeroSettings { HeroCountMin = uiMin, HeroCountMax = 12, HeroCountIncrement = increment },
        };

        GameRules rules = TemplateGenerator.Generate(settings).GameRules!;

        Assert.Equal(expected, rules.HeroCountMin);
        Assert.True(rules.HeroCountMin >= 0);
        Assert.True(rules.HeroCountMin <= rules.HeroCountMax);
    }

    [Theory]
    [InlineData(MapTopology.Default)]
    [InlineData(MapTopology.Lanes)]
    [InlineData(MapTopology.Balanced)]
    public void Generate_SingleZoneMap_HasNoDanglingRoadOrSelfLoop(MapTopology topology)
    {
        // A solo map with no neutral zones is a one-zone "ring": it must have zero connections and no
        // road pointing at the never-created "Ring-A-A" edge.
        var settings = new GeneratorSettings { Seed = 11, PlayerCount = 1, Topology = topology };

        Variant variant = Assert.Single(TemplateGenerator.Generate(settings).Variants ?? []);
        Zone zone = Assert.Single(variant.Zones ?? []);

        Assert.Empty(variant.Connections ?? []);
        Assert.DoesNotContain(zone.Roads ?? [], r => r.From?.Type == "Connection" || r.To?.Type == "Connection");
    }

    [Fact]
    public void Generate_TwoZoneRing_EmitsASingleEdge()
    {
        // Two zones close the ring on the same pair twice; only one corridor must be emitted,
        // otherwise the only border in the map is guarded twice over.
        var settings = new GeneratorSettings { Seed = 3, PlayerCount = 2, Topology = MapTopology.Default };

        Variant variant = Assert.Single(TemplateGenerator.Generate(settings).Variants ?? []);

        Assert.Equal(2, (variant.Zones ?? []).Count);
        Assert.Single(variant.Connections ?? []);
    }

    [Fact]
    public void Generate_IsolatedStartsWithTooFewNeutrals_StillProducesAReachableMap()
    {
        // "Isolate player starts" strips every player↔player edge; with fewer neutrals than gaps the
        // chain used to fall apart into unreachable islands. The connectivity repair must stitch it back.
        var settings = new GeneratorSettings
        {
            Seed = 5,
            PlayerCount = 5,
            Topology = MapTopology.Chain,
            NoDirectPlayerConnections = true,
            ZoneCfg = new ZoneConfiguration
            {
                Advanced = new AdvancedSettings { Enabled = true, NeutralMediumNoCastleCount = 1 },
            },
        };

        Variant variant = Assert.Single(TemplateGenerator.Generate(settings).Variants ?? []);
        List<Zone> zones = variant.Zones ?? [];

        var adjacency = zones.ToDictionary(z => z.Name, _ => new List<string>(), StringComparer.Ordinal);
        foreach (Connection c in variant.Connections ?? [])
        {
            adjacency[c.From].Add(c.To);
            adjacency[c.To].Add(c.From);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal) { zones[0].Name };
        var queue = new Queue<string>([zones[0].Name]);
        while (queue.Count > 0)
            foreach (string nb in adjacency[queue.Dequeue()])
                if (seen.Add(nb)) queue.Enqueue(nb);

        Assert.Equal(zones.Count, seen.Count);
    }
}
