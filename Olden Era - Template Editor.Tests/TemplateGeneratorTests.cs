using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Generation;
using OldenEraTemplateEditor.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Olden_Era___Template_Editor.Tests;

public class TemplateGeneratorTests
{
    [Fact]
    public void Generate_UsesRequestedSettingsForTemplateAndGameRules()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "Baseline Test Template",
            GameMode = "Classic",
            MapSize = 200,
            GameEndConditions = new GameEndConditions
            {
                VictoryCondition = "win_condition_5"
            },
            HeroSettings = new HeroSettings
            {
                HeroCountMin = 7,
                HeroCountMax = 15,
                HeroCountIncrement = 3
            },
            Topology = MapTopology.Default
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.Equal(settings.TemplateName, template.Name);
        Assert.Equal(settings.GameMode, template.GameMode);
        Assert.Equal(settings.MapSize, template.SizeX);
        Assert.Equal(settings.MapSize, template.SizeZ);
        Assert.Equal(settings.GameEndConditions.VictoryCondition, template.DisplayWinCondition);
        Assert.Matches(@"^Generated with Olden Era Template Generator v\d+\.\d+: Ring layout, no neutral zones, 1 castle per player zone\.$", template.Description);
        Assert.NotNull(template.GameRules);
        Assert.Equal(settings.HeroSettings.HeroCountMin - settings.HeroSettings.HeroCountIncrement, template.GameRules.HeroCountMin);
        Assert.Equal(settings.HeroSettings.HeroCountMax, template.GameRules.HeroCountMax);
        Assert.Equal(settings.HeroSettings.HeroCountIncrement, template.GameRules.HeroCountIncrement);
        Assert.NotEmpty(template.ZoneLayouts ?? []);
        Assert.NotEmpty(template.ContentCountLimits ?? []);
    }

    [Fact]
    public void Generate_WritesBasicTemplateDescription()
    {
        var settings = new GeneratorSettings
        {
            GameMode = "SingleHero",
            PlayerCount = 4,
            ZoneCfg = new ZoneConfiguration
            {
                Advanced = new AdvancedSettings()
                {
                    NeutralMediumCastleCount = 2
                },
                PlayerZoneCastles = 2,
                NeutralZoneCastles = 0
            },
            MapSize = 192,
            Topology = MapTopology.Chain,
            NoDirectPlayerConnections = true,
            RandomPortals = true,
            SpawnRemoteFootholds = false,
            GenerateRoads = false
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.Matches(@"^Generated with Olden Era Template Generator v\d+\.\d+: Chain layout, 2 neutral zones, 2 castles per player zone, no castles per neutral zone, options: isolated player starts, random portals, no remote footholds, roads disabled\.$", template.Description);
    }

    [Fact]
    public void Generate_DefaultTopologyCreatesExpectedZonesAndRingConnections()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 3,
            ZoneCfg = new ZoneConfiguration
            {
                Advanced = new AdvancedSettings()
                {
                    NeutralMediumCastleCount = 2
                },
                PlayerZoneCastles = 2,
                NeutralZoneCastles = 1
            },
            Topology = MapTopology.Default,
            RandomPortals = false
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zones = RequiredZones(variant);
        var connections = RequiredConnections(variant);

        Assert.Equal(5, zones.Count);
        Assert.Equal(3, zones.Count(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal)));
        Assert.Equal(2, zones.Count(zone => zone.Name.StartsWith("Neutral-", StringComparison.Ordinal)));
        Assert.Equal(5, connections.Count);
        Assert.All(connections, connection =>
        {
            Assert.StartsWith("Ring-", connection.Name);
            Assert.Equal("Direct", connection.ConnectionType);
        });
        Assert.All(zones.Where(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal)),
            zone => Assert.Equal(2, zone.MainObjects?.Count));
        Assert.All(zones.Where(zone => zone.Name.StartsWith("Neutral-", StringComparison.Ordinal)),
            zone => Assert.Single(zone.MainObjects ?? []));
    }

    [Fact]
    public void Generate_SharedWebReferencesSpokeConnectionsFromBothEndpointZones()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 4,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 3,
                NeutralZoneCastles = 1
            },
            Topology = MapTopology.SharedWeb,
            RandomPortals = false
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zones = RequiredZones(variant).ToDictionary(zone => zone.Name, StringComparer.Ordinal);
        var webConnections = RequiredConnections(variant)
            .Where(connection => connection.Name?.StartsWith("Web-", StringComparison.Ordinal) == true)
            .ToList();

        Assert.NotEmpty(webConnections);
        Assert.All(webConnections, connection =>
        {
            Assert.False(string.IsNullOrWhiteSpace(connection.Name));
            string name = connection.Name!;
            Assert.Contains(name, RoadConnectionNames(zones[connection.From]));
            Assert.Contains(name, RoadConnectionNames(zones[connection.To]));
        });
    }

    [Fact]
    public void Generate_SharedWebCastlelessNeutralZonesUseConnectionRoads()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 0,
                NeutralZoneCastles = 0
            },
            Topology = MapTopology.SharedWeb,
            RandomPortals = false
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        Zone neutralZone = Assert.Single(RequiredZones(variant),
            zone => zone.Name.StartsWith("Neutral-", StringComparison.Ordinal));
        var webConnectionNames = RequiredConnections(variant)
            .Where(connection => connection.Name?.StartsWith("Web-", StringComparison.Ordinal) == true)
            .Select(connection => connection.Name!)
            .ToList();

        Assert.Empty(neutralZone.MainObjects ?? []);
        Assert.NotEmpty(neutralZone.Roads ?? []);
        Assert.All(neutralZone.Roads ?? [], road =>
        {
            Assert.Equal("Connection", road.From?.Type);
            Assert.Equal("Connection", road.To?.Type);
        });
        Assert.Equal(2, webConnectionNames.Count);
        Assert.All(webConnectionNames, name => Assert.Contains(name, RoadConnectionNames(neutralZone)));
    }

    [Fact]
    public void Generate_LanesTopology_BuildsParallelCorridorsMeetingAtSharedArena()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 4,
            ZoneCfg = new ZoneConfiguration
            {
                Advanced = new AdvancedSettings()
                {
                    NeutralLowNoCastleCount = 4,
                    NeutralMediumNoCastleCount = 4,
                    NeutralHighCastleCount = 1, // highest quality + castle → becomes the shared arena
                },
                PlayerZoneCastles = 1,
                NeutralZoneCastles = 1,
            },
            MapSize = 240,
            Topology = MapTopology.Lanes,
            RandomPortals = false,
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zones = RequiredZones(variant);
        var connections = RequiredConnections(variant);

        // Four players + nine neutral zones (8 lane zones + 1 arena).
        Assert.Equal(4, zones.Count(z => z.Name.StartsWith("Spawn-", StringComparison.Ordinal)));
        Assert.Equal(9, zones.Count(z => z.Name.StartsWith("Neutral-", StringComparison.Ordinal)));

        // Players never border each other directly: no connection links two Spawn zones, and every
        // lane passage is Direct.
        Assert.DoesNotContain(connections, c =>
            c.From.StartsWith("Spawn-", StringComparison.Ordinal) &&
            c.To.StartsWith("Spawn-", StringComparison.Ordinal));
        Assert.All(connections, c => Assert.Equal("Direct", c.ConnectionType));

        // The lanes converge on exactly ONE shared arena: removing it leaves no two players in the
        // same connected component (every player-to-player route runs through the arena).
        var adjacency = BuildAdjacency(connections);
        var spawns = zones.Where(z => z.Name.StartsWith("Spawn-", StringComparison.Ordinal))
            .Select(z => z.Name).ToList();
        var arenaCandidates = zones
            .Where(z => z.Name.StartsWith("Neutral-", StringComparison.Ordinal))
            .Select(z => z.Name)
            .Where(arena => PlayersPairwiseSeparatedWithout(adjacency, spawns, arena))
            .ToList();
        Assert.Single(arenaCandidates);

        // Border guards rise with the entered zone's quality (the "Highway" pattern): the toughest
        // lane guard (entering the high-tier arena) outweighs the softest (entering a low-tier zone).
        var laneGuards = connections
            .Where(c => c.Name?.StartsWith("Lane", StringComparison.Ordinal) == true)
            .Select(c => c.GuardValue).ToList();
        Assert.NotEmpty(laneGuards);
        Assert.True(laneGuards.Max() > laneGuards.Min());

        static Dictionary<string, HashSet<string>> BuildAdjacency(IEnumerable<Connection> conns)
        {
            var adj = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            HashSet<string> Bucket(string k)
            {
                if (!adj.TryGetValue(k, out var s)) adj[k] = s = new HashSet<string>(StringComparer.Ordinal);
                return s;
            }
            foreach (var c in conns)
            {
                Bucket(c.From).Add(c.To);
                Bucket(c.To).Add(c.From);
            }
            return adj;
        }

        static bool PlayersPairwiseSeparatedWithout(
            Dictionary<string, HashSet<string>> adj, List<string> spawns, string removed)
        {
            foreach (var start in spawns)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal) { start };
                var queue = new Queue<string>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    if (!adj.TryGetValue(cur, out var nbrs)) continue;
                    foreach (var nb in nbrs)
                        if (nb != removed && seen.Add(nb)) queue.Enqueue(nb);
                }
                if (spawns.Any(s => s != start && seen.Contains(s))) return false;
            }
            return true;
        }
    }

    [Fact]
    public void QuickGenerate_LanesGameType_UsesLanesTopologyAndIsDeterministic()
    {
        var opts = new QuickGenerateOptions
        {
            Seed = 0x1A2E5,
            PlayerCount = 4,
            GameType = QuickGameType.Lanes,
            Scale = QuickMapScale.Medium,
            Length = QuickGameLength.Medium,
            Chaos = QuickChaos.Normal,
        };

        GeneratorSettings a = RandomTemplateBuilder.Build(opts);
        GeneratorSettings b = RandomTemplateBuilder.Build(opts);

        // The Lanes game type maps straight to the Lanes topology (not a random pool pick).
        Assert.Equal(MapTopology.Lanes, a.Topology);
        Assert.Equal(a.Topology, b.Topology);

        // Deterministic: identical serialised maps for the same options + seed (the seed contract).
        string ja = JsonSerializer.Serialize(TemplateGenerator.Generate(a), JsonExport.Options);
        string jb = JsonSerializer.Serialize(TemplateGenerator.Generate(b), JsonExport.Options);
        Assert.Equal(ja, jb);
    }

    [Fact]
    public void Generate_WhenRoadsAreDisabledLeavesZoneRoadListsEmpty()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration
            {
                Advanced = new AdvancedSettings()
                {
                    NeutralMediumCastleCount = 2
                },
                PlayerZoneCastles = 3,
                NeutralZoneCastles = 2
            },
            SpawnRemoteFootholds = true,
            GenerateRoads = false,
            Topology = MapTopology.Default
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));

        Assert.All(RequiredZones(variant), zone => Assert.Empty(zone.Roads ?? []));
    }

    [Fact]
    public void Generate_WithPlayerIsolationAndNeutralZonesDoesNotCreateDirectPlayerConnections()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration
            {
                Advanced = new AdvancedSettings()
                {
                    NeutralMediumCastleCount = 2
                }
            },
            NoDirectPlayerConnections = true,
            Topology = MapTopology.Default
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var playerToPlayerConnections = RequiredConnections(variant)
            .Where(connection =>
                connection.From.StartsWith("Spawn-", StringComparison.Ordinal) &&
                connection.To.StartsWith("Spawn-", StringComparison.Ordinal));

        Assert.Empty(playerToPlayerConnections);
    }

    [Fact]
    public void Generate_WithRandomPortalsAddsOnePortalPerZone()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 2
            },
            RandomPortals = true,
            Topology = MapTopology.Default
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zones = RequiredZones(variant);
        var portals = RequiredConnections(variant)
            .Where(connection => connection.ConnectionType == "Portal")
            .ToList();

        Assert.Equal(zones.Count, portals.Count);
        Assert.All(portals, portal =>
        {
            Assert.True(portal.Road);
            Assert.NotEmpty(portal.PortalPlacementRulesFrom ?? []);
            Assert.NotEmpty(portal.PortalPlacementRulesTo ?? []);
        });
    }

    [Fact]
    public void Generate_AppliesResourceAndStructureDensitySeparately()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration
            {
                Advanced = new AdvancedSettings()
                {
                    NeutralMediumCastleCount = 2
                },
                ResourceDensityPercent = 50,
                StructureDensityPercent = 150
            },
            MapSize = 160,
            Topology = MapTopology.Default,
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        Zone spawnZone = RequiredZones(variant)
            .First(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal));

        Assert.Equal(300000, spawnZone.GuardedContentValue);
        Assert.Equal(3000, spawnZone.GuardedContentValuePerArea);
        Assert.Equal(75000, spawnZone.UnguardedContentValue);
        Assert.Equal(600, spawnZone.UnguardedContentValuePerArea);
        Assert.Equal(20000, spawnZone.ResourcesValue);
        Assert.Equal(150, spawnZone.ResourcesValuePerArea);
    }

    [Fact]
    public void Generate_AppliesZoneNeutralStrengthToZoneAndMainObjectGuardsOnly()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration
            {
                Advanced = new AdvancedSettings()
                {
                    NeutralMediumCastleCount = 2
                },
                PlayerZoneCastles = 2,
                NeutralZoneCastles = 2,
                NeutralStackStrengthPercent = 200,
                BorderGuardStrengthPercent = 100
            },
            MapSize = 160,
            Topology = MapTopology.Default,
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zones = RequiredZones(variant);
        Zone spawnZone = zones.First(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal));
        Zone neutralZone = zones.First(zone => zone.Name.StartsWith("Neutral-", StringComparison.Ordinal));

        Assert.Equal(2.0, spawnZone.GuardMultiplier);
        Assert.Equal([10000, 5000], spawnZone.MainObjects?.Select(mainObject => mainObject.GuardValue).ToArray());
        Assert.Equal(2.8, neutralZone.GuardMultiplier);
        Assert.Equal([16000, 8000], neutralZone.MainObjects?.Select(mainObject => mainObject.GuardValue).ToArray());
        Assert.All(RequiredConnections(variant), connection => Assert.Equal(20000, connection.GuardValue));
    }

    [Fact]
    public void Generate_AppliesBorderGuardStrengthToDirectAndPortalConnectionsOnly()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration
            {
                Advanced = new AdvancedSettings()
                {
                    NeutralMediumCastleCount = 2
                },
                NeutralStackStrengthPercent = 100,
                BorderGuardStrengthPercent = 50
            },
            MapSize = 160,
            RandomPortals = true,
            Topology = MapTopology.Default
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        Zone spawnZone = RequiredZones(variant)
            .First(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal));
        var directConnections = RequiredConnections(variant)
            .Where(connection => connection.ConnectionType == "Direct")
            .ToList();
        var portalConnections = RequiredConnections(variant)
            .Where(connection => connection.ConnectionType == "Portal")
            .ToList();

        Assert.Equal(1.0, spawnZone.GuardMultiplier);
        Assert.Equal(5000, Assert.Single(spawnZone.MainObjects ?? []).GuardValue);
        Assert.All(directConnections, connection => Assert.Equal(10000, connection.GuardValue));
        Assert.All(portalConnections, connection => Assert.Equal(12500, connection.GuardValue));
    }

    [Fact]
    public void Generate_AdvancedModeAppliesGuardRandomizationToSpawnAndNeutralZones()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 2,
                Advanced = new AdvancedSettings
                {
                    Enabled = true,
                    GuardRandomization = 0.23,
                }
            },
            Topology = MapTopology.Default
        };

        var zones = RequiredZones(SingleVariant(TemplateGenerator.Generate(settings)))
            .Where(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal)
                || zone.Name.StartsWith("Neutral-", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, zone => Assert.Equal(0.23, zone.GuardRandomization));
    }

    [Fact]
    public void Generate_SimpleModeIgnoresGuardRandomizationOverride()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 2,
                Advanced = new AdvancedSettings
                {
                    Enabled = false,
                    GuardRandomization = 0.23,
                }
            },
            Topology = MapTopology.Default
        };

        var zones = RequiredZones(SingleVariant(TemplateGenerator.Generate(settings)))
            .Where(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal)
                || zone.Name.StartsWith("Neutral-", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(zones);
        Assert.All(zones, zone => Assert.Equal(0.05, zone.GuardRandomization));
    }

    [Fact]
    public void Generate_AdvancedNeutralCountsCreateTieredCastleAndNoCastleZones()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCastles = 2,
                Advanced = new AdvancedSettings
                {
                    Enabled = true,
                    NeutralLowNoCastleCount = 1,
                    NeutralLowCastleCount = 1,
                    NeutralMediumNoCastleCount = 1,
                    NeutralMediumCastleCount = 1,
                    NeutralHighNoCastleCount = 1,
                    NeutralHighCastleCount = 1,
                }
            },
            Topology = MapTopology.Default
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);
        Variant variant = SingleVariant(template);
        var zones = RequiredZones(variant).ToDictionary(zone => zone.Name, StringComparer.Ordinal);

        Assert.Equal(8, zones.Count);
        Assert.Empty(zones["Neutral-C"].MainObjects ?? []);
        Assert.Equal(2, zones["Neutral-D"].MainObjects?.Count);
        Assert.Empty(zones["Neutral-E"].MainObjects ?? []);
        Assert.Equal(2, zones["Neutral-F"].MainObjects?.Count);
        Assert.Empty(zones["Neutral-G"].MainObjects ?? []);
        Assert.Equal(2, zones["Neutral-H"].MainObjects?.Count);
        Assert.Equal("MatchZone", zones["Neutral-C"].ZoneBiome?.Type);
        Assert.Equal("MatchMainObject", zones["Neutral-D"].ZoneBiome?.Type);
        Assert.True(zones["Neutral-C"].GuardedContentValue < zones["Neutral-E"].GuardedContentValue);
        Assert.True(zones["Neutral-E"].GuardedContentValue < zones["Neutral-G"].GuardedContentValue);
        Assert.Equal("zone_layout_sides", zones["Neutral-C"].Layout);
        Assert.Equal("zone_layout_sides", zones["Neutral-D"].Layout);
        Assert.Equal("zone_layout_treasure_zone", zones["Neutral-E"].Layout);
        Assert.Equal("zone_layout_treasure_zone", zones["Neutral-F"].Layout);
        Assert.Equal("zone_layout_treasure_zone", zones["Neutral-G"].Layout);
        Assert.Equal("zone_layout_treasure_zone", zones["Neutral-H"].Layout);
        Assert.Contains(template.ZoneLayouts ?? [], layout => layout.Name == "zone_layout_spawns");
        Assert.Contains(template.ZoneLayouts ?? [], layout => layout.Name == "zone_layout_sides");
        Assert.Contains(template.ZoneLayouts ?? [], layout => layout.Name == "zone_layout_treasure_zone");
        Assert.Contains(template.ZoneLayouts ?? [], layout => layout.Name == "zone_layout_center");

        string json = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
        Assert.DoesNotContain("\"tier\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"quality\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_BalancedRingEvenlySpacesPlayersAndNeutralQuality()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 4,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCastles = 1,
                Advanced = new AdvancedSettings
                {
                    Enabled = true,
                    NeutralLowNoCastleCount = 4,
                    NeutralHighCastleCount = 4,
                }

            },
            Topology = MapTopology.Default
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zones = RequiredZones(variant);
        var sequence = zones.Select(zone => zone.Name).ToList();
        var zonesByName = zones.ToDictionary(zone => zone.Name, StringComparer.Ordinal);
        var gaps = RingNeutralGapsBetweenPlayers(sequence);

        Assert.Equal(4, gaps.Count);
        Assert.All(gaps, gap =>
        {
            Assert.Equal(2, gap.Count);
            Assert.Single(gap, zoneName => zonesByName[zoneName].Layout == "zone_layout_treasure_zone");
            Assert.Single(gap, zoneName => zonesByName[zoneName].Layout == "zone_layout_sides");
        });

        var playerDistanceTotals = RingPlayerDistanceTotals(sequence);
        Assert.Single(playerDistanceTotals.Values.Distinct());
    }

    [Fact]
    public void Generate_BalancedSharedWebGivesEachPlayerMixedNeutralQuality()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 4,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCastles = 1,
                Advanced = new AdvancedSettings
                {
                    Enabled = true,
                    NeutralLowNoCastleCount = 4,
                    NeutralHighCastleCount = 4,
                }

            },
            Topology = MapTopology.SharedWeb
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zonesByName = RequiredZones(variant).ToDictionary(zone => zone.Name, StringComparer.Ordinal);
        var webConnectionsByPlayer = RequiredConnections(variant)
            .Where(connection => connection.Name?.StartsWith("Web-", StringComparison.Ordinal) == true)
            .GroupBy(connection => connection.From, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(4, webConnectionsByPlayer.Count);
        Assert.All(webConnectionsByPlayer, group =>
        {
            var connectedLayouts = group
                .Select(connection => zonesByName[connection.To].Layout)
                .ToList();

            Assert.Equal(2, connectedLayouts.Count);
            Assert.Contains("zone_layout_treasure_zone", connectedLayouts);
            Assert.Contains("zone_layout_sides", connectedLayouts);
        });
    }

    [Fact]
    public void Generate_AdvancedModeCanCreateThirtyTwoTotalZones()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 8,
            ZoneCfg = new ZoneConfiguration
            {
                Advanced = new AdvancedSettings
                {
                    Enabled = true,
                    NeutralLowNoCastleCount = 8,
                    NeutralLowCastleCount = 8,
                    NeutralMediumNoCastleCount = 8,
                }
            },
            Topology = MapTopology.Default,
            RandomPortals = true
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zones = RequiredZones(variant);
        var zoneNames = zones.Select(zone => zone.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(32, zones.Count);
        Assert.Contains("Neutral-AF", zoneNames);
        Assert.All(RequiredConnections(variant), connection =>
        {
            Assert.Contains(connection.From, zoneNames);
            Assert.Contains(connection.To, zoneNames);
        });
    }

    [Fact]
    public void Generate_AppliesPlayerAndNeutralZoneSizes()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 2,
                Advanced = new AdvancedSettings
                {
                    Enabled = true,
                    PlayerZoneSize = 2.0,
                    NeutralZoneSize = 0.5,
                }

            },
            Topology = MapTopology.Default
        };

        var zones = RequiredZones(SingleVariant(TemplateGenerator.Generate(settings)));

        Assert.All(zones.Where(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal)),
            zone => Assert.Equal(2.0, zone.Size));
        Assert.All(zones.Where(zone => zone.Name.StartsWith("Neutral-", StringComparison.Ordinal)),
            zone => Assert.Equal(0.5, zone.Size));
    }

    [Fact]
    public void KnownValues_ExperimentalMapSizesContinueOfficialIncrementThrough512()
    {
        Assert.Equal(240, KnownValues.MaxOfficialMapSize);
        Assert.Equal(256, KnownValues.ExperimentalMapSizes[0]);
        Assert.Equal(512, KnownValues.ExperimentalMapSizes[^1]);
        Assert.Equal(17, KnownValues.ExperimentalMapSizes.Length);
        Assert.All(KnownValues.ExperimentalMapSizes, size => Assert.Equal(0, size % 16));
        Assert.Equal(KnownValues.MapSizes.Length + KnownValues.ExperimentalMapSizes.Length, KnownValues.AllMapSizes.Length);
    }

    [Fact]
    public void Generate_AdvancedModeWritesOfficialTournamentWinConditionSettings()
    {
        var settings = new GeneratorSettings
        {
            ZoneCfg = new ZoneConfiguration
            {
                Advanced = new AdvancedSettings
                {
                    Enabled = true
                }
            },
            GameEndConditions = new GameEndConditions
            {
                VictoryCondition = "win_condition_6"
            },
            TournamentRules = new TournamentRules
            {
                FirstTournamentDay = 8,
                Interval = 7,
                PointsToWin = 2
            },
            Topology = MapTopology.Default
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);
        WinConditions? winConditions = template.GameRules?.WinConditions;

        Assert.Equal("win_condition_6", template.DisplayWinCondition);
        Assert.NotNull(winConditions);
        Assert.True(winConditions.Tournament);
        Assert.Null(winConditions.GladiatorArena);
        Assert.Equal([7, 6, 6], winConditions.TournamentDays);
        Assert.Equal([1, 9, 16], winConditions.TournamentAnnounceDays);
        Assert.Equal(2, winConditions.TournamentPointsToWin);
        Assert.True(winConditions.TournamentSaveArmy);
        Assert.False(winConditions.LostStartHero);
        Assert.Equal("StartHero", winConditions.ChampionSelectRule);
    }

    [Fact]
    public void Generate_AdvancedModeAppliesSeparateExperienceModifiers()
    {
        var settings = new GeneratorSettings
        {
            ZoneCfg = new ZoneConfiguration
            {
                Advanced = new AdvancedSettings
                {
                    Enabled = true
                }
            },
            FactionLawsExpPercent = 50,
            AstrologyExpPercent = 200,
            Topology = MapTopology.Default
        };

        GameRules? rules = TemplateGenerator.Generate(settings).GameRules;

        Assert.NotNull(rules);
        Assert.Equal(0.5, rules.FactionLawsExpModifier);
        Assert.Equal(2.0, rules.AstrologyExpModifier);
    }

    [Fact]
    public void Generate_SimpleModeAppliesWinConditionAndExperienceOverrides()
    {
        var settings = new GeneratorSettings
        {
            GameEndConditions = new GameEndConditions
            {
                VictoryCondition = "win_condition_6"
            },
            FactionLawsExpPercent = 50,
            AstrologyExpPercent = 200,
            TournamentRules = new TournamentRules
            {
                Enabled = true
            },
            Topology = MapTopology.Default
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);
        Assert.False(settings.ZoneCfg.Advanced.Enabled); // sanity check that we're in simple mode - advanced mode is off by default.
        Assert.Equal("win_condition_6", template.DisplayWinCondition);
        Assert.Equal(0.5, template.GameRules?.FactionLawsExpModifier);
        Assert.Equal(2.0, template.GameRules?.AstrologyExpModifier);
        Assert.True(template.GameRules?.WinConditions?.Tournament);
    }

    [Fact]
    public void Generate_WhenPlayerCastleFactionMatchingIsEnabled_ExtraCitiesMatchSpawnFaction()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration
            {
                PlayerZoneCastles = 3,
            },
            MatchPlayerCastleFactions = true,
            Topology = MapTopology.Default
        };

        var spawnZones = RequiredZones(SingleVariant(TemplateGenerator.Generate(settings)))
            .Where(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal))
            .ToList();

        Assert.All(spawnZones, zone =>
        {
            var mainObjects = zone.MainObjects ?? [];
            Assert.Equal(3, mainObjects.Count);
            Assert.All(mainObjects.Skip(1), mainObject =>
            {
                Assert.Equal("City", mainObject.Type);
                Assert.Equal("Match", mainObject.Faction?.Type);
                Assert.Equal(["0"], mainObject.Faction?.Args);
            });
        });
    }

    [Fact]
    public void Generate_RingHonorsMinimumNeutralSeparation_WhenEnoughNeutrals()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 3,
            ZoneCfg = new ZoneConfiguration
            {
                Advanced = new AdvancedSettings()
                {
                    NeutralMediumCastleCount = 6
                },
            },
            MinNeutralZonesBetweenPlayers = 2,
            Topology = MapTopology.Default
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var spawnNames = RequiredZones(variant)
            .Where(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal))
            .Select(zone => zone.Name)
            .ToList();

        for (int i = 0; i < spawnNames.Count; i++)
        {
            for (int j = i + 1; j < spawnNames.Count; j++)
                Assert.True(ShortestNeutralIntermediates(variant, spawnNames[i], spawnNames[j]) >= 2);
        }
    }

    [Fact]
    public void CanHonorNeutralSeparation_ReturnsFalseWhenPortalsCouldShortenPaths()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 3,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 6,
            },
            MinNeutralZonesBetweenPlayers = 2,
            RandomPortals = true,
            Topology = MapTopology.Default
        };

        Assert.False(TemplateGenerator.CanHonorNeutralSeparation(settings, settings.ZoneCfg.NeutralZoneCount));
    }

    [Fact]
    public void TemplatePreviewPngWriter_UsesOfficialSidecarNaming()
    {
        Assert.Equal(@"C:\maps\My Template.png", TemplatePreviewPngWriter.GetSidecarPath(@"C:\maps\My Template.rmg.json"));
        Assert.Equal(@"C:\maps\Other.png", TemplatePreviewPngWriter.GetSidecarPath(@"C:\maps\Other.json"));
    }

    [Fact]
    public void TemplatePreviewPngWriter_Save_EncodesNeutralQualityAndCastleCounts()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"olden-preview-test-{Guid.NewGuid():N}");
        string previewPath = Path.Combine(directory, "Preview.png");
        var template = new RmgTemplate
        {
            Name = "Preview Test",
            Variants =
            [
                new Variant
                {
                    Zones =
                    [
                        new Zone { Name = "Neutral-A", Layout = "zone_layout_sides", MainObjects = [] },
                        new Zone { Name = "Neutral-B", Layout = "zone_layout_treasure_zone", MainObjects = Cities(3) },
                        new Zone { Name = "Neutral-C", Layout = "zone_layout_center", MainObjects = Cities(2) }
                    ],
                    Connections = []
                }
            ]
        };

        try
        {
            Directory.CreateDirectory(directory);
            RunOnStaThread(() => TemplatePreviewPngWriter.Save(template, previewPath));
            BitmapSource bitmap = LoadBitmap(previewPath);

            // Use ComputeLayout to find the actual rendered positions (layout is graph-driven, not hardcoded)
            Dictionary<string, Point> layout = null!;
            double zoneRadius = 0;
            RunOnStaThread(() =>
            {
                layout = TemplatePreviewPngWriter.ComputeLayout(template);
                zoneRadius = TemplatePreviewPngWriter.GetLastZoneRadius();
            });

            Point pA = layout["Neutral-A"]; // Bronze (sides)
            Point pB = layout["Neutral-B"]; // Silver (treasure) — 3 castles
            Point pC = layout["Neutral-C"]; // Gold  (center)  — 2 castles

            // Sample a corner of the canvas that is guaranteed to be background
            int bgX = 15, bgY = 15;
            AssertColorNear(Color.FromRgb(28, 22, 16), PixelAt(bitmap, bgX, bgY));

            // The border of the Silver circle (Neutral-B) should show the silver border colour.
            // Sample 1 px inside the circumference to avoid antialiased edge blending.
            int bX = (int)Math.Round(pB.X + zoneRadius - 1);
            int bY = (int)Math.Round(pB.Y);
            bX = Math.Clamp(bX, 0, bitmap.PixelWidth - 1);
            bY = Math.Clamp(bY, 0, bitmap.PixelHeight - 1);
            AssertColorNear(Color.FromRgb(192, 192, 192), PixelAt(bitmap, bX, bY), tolerance: 30);

            // The castle-count label pixels near Neutral-B should be brighter than the plain background.
            var bgRect    = new Int32Rect(bgX, bgY, 24, 20);
            var labelRect = new Int32Rect(
                Math.Clamp((int)pB.X - 12, 0, bitmap.PixelWidth  - 25),
                Math.Clamp((int)pB.Y - 10, 0, bitmap.PixelHeight - 21),
                24, 20);
            int lowLabelPixels    = CountBrightPixels(bitmap, bgRect);
            int castleLabelPixels = CountBrightPixels(bitmap, labelRect);
            Assert.True(castleLabelPixels > lowLabelPixels + 10);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SettingsFile_LegacyContentDensitySeedsSplitDensitySettings()
    {
        const string json = """
            {
              "contentDensity": 130
            }
            """;

        SettingsFile? settings = JsonSerializer.Deserialize<SettingsFile>(json);

        Assert.NotNull(settings);
        Assert.Equal(130, settings.EffectiveResourceDensityPercent);
        Assert.Equal(130, settings.EffectiveStructureDensityPercent);
        Assert.Equal(100, settings.NeutralStackStrengthPercent);
        Assert.Equal(100, settings.BorderGuardStrengthPercent);
    }

    [Fact]
    public void Generate_CanSerializeAndDeserializeRmgJson()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "Round Trip Template",
            PlayerCount = 2,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 1
            },
            Topology = MapTopology.Default
        };

        RmgTemplate generated = TemplateGenerator.Generate(settings);
        string json = JsonSerializer.Serialize(generated, new JsonSerializerOptions { WriteIndented = true });
        RmgTemplate? deserialized = JsonSerializer.Deserialize<RmgTemplate>(json);

        Assert.Contains("\"name\": \"Round Trip Template\"", json, StringComparison.Ordinal);
        Assert.NotNull(deserialized);
        Assert.Equal(generated.Name, deserialized.Name);
        Assert.Single(deserialized.Variants ?? []);
    }

    // ── Loading stock templates: sid-list fields may be a bare string OR an array ──
    // The official templates (Expanse, Crossroads, Wastelands, …) write fields like
    // contentCountLimits as a bare string in some zones and an array in others. The default
    // deserializer only accepted arrays, so opening such a template failed with
    // "The JSON value could not be converted to System.Collections.Generic.List`1[System.String]
    //  Path: $.variants[0].zones[0].contentCountLimits". JsonExport.Options must tolerate both.

    [Fact]
    public void Load_ZoneSidListField_AcceptsBothBareStringAndArray()
    {
        // zones[0] uses the bare-string form (the exact shape that broke loading Expanse.rmg.json);
        // zones[1] uses the array form. Other sid-list fields are exercised in both forms too.
        const string json = """
            {
              "variants": [
                {
                  "zones": [
                    {
                      "name": "Spawn-A",
                      "contentCountLimits": "content_limits_spawn",
                      "guardedContentPool": "content_pool_start_guarded",
                      "mandatoryContent": "mandatory_content_spawn"
                    },
                    {
                      "name": "Spawn-B",
                      "contentCountLimits": ["content_limits_spawn"],
                      "guardedContentPool": ["content_pool_a", "content_pool_b"]
                    }
                  ]
                }
              ]
            }
            """;

        RmgTemplate? template = JsonSerializer.Deserialize<RmgTemplate>(json, JsonExport.Options);

        Assert.NotNull(template);
        List<Zone> zones = template.Variants![0].Zones!;

        // Bare-string form is read as a single-element list.
        Assert.Equal(new[] { "content_limits_spawn" }, zones[0].ContentCountLimits);
        Assert.Equal(new[] { "content_pool_start_guarded" }, zones[0].GuardedContentPool);
        Assert.Equal(new[] { "mandatory_content_spawn" }, zones[0].MandatoryContent);

        // Array form is unchanged.
        Assert.Equal(new[] { "content_limits_spawn" }, zones[1].ContentCountLimits);
        Assert.Equal(new[] { "content_pool_a", "content_pool_b" }, zones[1].GuardedContentPool);
    }

    [Fact]
    public void Save_ZoneSidListField_AlwaysWrittenAsArray()
    {
        // A bare-string field read in must round-trip back out as an array (the generator's
        // canonical form), so re-saving a loaded stock template normalises it.
        const string json = """
            { "variants": [ { "zones": [ { "name": "Z", "contentCountLimits": "content_limits_spawn" } ] } ] }
            """;

        RmgTemplate template = JsonSerializer.Deserialize<RmgTemplate>(json, JsonExport.Options)!;
        string written = JsonSerializer.Serialize(template, JsonExport.Options);

        Assert.Contains("\"contentCountLimits\": [", written, StringComparison.Ordinal);
        Assert.DoesNotContain("\"contentCountLimits\": \"content_limits_spawn\"", written, StringComparison.Ordinal);

        // And the normalised output reloads cleanly.
        RmgTemplate? reloaded = JsonSerializer.Deserialize<RmgTemplate>(written, JsonExport.Options);
        Assert.Equal(new[] { "content_limits_spawn" }, reloaded!.Variants![0].Zones![0].ContentCountLimits);
    }

    [Fact]
    public void Load_StringList_AcceptsNumericElements()
    {
        // Some templates write rule args as bare numbers (e.g. Pyramid / Sand Clover: "args": [0]).
        // The game treats them as string sids; we read them as their literal text.
        List<string>? list = JsonSerializer.Deserialize<List<string>>("[0, \"x\", 12]", JsonExport.Options);

        Assert.Equal(new[] { "0", "x", "12" }, list);
    }

    [Fact]
    public void Load_ObjectList_AcceptsSingleBareObject()
    {
        // gameRules.bonuses in some templates (e.g. Wastelands) is a single bare object, not an array.
        const string json = """
            {
              "gameRules": {
                "bonuses": { "sid": "add_bonus_hero_item", "receiverFilter": "start_hero" }
              }
            }
            """;

        RmgTemplate? template = JsonSerializer.Deserialize<RmgTemplate>(json, JsonExport.Options);

        List<Bonus> bonuses = template!.GameRules!.Bonuses!;
        Assert.Single(bonuses);
        Assert.Equal("add_bonus_hero_item", bonuses[0].Sid);

        // Re-saving normalises the single object back into an array.
        string written = JsonSerializer.Serialize(template, JsonExport.Options);
        Assert.Contains("\"bonuses\": [", written, StringComparison.Ordinal);
    }

    // ── Simple Mode / Quick Generate (RandomTemplateBuilder) ──────────────────────
    // Core promise: ANY combination of simple options + ANY seed must yield a template
    // that is structurally valid (no dangling/duplicate/isolated zones) and serialises to
    // game-compatible JSON. These sweeps are the safety net for the random corridors.

    private static void AssertQuickTemplateValid(QuickGenerateOptions opts)
    {
        GeneratorSettings settings = RandomTemplateBuilder.Build(opts);
        RmgTemplate template = TemplateGenerator.Generate(settings);

        Variant variant = Assert.Single(template.Variants ?? []);
        Assert.NotNull(variant.Zones);
        Assert.True(variant.Zones!.Count >= settings.PlayerCount,
            $"only {variant.Zones.Count} zones for {settings.PlayerCount} players");

        List<string> issues = ZoneGraphValidator.Validate(variant.Zones!, variant.Connections ?? []);
        Assert.True(issues.Count == 0,
            $"seed {opts.Seed} {opts.GameType}/{opts.Scale}/{opts.Length}/{opts.Chaos} " +
            $"players={settings.PlayerCount} topo={settings.Topology} size={settings.MapSize}: {string.Join("; ", issues)}");

        // Must produce game-compatible JSON and reload cleanly.
        string json = JsonSerializer.Serialize(template, JsonExport.Options);
        Assert.NotNull(JsonSerializer.Deserialize<RmgTemplate>(json, JsonExport.Options));
    }

    [Fact]
    public void QuickGenerate_EveryGameTypeScaleLengthChaosCombo_IsValid()
    {
        foreach (QuickGameType type in Enum.GetValues<QuickGameType>())
        foreach (QuickMapScale scale in Enum.GetValues<QuickMapScale>())
        foreach (QuickGameLength length in Enum.GetValues<QuickGameLength>())
        foreach (QuickChaos chaos in Enum.GetValues<QuickChaos>())
        {
            AssertQuickTemplateValid(new QuickGenerateOptions
            {
                Seed = 12345, PlayerCount = 4,
                GameType = type, Scale = scale, Length = length, Chaos = chaos,
            });
        }
    }

    [Fact]
    public void QuickGenerate_RandomSeedsPlayerCountsAndToggles_AreValid()
    {
        for (int seed = 0; seed < 120; seed++)
        {
            var pick = new Random(seed);
            AssertQuickTemplateValid(new QuickGenerateOptions
            {
                Seed = seed,
                PlayerCount = pick.Next(2, 9),
                GameType = (QuickGameType)pick.Next(0, 4),
                Scale = (QuickMapScale)pick.Next(0, 4),
                Length = (QuickGameLength)pick.Next(0, 3),
                Chaos = (QuickChaos)pick.Next(0, 3),
                Water = pick.NextDouble() < 0.5,
                Portals = pick.NextDouble() < 0.4,
                StrongNeutrals = pick.NextDouble() < 0.4,
            });
        }
    }

    [Fact]
    public void QuickGenerate_SameOptionsAndSeed_AreDeterministic()
    {
        QuickGenerateOptions Make() => new()
        {
            Seed = 4242, PlayerCount = 4, GameType = QuickGameType.FreeForAll,
            Scale = QuickMapScale.Medium, Length = QuickGameLength.Long, Chaos = QuickChaos.Wild,
            Water = true, Portals = true, StrongNeutrals = true,
        };

        GeneratorSettings a = RandomTemplateBuilder.Build(Make());
        GeneratorSettings b = RandomTemplateBuilder.Build(Make());

        Assert.Equal(a.MapSize, b.MapSize);
        Assert.Equal(a.Topology, b.Topology);
        Assert.Equal(a.WaterLevel, b.WaterLevel);
        Assert.Equal(a.MonsterAggression, b.MonsterAggression);
        Assert.Equal(a.ZoneCfg.ResourceDensityPercent, b.ZoneCfg.ResourceDensityPercent);
        Assert.Equal(a.ZoneCfg.NeutralStackStrengthPercent, b.ZoneCfg.NeutralStackStrengthPercent);
        Assert.Equal(a.ZoneCfg.Advanced.NeutralLowNoCastleCount + a.ZoneCfg.Advanced.NeutralHighCastleCount,
                     b.ZoneCfg.Advanced.NeutralLowNoCastleCount + b.ZoneCfg.Advanced.NeutralHighCastleCount);
        Assert.Equal(a.TemplateName, b.TemplateName);
    }

    [Fact]
    public void QuickGenerate_SameSeed_ReproducesIdenticalMap()
    {
        // The shareable-seed promise: same options + seed → byte-identical template.
        var opts = new QuickGenerateOptions
        {
            Seed = 0x7F3A22, PlayerCount = 4, GameType = QuickGameType.FreeForAll,
            Scale = QuickMapScale.Medium, Length = QuickGameLength.Long, Chaos = QuickChaos.Wild,
            Water = true, Portals = true,
        };

        string first = JsonSerializer.Serialize(TemplateGenerator.Generate(RandomTemplateBuilder.Build(opts)), JsonExport.Options);
        string second = JsonSerializer.Serialize(TemplateGenerator.Generate(RandomTemplateBuilder.Build(opts)), JsonExport.Options);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Generate_DifferentSeedsGenerallyDiffer()
    {
        // Sanity that seeding doesn't collapse every map to the same output.
        var a = new QuickGenerateOptions { Seed = 1, PlayerCount = 6, GameType = QuickGameType.FreeForAll, Chaos = QuickChaos.Wild };
        var b = new QuickGenerateOptions { Seed = 2, PlayerCount = 6, GameType = QuickGameType.FreeForAll, Chaos = QuickChaos.Wild };

        string ja = JsonSerializer.Serialize(TemplateGenerator.Generate(RandomTemplateBuilder.Build(a)), JsonExport.Options);
        string jb = JsonSerializer.Serialize(TemplateGenerator.Generate(RandomTemplateBuilder.Build(b)), JsonExport.Options);

        Assert.NotEqual(ja, jb);
    }

    [Fact]
    public void QuickGenerate_BorderGuardLevel_SetsDistinctStrengthBands()
    {
        // The player-facing "border guards" control must actually move the border-guard strength so the
        // player can pick how hard it is to cross a zone border (the "face-control" against early rushes).
        // Each level lands in its own band; Normal keeps the historical 80–140% so old seeds are unchanged.
        QuickGenerateOptions Opt(QuickGuardLevel g) => new()
        {
            Seed = 0x51EED5, PlayerCount = 4, GameType = QuickGameType.FreeForAll,
            Scale = QuickMapScale.Medium, Length = QuickGameLength.Medium, Chaos = QuickChaos.Normal,
            BorderGuards = g,
        };

        int Strength(QuickGuardLevel g) => RandomTemplateBuilder.Build(Opt(g)).ZoneCfg.BorderGuardStrengthPercent;

        Assert.InRange(Strength(QuickGuardLevel.Weak),     45, 80);
        Assert.InRange(Strength(QuickGuardLevel.Normal),   80, 140);
        Assert.InRange(Strength(QuickGuardLevel.Strong),   150, 220);
        Assert.InRange(Strength(QuickGuardLevel.Fortress), 230, 300);
    }

    [Fact]
    public void QuickGenerate_DuelForcesTwoPlayers_AndNameIsAscii()
    {
        GeneratorSettings s = RandomTemplateBuilder.Build(new QuickGenerateOptions
        {
            Seed = 7, PlayerCount = 6, GameType = QuickGameType.Duel,
        });

        Assert.Equal(2, s.PlayerCount);
        Assert.All(s.TemplateName, ch => Assert.True(ch < 128, $"non-ASCII char in name: {s.TemplateName}"));
    }

    [Fact]
    public void QuickGenerate_HugeScale_ProducesLargeExperimentalMap()
    {
        // The Huge scale is the answer to "I want maps bigger than 240×240": it must always land
        // above the official cap, inside the experimental size set, and stay in the ≈256–400 band.
        foreach (QuickGameType type in Enum.GetValues<QuickGameType>())
        foreach (QuickGameLength length in Enum.GetValues<QuickGameLength>())
        for (int seed = 0; seed < 8; seed++)
        for (int players = 2; players <= 8; players++)
        {
            GeneratorSettings s = RandomTemplateBuilder.Build(new QuickGenerateOptions
            {
                Seed = seed * 31 + players, PlayerCount = players, GameType = type,
                Scale = QuickMapScale.Huge, Length = length, Chaos = QuickChaos.Normal,
            });
            string id = $"{type}/{length} p={players} seed={seed}";

            Assert.True(s.MapSize > KnownValues.MaxOfficialMapSize, $"{id}: {s.MapSize} not above the 240 cap");
            Assert.True(KnownValues.IsExperimentalMapSize(s.MapSize), $"{id}: {s.MapSize} not an experimental size");
            Assert.InRange(s.MapSize, 256, 400);
        }
    }

    // Helper: enumerate a broad, deterministic spread of quick options for the invariant sweeps.
    private static IEnumerable<QuickGenerateOptions> QuickOptionMatrix()
    {
        foreach (QuickGameType type in Enum.GetValues<QuickGameType>())
        foreach (QuickMapScale scale in Enum.GetValues<QuickMapScale>())
        foreach (QuickGameLength length in Enum.GetValues<QuickGameLength>())
        foreach (QuickChaos chaos in Enum.GetValues<QuickChaos>())
        for (int seed = 0; seed < 4; seed++)
        {
            var pick = new Random(seed * 97 + (int)type * 13 + (int)scale * 5 + (int)length * 3 + (int)chaos);
            for (int players = 2; players <= 8; players += 3)
                yield return new QuickGenerateOptions
                {
                    Seed = seed * 100 + players, PlayerCount = players,
                    GameType = type, Scale = scale, Length = length, Chaos = chaos,
                    Water = pick.NextDouble() < 0.5, Portals = pick.NextDouble() < 0.4,
                    StrongNeutrals = pick.NextDouble() < 0.4,
                    BorderGuards = (QuickGuardLevel)pick.Next(0, 4),
                };
        }
    }

    [Fact]
    public void QuickGenerate_SettingsStayWithinAdvancedUiRanges()
    {
        // Simple Mode must never feed TemplateGenerator anything the Advanced UI couldn't also
        // produce — so its output stays inside the already-proven (in-game-tested) envelope.
        // Ranges below mirror the Advanced-tab sliders/combos.
        foreach (QuickGenerateOptions opts in QuickOptionMatrix())
        {
            GeneratorSettings s = RandomTemplateBuilder.Build(opts);
            var z = s.ZoneCfg;
            string id = $"{opts.GameType}/{opts.Scale}/{opts.Length}/{opts.Chaos} p={s.PlayerCount}";

            // Advanced UI can also produce the experimental 256–512 sizes (the "large maps" checkbox),
            // so AllMapSizes — not just the official set — is the correct envelope. The Huge scale uses it.
            Assert.True(KnownValues.AllMapSizes.Contains(s.MapSize), $"{id}: map size {s.MapSize} not a known size");
            Assert.InRange(s.PlayerCount, 2, 8);
            Assert.InRange(z.ResourceDensityPercent, 20, 400);   // SldResourceDensity
            Assert.InRange(z.StructureDensityPercent, 20, 200);  // SldStructureDensity
            Assert.InRange(z.NeutralStackStrengthPercent, 25, 300); // SldNeutralStackStrength
            Assert.InRange(z.BorderGuardStrengthPercent, 20, 400);
            Assert.InRange(s.TerrainRoughnessPercent, 20, 400);
            Assert.InRange(s.LakeAmountPercent, 20, 400);
            Assert.InRange(s.NeutralDiplomacyModifier, -1.0, 0.5);
            Assert.Contains(s.GameEndConditions.VictoryCondition, KnownValues.VictoryConditionIds);

            var a = z.Advanced;
            int neutrals = a.NeutralLowNoCastleCount + a.NeutralLowCastleCount
                         + a.NeutralMediumNoCastleCount + a.NeutralMediumCastleCount
                         + a.NeutralHighNoCastleCount + a.NeutralHighCastleCount;
            int totalZones = s.PlayerCount + neutrals;
            Assert.True(totalZones <= 32, $"{id}: {totalZones} zones exceeds the 32-zone cap");
            // The Advanced UI warns when a zone gets < 1024 map area; the builder must respect that.
            Assert.True((double)s.MapSize * s.MapSize / totalZones >= 1024.0,
                $"{id}: overcrowded — {totalZones} zones on {s.MapSize}² (< 1024 area/zone)");
        }
    }

    [Fact]
    public void QuickGenerate_MapsSatisfyPlayabilityInvariants()
    {
        foreach (QuickGenerateOptions opts in QuickOptionMatrix())
        {
            GeneratorSettings s = RandomTemplateBuilder.Build(opts);
            RmgTemplate tpl = TemplateGenerator.Generate(s);
            string id = $"{opts.GameType}/{opts.Scale}/{opts.Length}/{opts.Chaos} p={s.PlayerCount} seed={opts.Seed}";

            Variant variant = Assert.Single(tpl.Variants ?? []);
            Assert.NotNull(variant.Zones);

            // Graph integrity.
            List<string> issues = ZoneGraphValidator.Validate(variant.Zones!, variant.Connections ?? []);
            Assert.True(issues.Count == 0, $"{id}: {string.Join("; ", issues)}");

            // Exactly one spawn per player, all distinct.
            var spawns = variant.Zones!
                .SelectMany(zo => zo.MainObjects ?? [])
                .Where(o => o.Type == "Spawn")
                .Select(o => o.Spawn)
                .ToList();
            Assert.Equal(s.PlayerCount, spawns.Count);
            Assert.Equal(spawns.Count, spawns.Distinct().Count());

            // Content is actually budgeted (zones won't be empty) and count-limits exist.
            Assert.NotNull(tpl.ContentCountLimits);
            Assert.NotEmpty(tpl.ContentCountLimits!);
            Assert.Contains(variant.Zones!, zo => (zo.GuardedContentValue ?? 0) > 0 || (zo.ResourcesValue ?? 0) > 0);
        }
    }

    [Fact]
    public void QuickGenerate_EveryVictoryConditionProducesValidMap()
    {
        // Every real in-game mode (the same set the Advanced tab exposes) must generate a valid map,
        // carry the right win condition, and honour its constraints (tournament → 2 players;
        // city hold → a castle neutral to hold).
        foreach (string victory in KnownValues.VictoryConditionIds)
        foreach (QuickGameType type in Enum.GetValues<QuickGameType>())
        for (int seed = 0; seed < 3; seed++)
        {
            var opts = new QuickGenerateOptions
            {
                Seed = seed, PlayerCount = 4, GameType = type,
                Scale = QuickMapScale.Medium, Length = QuickGameLength.Medium, Chaos = QuickChaos.Normal,
                VictoryCondition = victory,
            };
            GeneratorSettings s = RandomTemplateBuilder.Build(opts);
            string id = $"{victory}/{type} seed={seed}";

            Assert.Equal(victory, s.GameEndConditions.VictoryCondition);
            if (victory == "win_condition_6") Assert.Equal(2, s.PlayerCount); // tournament = two 1v1 clusters
            if (victory == "win_condition_5")
            {
                var a = s.ZoneCfg.Advanced;
                Assert.True(a.NeutralLowCastleCount + a.NeutralMediumCastleCount + a.NeutralHighCastleCount > 0,
                    $"{id}: city hold needs a castle neutral to hold");
            }

            RmgTemplate tpl = TemplateGenerator.Generate(s);
            Variant v = Assert.Single(tpl.Variants ?? []);
            List<string> issues = ZoneGraphValidator.Validate(v.Zones!, v.Connections ?? []);
            Assert.True(issues.Count == 0, $"{id}: {string.Join("; ", issues)}");
            Assert.Equal(victory, tpl.DisplayWinCondition);
        }
    }

    [Fact]
    public void Generate_ConnectionsReferToGeneratedZones()
    {
        var settings = new GeneratorSettings
        {
            PlayerCount = 4,
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = 3
            },
            RandomPortals = true,
            Topology = MapTopology.Default
        };

        Variant variant = SingleVariant(TemplateGenerator.Generate(settings));
        var zoneNames = RequiredZones(variant).Select(zone => zone.Name).ToHashSet(StringComparer.Ordinal);

        Assert.All(RequiredConnections(variant), connection =>
        {
            Assert.Contains(connection.From, zoneNames);
            Assert.Contains(connection.To, zoneNames);
        });
    }

    // ── Bonuses ───────────────────────────────────────────────────────────────

    [Fact]
    public void Generate_WithNoBonusesLeavesGameRulesBonusesNull()
    {
        var settings = new GeneratorSettings { Bonuses = [] };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.Null(template.GameRules?.Bonuses);
    }

    [Fact]
    public void Generate_TownPortalFreeBonusExpandsToTwoRawBonuses()
    {
        var settings = new GeneratorSettings
        {
            Bonuses =
            [
                new BonusEntry { PresetType = BonusPresetType.TownPortalFree, ReceiverFilter = "start_hero" }
            ]
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template.GameRules?.Bonuses);
        var bonuses = template.GameRules!.Bonuses!;
        Assert.Equal(2, bonuses.Count);
        Assert.Contains(bonuses, b => b.Sid == "add_bonus_hero_spell" && (b.Parameters?.Contains("neutral_magic_town_portal") ?? false));
        Assert.Contains(bonuses, b => b.Sid == "add_bonus_hero_stat"  && (b.Parameters?.Contains("neutral_magic_town_portal") ?? false));
    }

    [Fact]
    public void Generate_StartingItemBonusExpandsToOneRawBonus()
    {
        var settings = new GeneratorSettings
        {
            Bonuses =
            [
                new BonusEntry { PresetType = BonusPresetType.StartingItem, ReceiverFilter = "all_heroes", Param = "amulet_of_health" }
            ]
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template.GameRules?.Bonuses);
        var bonuses = template.GameRules!.Bonuses!;
        Bonus bonus = Assert.Single(bonuses);
        Assert.Equal("add_bonus_hero_item", bonus.Sid);
        Assert.Equal("all_heroes", bonus.ReceiverFilter);
        Assert.Contains("amulet_of_health", bonus.Parameters ?? []);
    }

    [Fact]
    public void Generate_MultipleBonusEntriesAreAllExpanded()
    {
        var settings = new GeneratorSettings
        {
            Bonuses =
            [
                new BonusEntry { PresetType = BonusPresetType.StartingGold,    Param = "2000" },
                new BonusEntry { PresetType = BonusPresetType.MovementBonus,   Param = "200"  },
            ]
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template.GameRules?.Bonuses);
        var bonuses = template.GameRules!.Bonuses!;
        Assert.Equal(2, bonuses.Count);
        Assert.Contains(bonuses, b => b.Sid == "add_bonus_res"      && (b.Parameters?.Contains("gold") ?? false));
        Assert.Contains(bonuses, b => b.Sid == "add_bonus_hero_stat" && (b.Parameters?.Contains("movementBonus") ?? false));
    }

    // ── Value overrides ───────────────────────────────────────────────────────

    [Fact]
    public void Generate_WithEmptyValueOverridesTextLeavesOverridesNull()
    {
        var settings = new GeneratorSettings { ValueOverridesText = "" };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.Null(template.ValueOverrides);
    }

    [Fact]
    public void Generate_ParsesValidValueOverrideLines()
    {
        var settings = new GeneratorSettings
        {
            ValueOverridesText = "dragon_utopia=10000\narena=5000"
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template.ValueOverrides);
        var overrides = template.ValueOverrides!;
        Assert.Equal(2, overrides.Count);
        Assert.Contains(overrides, v => v.Sid == "dragon_utopia" && v.GuardValue == 10000 && v.Variant == -1);
        Assert.Contains(overrides, v => v.Sid == "arena"         && v.GuardValue == 5000  && v.Variant == -1);
    }

    [Fact]
    public void Generate_SkipsMalformedAndBlankValueOverrideLines()
    {
        var settings = new GeneratorSettings
        {
            ValueOverridesText = "arena=5000\n\nbadline\n=1000\nalchemy_lab=3000"
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template.ValueOverrides);
        var overrides = template.ValueOverrides!;
        Assert.Equal(2, overrides.Count);
        Assert.Contains(overrides, v => v.Sid == "arena");
        Assert.Contains(overrides, v => v.Sid == "alchemy_lab");
    }

    // ── Global bans ───────────────────────────────────────────────────────────

    [Fact]
    public void Generate_WithNoBansLeavesGlobalBansNull()
    {
        var settings = new GeneratorSettings { BannedItems = "", BannedMagics = "" };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.Null(template.GlobalBans);
    }

    [Fact]
    public void Generate_BannedItemsAreWrittenToGlobalBansItems()
    {
        var settings = new GeneratorSettings
        {
            BannedItems  = "amulet_of_health\nring_of_life",
            BannedMagics = ""
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template.GlobalBans);
        var bans = template.GlobalBans!;
        Assert.NotNull(bans.Items);
        Assert.Contains("amulet_of_health", bans.Items);
        Assert.Contains("ring_of_life",     bans.Items);
        Assert.Null(bans.Magics);
    }

    [Fact]
    public void Generate_BannedMagicsAreWrittenToGlobalBansMagics()
    {
        var settings = new GeneratorSettings
        {
            BannedItems  = "",
            BannedMagics = "neutral_magic_town_portal\nneutral_magic_dimension_door"
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template.GlobalBans);
        var bans = template.GlobalBans!;
        Assert.Null(bans.Items);
        Assert.NotNull(bans.Magics);
        Assert.Contains("neutral_magic_town_portal",    bans.Magics);
        Assert.Contains("neutral_magic_dimension_door", bans.Magics);
    }

    [Fact]
    public void Generate_ItemsAndMagicsBannedTogetherWritesBothLists()
    {
        var settings = new GeneratorSettings
        {
            BannedItems  = "amulet_of_health",
            BannedMagics = "neutral_magic_town_portal"
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template.GlobalBans);
        var bans = template.GlobalBans!;
        Assert.NotNull(bans.Items);
        Assert.NotNull(bans.Magics);
    }

    private static List<MainObject> Cities(int count) =>
        Enumerable.Range(0, count).Select(_ => new MainObject { Type = "City" }).ToList();

    private static void RunOnStaThread(Action action)
    {
        Exception? thrown = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                thrown = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (thrown is not null)
            throw new InvalidOperationException("The STA action failed.", thrown);
    }

    private static BitmapSource LoadBitmap(string path)
    {
        using var stream = File.OpenRead(path);
        BitmapSource source = BitmapDecoder
            .Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad)
            .Frames[0];

        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    private static Color PixelAt(BitmapSource bitmap, int x, int y)
    {
        byte[] pixels = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixels, 4, 0);
        return Color.FromRgb(pixels[2], pixels[1], pixels[0]);
    }

    private static int CountBrightPixels(BitmapSource bitmap, Int32Rect rect)
    {
        int stride = rect.Width * 4;
        byte[] pixels = new byte[stride * rect.Height];
        bitmap.CopyPixels(rect, pixels, stride, 0);

        int count = 0;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte blue = pixels[i];
            byte green = pixels[i + 1];
            byte red = pixels[i + 2];
            if (red >= 180 && green >= 180 && blue >= 180)
                count++;
        }

        return count;
    }

    private static void AssertColorNear(Color expected, Color actual, int tolerance = 8)
    {
        Assert.True(
            Math.Abs(expected.R - actual.R) <= tolerance &&
            Math.Abs(expected.G - actual.G) <= tolerance &&
            Math.Abs(expected.B - actual.B) <= tolerance,
            $"Expected color near RGB({expected.R}, {expected.G}, {expected.B}), got RGB({actual.R}, {actual.G}, {actual.B}).");
    }

    // ── New generation parameters ────────────────────────────────────────────

    [Fact]
    public void Generate_FactionBasedTerrain_KeepsEngineDefaultBiomeSelectors()
    {
        RmgTemplate template = TemplateGenerator.Generate(new GeneratorSettings());

        Zone spawn = FirstSpawnZone(template);
        Assert.Equal("MatchMainObject", spawn.ZoneBiome?.Type);
        Assert.Equal(new[] { "0" }, spawn.ZoneBiome?.Args);
        Assert.Equal("MatchMainObject", spawn.ContentBiome?.Type);
        Assert.Equal("MatchMainObject", spawn.MetaObjectsBiome?.Type);
    }

    [Fact]
    public void Generate_SpecificTerrain_LocksZoneBiomeToThatTerrain()
    {
        RmgTemplate template = TemplateGenerator.Generate(new GeneratorSettings { Terrain = TerrainTheme.Snow });

        Zone spawn = FirstSpawnZone(template);
        Assert.Equal("FromList", spawn.ZoneBiome?.Type);
        Assert.Equal(new[] { "Snow" }, spawn.ZoneBiome?.Args);
        // Content and meta objects follow the zone so the whole zone is visually consistent.
        Assert.Equal("MatchZone", spawn.ContentBiome?.Type);
        Assert.Equal("MatchZone", spawn.MetaObjectsBiome?.Type);
    }

    [Fact]
    public void Generate_RandomTerrain_OffersAllSevenBiomes()
    {
        RmgTemplate template = TemplateGenerator.Generate(new GeneratorSettings { Terrain = TerrainTheme.Random });

        Zone spawn = FirstSpawnZone(template);
        Assert.Equal("FromList", spawn.ZoneBiome?.Type);
        Assert.Equal(7, spawn.ZoneBiome?.Args?.Count);
        Assert.Contains("Lava", spawn.ZoneBiome?.Args ?? []);
        Assert.Contains("Autumn", spawn.ZoneBiome?.Args ?? []);
    }

    [Fact]
    public void Generate_MatchSpawnTerrainToFaction_KeepsSpawnFactionDerivedDespiteForcedTheme()
    {
        // A forced theme (Snow) normally locks the spawn biome to "FromList"; with the flag on, the
        // SPAWN zone stays faction-derived (MatchMainObject[0]) so a player's home matches their faction.
        RmgTemplate template = TemplateGenerator.Generate(
            new GeneratorSettings { Terrain = TerrainTheme.Snow, MatchSpawnTerrainToFaction = true });

        Zone spawn = FirstSpawnZone(template);
        Assert.Equal("MatchMainObject", spawn.ZoneBiome?.Type);
        Assert.Equal(new[] { "0" }, spawn.ZoneBiome?.Args);
        Assert.Equal("MatchMainObject", spawn.ContentBiome?.Type);
    }

    [Fact]
    public void QuickGenerate_SeedsStarterMinesAndFactionMatchedHomeTerrain()
    {
        // Round-6 feedback (Toni): quick maps must spawn ownable mines (not just one-shot resource piles),
        // and each player's starting terrain must match their faction regardless of the rolled theme.
        foreach (int seed in new[] { 1, 7, 99, 12345 })
        {
            GeneratorSettings s = RandomTemplateBuilder.Build(new QuickGenerateOptions
            {
                Seed = seed, PlayerCount = 4, GameType = QuickGameType.FreeForAll,
                Scale = QuickMapScale.Medium, Length = QuickGameLength.Medium, Chaos = QuickChaos.Normal,
            });

            // Starter mines (Wood/Ore/Gold) are guaranteed in every player's spawn zone.
            Assert.True(s.MatchSpawnTerrainToFaction);
            var sids = s.PlayerZoneMandatoryContent.Select(c => c.Sid).ToList();
            Assert.Contains("mine_wood", sids);
            Assert.Contains("mine_ore", sids);
            Assert.Contains("mine_gold", sids);

            // Every player's spawn zone is faction-derived regardless of the rolled terrain theme.
            RmgTemplate template = TemplateGenerator.Generate(s);
            foreach (Zone z in RequiredZones(SingleVariant(template))
                         .Where(z => z.Name.StartsWith("Spawn-", StringComparison.Ordinal)))
                Assert.Equal("MatchMainObject", z.ZoneBiome?.Type);

            // End-to-end: the mines actually reach the serialised .rmg.json (not just the settings object).
            string json = JsonSerializer.Serialize(template, JsonExport.Options);
            Assert.Contains("mine_gold", json);
        }
    }

    [Fact]
    public void Generate_NormalAggression_ReproducesHistoricalSpawnReaction()
    {
        RmgTemplate template = TemplateGenerator.Generate(new GeneratorSettings());

        Zone spawn = FirstSpawnZone(template);
        Assert.Equal(new[] { 60, 20, 10, 10, 2, 0 }, spawn.GuardReactionDistribution);
    }

    [Fact]
    public void Generate_AggressiveAndPassive_ShiftGuardReactionWeights()
    {
        Zone passive = FirstSpawnZone(TemplateGenerator.Generate(
            new GeneratorSettings { MonsterAggression = MonsterAggression.Passive }));
        Zone aggressive = FirstSpawnZone(TemplateGenerator.Generate(
            new GeneratorSettings { MonsterAggression = MonsterAggression.Aggressive }));

        List<int> passiveDist = passive.GuardReactionDistribution ?? [];
        List<int> aggressiveDist = aggressive.GuardReactionDistribution ?? [];

        // Passive favours the calmest bucket (index 0); aggressive favours the fight buckets (3-5).
        Assert.True(passiveDist[0] > aggressiveDist[0]);
        Assert.True(aggressiveDist.Skip(3).Sum() > passiveDist.Skip(3).Sum());
    }

    [Fact]
    public void Generate_DiplomacyModifier_PropagatesToZonesAndIsClamped()
    {
        Zone spawn = FirstSpawnZone(TemplateGenerator.Generate(
            new GeneratorSettings { NeutralDiplomacyModifier = 0.25 }));
        Assert.Equal(0.25, spawn.DiplomacyModifier);

        Zone clamped = FirstSpawnZone(TemplateGenerator.Generate(
            new GeneratorSettings { NeutralDiplomacyModifier = 5.0 }));
        Assert.Equal(1.0, clamped.DiplomacyModifier);
    }

    [Fact]
    public void Generate_EncounterHoles_TogglesGameRuleAndZoneSettings()
    {
        RmgTemplate off = TemplateGenerator.Generate(new GeneratorSettings());
        Assert.Equal(false, off.GameRules?.EncounterHoles);
        Assert.Null(FirstSpawnZone(off).EncounterHolesSettings);

        RmgTemplate on = TemplateGenerator.Generate(new GeneratorSettings { EncounterHoles = true });
        Assert.Equal(true, on.GameRules?.EncounterHoles);
        EncounterHolesSettings? holes = FirstSpawnZone(on).EncounterHolesSettings;
        Assert.NotNull(holes);
        Assert.Equal(0.66, holes!.AffectedEncounters);
        Assert.Equal(0.66, holes.TwoHoleEncounters);
    }

    [Fact]
    public void Generate_WaterLevel_SetsBorderWaterWidthAndTypeFromTerrain()
    {
        Variant dry = SingleVariant(TemplateGenerator.Generate(new GeneratorSettings()));
        Assert.Equal(0, dry.Border?.WaterWidth);

        Variant wet = SingleVariant(TemplateGenerator.Generate(
            new GeneratorSettings { WaterLevel = WaterLevel.Medium, Terrain = TerrainTheme.Snow }));
        Assert.Equal(4, wet.Border?.WaterWidth);
        Assert.Equal("water snow", wet.Border?.WaterType);
    }

    [Fact]
    public void Generate_TerrainRoughnessAndLakes_ScaleZoneLayoutFills()
    {
        RmgTemplate flat = TemplateGenerator.Generate(
            new GeneratorSettings { TerrainRoughnessPercent = 0, LakeAmountPercent = 0 });
        Assert.All(flat.ZoneLayouts ?? [], layout =>
        {
            Assert.Equal(0.0, layout.ObstaclesFill);
            Assert.Equal(0.0, layout.LakesFill);
        });

        RmgTemplate normal = TemplateGenerator.Generate(new GeneratorSettings());
        ZoneLayout spawnLayout = Assert.Single(normal.ZoneLayouts ?? [],
            layout => layout.Name == "zone_layout_spawns");
        Assert.Equal(0.24, spawnLayout.ObstaclesFill);
    }

    [Fact]
    public void Presets_AllProduceValidTemplates()
    {
        Assert.NotEmpty(Presets.All);
        foreach (var preset in Presets.All)
        {
            SettingsFile sf = preset.Settings;
            string who = preset.Name;
            RmgTemplate template = TemplateGenerator.Generate(Presets.ToGeneratorSettings(sf));

            Assert.False(string.IsNullOrWhiteSpace(template.Name));
            Assert.Equal(sf.MapSize, template.SizeX);
            Assert.InRange(sf.PlayerCount, 2, 8);

            Variant variant = SingleVariant(template);
            List<Zone> zones = RequiredZones(variant);

            int spawnCount = zones.Count(z => z.Name.StartsWith("Spawn-", StringComparison.Ordinal));
            Assert.True(sf.PlayerCount == spawnCount, $"{who}: players {sf.PlayerCount} != spawns {spawnCount}");

            int expectedNeutral = sf.AdvancedMode
                ? sf.NeutralLowNoCastleCount + sf.NeutralLowCastleCount + sf.NeutralMediumNoCastleCount
                  + sf.NeutralMediumCastleCount + sf.NeutralHighNoCastleCount + sf.NeutralHighCastleCount
                : sf.NeutralZoneCount;
            int actualNeutral = zones.Count(z => z.Name.StartsWith("Neutral-", StringComparison.Ordinal));
            Assert.True(expectedNeutral == actualNeutral, $"{who}: neutrals {expectedNeutral} != {actualNeutral}");

            // Player zones must be seeded with the standard guarded mines.
            Assert.Contains(template.MandatoryContent ?? [],
                g => g.Name.StartsWith("mandatory_content_side_", StringComparison.Ordinal)
                     && (g.Content?.Count ?? 0) > 0);
        }
    }

    [Fact]
    public void Presets_MatchTheirDescribedFeatures()
    {
        foreach (var preset in Presets.All)
        {
            SettingsFile sf = preset.Settings;
            string who = preset.Name;
            RmgTemplate t = TemplateGenerator.Generate(Presets.ToGeneratorSettings(sf));
            List<Zone> zones = RequiredZones(SingleVariant(t));

            if (sf.SingleHeroMode)
            {
                Assert.True(t.GameMode == "SingleHero", $"{who}: gameMode");
                Assert.True(t.GameRules?.HeroHireBan == true, $"{who}: heroHireBan");
                Assert.True(t.GameRules?.HeroCountMax == 1, $"{who}: heroCountMax");
            }

            if (sf.Terrain != TerrainTheme.FactionBased)
            {
                Zone? biomeZone = zones.FirstOrDefault(z => z.ZoneBiome?.Type == "FromList" && (z.ZoneBiome.Args?.Count ?? 0) > 0);
                Assert.True(biomeZone != null, $"{who}: no forced biome");
                if (sf.Terrain == TerrainTheme.Random)
                    Assert.True(biomeZone!.ZoneBiome!.Args!.Count == 7, $"{who}: random biome count");
                else
                    Assert.Contains(sf.Terrain.ToString(), biomeZone!.ZoneBiome!.Args!);
            }

            int expWater = sf.WaterLevel switch { WaterLevel.Small => 3, WaterLevel.Medium => 4, WaterLevel.Large => 6, _ => 0 };
            Assert.True((SingleVariant(t).Border?.WaterWidth ?? 0) == expWater, $"{who}: waterWidth");

            if (sf.CityHold || sf.VictoryCondition == "win_condition_5")
                Assert.True(t.GameRules?.WinConditions?.CityHold == true, $"{who}: cityHold");

            if (sf.Tournament || sf.VictoryCondition == "win_condition_6")
                Assert.True(t.GameRules?.WinConditions?.Tournament == true, $"{who}: tournament");

            if (sf.Topology == MapTopology.HubAndSpoke)
                Assert.Contains(zones, z => z.Name == "Hub");

            Assert.True((t.GameRules?.EncounterHoles ?? false) == sf.EncounterHoles, $"{who}: encounterHoles");

            double expDip = Math.Round(Math.Clamp(sf.NeutralDiplomacyModifier, -1.0, 1.0), 2);
            Zone? dipZone = zones.FirstOrDefault(z => z.DiplomacyModifier.HasValue);
            Assert.True(dipZone != null && Math.Abs(dipZone.DiplomacyModifier!.Value - expDip) < 0.001, $"{who}: diplomacy {dipZone?.DiplomacyModifier} != {expDip}");

            if (sf.PlayerStartsWithCastles)
                Assert.Contains(zones.Where(z => z.Name.StartsWith("Spawn-", StringComparison.Ordinal))
                    .SelectMany(z => z.MainObjects ?? []), o => !string.IsNullOrEmpty(o.Owner));
        }
    }

    [Fact]
    public void Presets_HaveUniqueNamesAndTemplateNames()
    {
        Assert.Equal(Presets.All.Length, Presets.All.Select(p => p.Name).Distinct().Count());
        Assert.Equal(Presets.All.Length, Presets.All.Select(p => p.Settings.TemplateName).Distinct().Count());
    }

    [Fact]
    public void Generate_WithEnvironmentOptions_SerializesExpectedJsonTokens()
    {
        var settings = new GeneratorSettings
        {
            Terrain = TerrainTheme.Snow,
            MonsterAggression = MonsterAggression.Aggressive,
            WaterLevel = WaterLevel.Large,
            EncounterHoles = true,
            NeutralDiplomacyModifier = 0.3,
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);
        string json = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = false });

        Assert.Contains("\"waterWidth\":6", json);
        Assert.Contains("water snow", json);
        Assert.Contains("\"type\":\"FromList\"", json);
        Assert.Contains("encounterHolesSettings", json);
        Assert.Contains("\"encounterHoles\":true", json);
        Assert.Contains("\"diplomacyModifier\":0.3", json);
    }

    private static Zone FirstSpawnZone(RmgTemplate template) =>
        RequiredZones(SingleVariant(template))
            .First(zone => zone.Name.StartsWith("Spawn-", StringComparison.Ordinal));

    private static Variant SingleVariant(RmgTemplate template) =>
        Assert.Single(template.Variants ?? []);

    private static List<Zone> RequiredZones(Variant variant)
    {
        Assert.NotNull(variant.Zones);
        return variant.Zones;
    }

    private static List<Connection> RequiredConnections(Variant variant)
    {
        Assert.NotNull(variant.Connections);
        return variant.Connections;
    }

    private static List<string> RoadConnectionNames(Zone zone)
    {
        var names = new List<string>();
        foreach (Road road in zone.Roads ?? [])
        {
            AddConnectionName(road.From);
            AddConnectionName(road.To);
        }

        return names;

        void AddConnectionName(RoadEndpoint? endpoint)
        {
            if (endpoint?.Type == "Connection" && endpoint.Args is { Count: > 0 })
                names.Add(endpoint.Args[0]);
        }
    }

    private static List<List<string>> RingNeutralGapsBetweenPlayers(List<string> sequence)
    {
        var playerPositions = sequence
            .Select((zoneName, index) => (zoneName, index))
            .Where(item => item.zoneName.StartsWith("Spawn-", StringComparison.Ordinal))
            .ToList();

        var gaps = new List<List<string>>();
        for (int i = 0; i < playerPositions.Count; i++)
        {
            int start = playerPositions[i].index;
            int end = playerPositions[(i + 1) % playerPositions.Count].index;
            var gap = new List<string>();

            for (int cursor = (start + 1) % sequence.Count; cursor != end; cursor = (cursor + 1) % sequence.Count)
                gap.Add(sequence[cursor]);

            gaps.Add(gap);
        }

        return gaps;
    }

    private static Dictionary<string, int> RingPlayerDistanceTotals(List<string> sequence)
    {
        var playerPositions = sequence
            .Select((zoneName, index) => (zoneName, index))
            .Where(item => item.zoneName.StartsWith("Spawn-", StringComparison.Ordinal))
            .ToList();

        return playerPositions.ToDictionary(
            player => player.zoneName,
            player => playerPositions
                .Where(other => other.zoneName != player.zoneName)
                .Sum(other =>
                {
                    int clockwise = Math.Abs(player.index - other.index);
                    return Math.Min(clockwise, sequence.Count - clockwise);
                }),
            StringComparer.Ordinal);
    }

    private static int ShortestNeutralIntermediates(Variant variant, string from, string to)
    {
        var graph = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (Connection connection in RequiredConnections(variant)
            .Where(connection => connection.ConnectionType is "Direct" or "Portal"))
        {
            if (!graph.TryGetValue(connection.From, out var fromEdges))
                graph[connection.From] = fromEdges = [];
            if (!graph.TryGetValue(connection.To, out var toEdges))
                graph[connection.To] = toEdges = [];

            fromEdges.Add(connection.To);
            toEdges.Add(connection.From);
        }

        var queue = new Queue<(string Zone, int Neutrals)>();
        var best = new Dictionary<string, int>(StringComparer.Ordinal) { [from] = 0 };
        queue.Enqueue((from, 0));

        while (queue.Count > 0)
        {
            var (zone, neutrals) = queue.Dequeue();
            if (!graph.TryGetValue(zone, out var neighbors)) continue;

            foreach (string neighbor in neighbors)
            {
                int nextNeutrals = neutrals + (neighbor != to && !neighbor.StartsWith("Spawn-", StringComparison.Ordinal) ? 1 : 0);
                if (best.TryGetValue(neighbor, out int existing) && existing <= nextNeutrals)
                    continue;

                best[neighbor] = nextNeutrals;
                queue.Enqueue((neighbor, nextNeutrals));
            }
        }

        return best.TryGetValue(to, out int shortest) ? shortest : int.MaxValue;
    }

    // ── Hero bans (globalBans.heroes) ─────────────────────────────────────────

    [Fact]
    public void Generate_EmitsBannedHeroesIntoGlobalBans()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "Hero Ban Test",
            GameMode     = "Classic",
            MapSize      = 160,
            Topology     = MapTopology.Default,
            BannedItems  = "pole_star_artifact",
            BannedMagics = "neutral_magic_town_portal",
            BannedHeroes = "demon_hero_3\nnature_hero_17\nhuman_hero_8",
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template.GlobalBans);
        Assert.NotNull(template.GlobalBans!.Heroes);
        Assert.Equal(
            new[] { "demon_hero_3", "nature_hero_17", "human_hero_8" },
            template.GlobalBans.Heroes!.ToArray());

        // The schema key must be exactly "heroes" — the engine reads globalBans.heroes.
        string json = JsonSerializer.Serialize(template);
        Assert.Contains("\"heroes\"", json);
    }

    [Fact]
    public void Generate_WithNoHeroBans_OmitsHeroesList()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "No Hero Ban",
            GameMode     = "Classic",
            MapSize      = 160,
            Topology     = MapTopology.Default,
            BannedItems  = "pole_star_artifact",
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template.GlobalBans);
        Assert.Null(template.GlobalBans!.Heroes);
    }

    [Fact]
    public void Generate_HighCustomHeroCount_IsPreserved()
    {
        // The hero-count UI now accepts free numeric entry beyond the old cap of 12.
        var settings = new GeneratorSettings
        {
            TemplateName = "Twenty Heroes",
            GameMode     = "Classic",
            MapSize      = 160,
            Topology     = MapTopology.Default,
            HeroSettings = new HeroSettings
            {
                HeroCountMin       = 20,
                HeroCountMax       = 20,
                HeroCountIncrement = 0,
            },
        };

        RmgTemplate template = TemplateGenerator.Generate(settings);

        Assert.NotNull(template.GameRules);
        Assert.Equal(20, template.GameRules!.HeroCountMax);
        Assert.Equal(20, template.GameRules.HeroCountMin); // min - increment (0) = 20
    }

    [Fact]
    public void BannableHeroes_AllFollowFactionHeroPattern()
    {
        Assert.NotEmpty(KnownValues.BannableHeroes);
        foreach (var hero in KnownValues.BannableHeroes)
        {
            var (_, faction) = KnownValues.DescribeHeroSid(hero.Id);
            Assert.Equal(hero.Category, faction);
            Assert.Contains(hero.Category, KnownValues.HeroFactions);
            Assert.Matches(@"^[a-z]+_hero_\d+$", hero.Id);
        }
    }
}
