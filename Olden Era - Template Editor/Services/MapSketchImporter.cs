using System;
using System.Collections.Generic;
using System.Linq;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>One detected zone blob: where it is, how big it is and what colour it was drawn in.</summary>
    public sealed record SketchBlob(int Label, double CentreX, double CentreY, int PixelCount, byte R, byte G, byte B);

    /// <summary>Everything a sketch import produced: the template, the canvas positions and any notes.</summary>
    public sealed record SketchImportResult(
        RmgTemplate Template,
        IReadOnlyDictionary<string, (double X, double Y)> Positions,
        IReadOnlyList<SketchBlob> Blobs,
        IReadOnlyList<string> Warnings);

    /// <summary>
    /// Turns a hand-drawn map sketch into a zone graph.
    /// <para>
    /// Sketching a layout in any paint program is far quicker than placing nodes one by one: draw a
    /// coloured blob per zone, let blobs that should be connected touch each other, and this importer
    /// reads that picture back as zones and connections. The colour picks the zone's role (see
    /// <see cref="RoleForColour"/>), the blob's area sets its relative size, and its centre becomes the
    /// node's position on the editor canvas.
    /// </para>
    /// <para>
    /// Connections come from <b>adjacency</b>, not from drawn lines: two zones are linked when their
    /// blobs touch (or nearly touch). Line detection is far more fragile than "these two areas share a
    /// border", and sharing a border is exactly how a map sketch reads to a human. When the sketch has
    /// separate islands, the nearest-neighbour pass links them so the map is never unreachable.
    /// </para>
    /// The analysis works on raw BGRA pixels so it can be unit-tested without any imaging stack.
    /// </summary>
    public static class MapSketchImporter
    {
        private static string L(string key, params object[] args) => Localization.LocalizationManager.T(key, args);
        /// <summary>Colour → zone role. Hue-based, so any shade of "green" reads as a player start.</summary>
        public static string RoleForColour(byte r, byte g, byte b)
        {
            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));
            int chroma = max - min;

            // Nearly grey: light = treasure (silver), dark = a plain side zone.
            if (chroma < 30) return max >= 140 ? "zone_layout_treasure_zone" : "zone_layout_sides";

            double hue = Hue(r, g, b);
            return hue switch
            {
                < 20 or >= 330 => "zone_layout_wincondition_zone", // red
                < 45 => "zone_layout_side_zone",                   // orange / brown
                < 70 => "zone_layout_supertreasure_zone",          // yellow / gold
                < 165 => "zone_layout_player_spawn",               // green
                < 200 => "zone_layout_center",                     // cyan
                < 260 => "zone_layout_start_zone",                 // blue
                _ => "zone_layout_treasures",                      // violet / magenta
            };
        }

        private static double Hue(byte r, byte g, byte b)
        {
            double rd = r / 255.0, gd = g / 255.0, bd = b / 255.0;
            double max = Math.Max(rd, Math.Max(gd, bd)), min = Math.Min(rd, Math.Min(gd, bd));
            double c = max - min;
            if (c <= 0) return 0;

            double h = max == rd ? ((gd - bd) / c) % 6
                     : max == gd ? (bd - rd) / c + 2
                                 : (rd - gd) / c + 4;
            h *= 60;
            return h < 0 ? h + 360 : h;
        }

        /// <summary>
        /// Analyses a BGRA image (as produced by WPF's <c>Bgra32</c> format).
        /// </summary>
        /// <param name="pixels">width × height × 4 bytes, B,G,R,A order.</param>
        /// <param name="minAreaPercent">Blobs smaller than this share of the image are noise, not zones.</param>
        /// <param name="linkRadius">How many pixels apart two blobs may be and still count as touching.</param>
        public static SketchImportResult Analyse(
            byte[] pixels, int width, int height, double minAreaPercent = 0.3, int linkRadius = 3)
        {
            if (width <= 0 || height <= 0 || pixels.LongLength / 4 < (long)width * height)
                throw new ArgumentException(L("S.Label.ImageBuffer"), nameof(pixels));

            int[] labels = LabelBlobs(pixels, width, height, out List<SketchBlob> blobs);

            int minPixels = Math.Max(8, (int)(width * height * minAreaPercent / 100.0));
            var kept = blobs.Where(b => b.PixelCount >= minPixels)
                            .OrderByDescending(b => b.PixelCount)
                            .ToList();

            var warnings = new List<string>();
            if (blobs.Count > kept.Count)
                warnings.Add(L("S.IM.Noise", blobs.Count - kept.Count, minAreaPercent));
            if (kept.Count == 0)
                throw new InvalidOperationException(L("S.IM.NoAreas"));
            if (kept.Count > KnownValues.SpawnPlayers.Length * 6)
                warnings.Add(L("S.IM.ManyZones", kept.Count));

            var keptLabels = new HashSet<int>(kept.Select(b => b.Label));
            var adjacency = FindAdjacency(labels, width, height, keptLabels, linkRadius);

            return BuildTemplate(kept, adjacency, width, height, warnings);
        }

        // ── Blob detection ───────────────────────────────────────────────────────────

        /// <summary>
        /// Flood-fills areas of similar colour into labelled blobs, skipping the background (very light,
        /// very dark or transparent pixels — paper, canvas and pencil lines).
        /// </summary>
        private static int[] LabelBlobs(byte[] pixels, int width, int height, out List<SketchBlob> blobs)
        {
            const int ColourTolerance = 48; // per-channel distance that still counts as "the same colour"

            var labels = new int[width * height];
            blobs = [];
            int next = 1;
            var queue = new Queue<int>();

            for (int start = 0; start < labels.Length; start++)
            {
                if (labels[start] != 0) continue;
                int p = start * 4;
                byte b0 = pixels[p], g0 = pixels[p + 1], r0 = pixels[p + 2], a0 = pixels[p + 3];
                if (IsBackground(r0, g0, b0, a0)) { labels[start] = -1; continue; }

                int label = next++;
                long sumX = 0, sumY = 0, count = 0;
                long sumR = 0, sumG = 0, sumB = 0;

                labels[start] = label;
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    int x = index % width, y = index / width;
                    int q = index * 4;
                    sumX += x; sumY += y; count++;
                    sumB += pixels[q]; sumG += pixels[q + 1]; sumR += pixels[q + 2];

                    foreach (int nb in Neighbours(index, x, y, width, height))
                    {
                        if (labels[nb] != 0) continue;
                        int n = nb * 4;
                        byte nbB = pixels[n], nbG = pixels[n + 1], nbR = pixels[n + 2], nbA = pixels[n + 3];
                        if (IsBackground(nbR, nbG, nbB, nbA)) { labels[nb] = -1; continue; }
                        if (Math.Abs(nbR - r0) > ColourTolerance ||
                            Math.Abs(nbG - g0) > ColourTolerance ||
                            Math.Abs(nbB - b0) > ColourTolerance) continue;

                        labels[nb] = label;
                        queue.Enqueue(nb);
                    }
                }

                blobs.Add(new SketchBlob(
                    label,
                    sumX / (double)count, sumY / (double)count,
                    (int)count,
                    (byte)(sumR / count), (byte)(sumG / count), (byte)(sumB / count)));
            }

            return labels;
        }

        private static bool IsBackground(byte r, byte g, byte b, byte a)
        {
            if (a < 32) return true;                       // transparent
            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));
            if (max >= 232 && max - min < 24) return true; // paper white
            if (max <= 40) return true;                    // ink black (outlines, grid)
            return false;
        }

        private static IEnumerable<int> Neighbours(int index, int x, int y, int width, int height)
        {
            if (x > 0) yield return index - 1;
            if (x < width - 1) yield return index + 1;
            if (y > 0) yield return index - width;
            if (y < height - 1) yield return index + width;
        }

        // ── Adjacency ────────────────────────────────────────────────────────────────

        /// <summary>Two blobs are connected when pixels of one sit within <paramref name="radius"/> of the other.</summary>
        internal static HashSet<(int, int)> FindAdjacency(
            int[] labels, int width, int height, HashSet<int> kept, int radius)
        {
            var pairs = new HashSet<(int, int)>();
            radius = Math.Clamp(radius, 1, Math.Max(width, height));

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int label = labels[y * width + x];
                if (label <= 0 || !kept.Contains(label)) continue;
                // Any neighbouring blob within the radius is also within reach of a boundary pixel.
                // Interior pixels add no pairs; on filled sketches this skips most neighbourhood scans.
                if (x > 0 && y > 0 && x + 1 < width && y + 1 < height
                    && labels[y * width + x - 1] == label && labels[y * width + x + 1] == label
                    && labels[(y - 1) * width + x] == label && labels[(y + 1) * width + x] == label) continue;

                for (int ny = Math.Max(0, y - radius); ny <= Math.Min(height - 1, y + radius); ny++)
                for (int nx = Math.Max(0, x - radius); nx <= Math.Min(width - 1, x + radius); nx++)
                {
                    int other = labels[ny * width + nx];
                    if (other <= label || !kept.Contains(other)) continue;
                    pairs.Add((label, other));
                }
            }

            return pairs;
        }

        // ── Template assembly ────────────────────────────────────────────────────────

        private static SketchImportResult BuildTemplate(
            List<SketchBlob> blobs, HashSet<(int, int)> adjacency, int width, int height, List<string> warnings)
        {
            var zonesByLabel = new Dictionary<int, Zone>();
            var positions = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
            var zones = new List<Zone>();
            double averageArea = blobs.Average(b => (double)b.PixelCount);

            int playerIndex = 0, otherIndex = 0;
            foreach (SketchBlob blob in blobs)
            {
                string layout = RoleForColour(blob.R, blob.G, blob.B);
                bool isSpawn = layout is "zone_layout_player_spawn" or "zone_layout_ai_spawn";

                string name = isSpawn && playerIndex < KnownValues.SpawnPlayers.Length
                    ? $"Spawn-{(char)('A' + playerIndex)}" : $"Zone-{++otherIndex}";
                Zone zone = SketchZone(name, layout, blob.PixelCount / averageArea);

                if (isSpawn)
                {
                    if (playerIndex < KnownValues.SpawnPlayers.Length)
                    {
                        zone.MainObjects =
                        [
                            new MainObject
                            {
                                Type = "Spawn",
                                Spawn = KnownValues.SpawnPlayers[playerIndex],
                                RemoveGuardIfHasOwner = true,
                                GuardChance = 1,
                                GuardValue = 5000,
                                GuardWeeklyIncrement = 0.10,
                                BuildingsConstructionSid = "default_buildings_construction",
                                Placement = "Uniform",
                                PlacementArgs = ["true", "0.7", "0"],
                            },
                        ];
                        playerIndex++;
                    }
                    else
                    {
                        warnings.Add(L("S.IM.ExcessStart", KnownValues.SpawnPlayers.Length, name));
                        zone.Layout = "zone_layout_sides";
                    }
                }

                zones.Add(zone);
                zonesByLabel[blob.Label] = zone;
                positions[zone.Name] = (blob.CentreX / width, blob.CentreY / height);
            }

            if (playerIndex == 0)
                warnings.Add(L("S.IM.NoStarts"));

            var connections = new List<Connection>();
            foreach ((int a, int b) in adjacency.OrderBy(p => p.Item1).ThenBy(p => p.Item2))
                AddLink(connections, zonesByLabel[a], zonesByLabel[b]);

            int islandLinks = ConnectIslands(blobs, zonesByLabel, connections, positions);
            if (islandLinks > 0)
                warnings.Add(L("S.IM.Islands", islandLinks));

            var template = new RmgTemplate
            {
                Name = "Sketch import",
                GameMode = "Classic",
                Description = "Imported from a hand-drawn sketch (structure only — content uses AuroraRMG defaults).",
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
                    },
                },
                Variants = [new Variant { Zones = zones, Connections = connections }],
                ZoneLayouts = [],
                MandatoryContent = [],
                ContentCountLimits = [],
                ContentPools = [],
                ContentLists = [],
            };

            TemplateGenerator.EnsureZoneLayoutsDefined(template);
            return new SketchImportResult(template, positions, blobs, warnings);
        }

        private static void AddLink(List<Connection> connections, Zone from, Zone to)
        {
            if (ReferenceEquals(from, to)) return;
            if (connections.Any(c =>
                    (c.From == from.Name && c.To == to.Name) ||
                    (c.From == to.Name && c.To == from.Name))) return;

            connections.Add(new Connection
            {
                Name = $"Sketch-{from.Name}-{to.Name}",
                From = from.Name,
                To = to.Name,
                ConnectionType = "Direct",
                GuardZone = from.Name,
                GuardEscape = false,
                SimTurnSquad = true,
                GuardValue = 20000,
                GuardWeeklyIncrement = 0.15,
            });
        }

        /// <summary>
        /// Links up areas the sketch left floating: each disconnected group is attached to the nearest
        /// zone of the main group, so the imported map is a single reachable world.
        /// Returns how many links were added.
        /// </summary>
        private static int ConnectIslands(
            List<SketchBlob> blobs,
            Dictionary<int, Zone> zonesByLabel,
            List<Connection> connections,
            Dictionary<string, (double X, double Y)> positions)
        {
            if (blobs.Count < 2) return 0;

            var parent = blobs.ToDictionary(b => b.Label, b => b.Label);
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[b] = a; }

            var labelByName = zonesByLabel.ToDictionary(kv => kv.Value.Name, kv => kv.Key, StringComparer.Ordinal);
            foreach (Connection c in connections)
                if (labelByName.TryGetValue(c.From, out int a) && labelByName.TryGetValue(c.To, out int b))
                    Union(a, b);

            int added = 0;
            while (true)
            {
                var groups = blobs.GroupBy(b => Find(b.Label)).ToList();
                if (groups.Count < 2) break;

                var main = groups.OrderByDescending(g => g.Sum(b => (long)b.PixelCount)).First().ToList();
                var island = groups.First(g => g.Key != Find(main[0].Label)).ToList();

                SketchBlob bestFrom = island[0], bestTo = main[0];
                double best = double.MaxValue;
                foreach (SketchBlob i in island)
                foreach (SketchBlob m in main)
                {
                    double d = (i.CentreX - m.CentreX) * (i.CentreX - m.CentreX)
                             + (i.CentreY - m.CentreY) * (i.CentreY - m.CentreY);
                    if (d >= best) continue;
                    best = d; bestFrom = i; bestTo = m;
                }

                AddLink(connections, zonesByLabel[bestFrom.Label], zonesByLabel[bestTo.Label]);
                Union(bestFrom.Label, bestTo.Label);
                added++;
            }

            _ = positions;
            return added;
        }

        /// <summary>A zone with the same proven defaults a hand-added zone gets, sized by its blob.</summary>
        private static Zone SketchZone(string name, string layout, double relativeArea) => new()
        {
            Name = name,
            Size = Math.Round(Math.Clamp(Math.Sqrt(Math.Max(0.01, relativeArea)), 0.4, 2.0), 2),
            Layout = layout,
            GuardCutoffValue = 2000,
            GuardRandomization = 0.05,
            GuardMultiplier = 1.0,
            GuardWeeklyIncrement = 0.20,
            GuardReactionDistribution = [60, 20, 10, 10, 2, 0],
            DiplomacyModifier = -0.5,
            GuardedContentPool = ["classic_template_pool_random_t2_item"],
            UnguardedContentPool = ["classic_template_pool_random_unguarded_t2_item"],
            ResourcesContentPool = ["content_pool_general_resources_start_zone_poor"],
            MandatoryContent = [],
            ContentCountLimits = [],
            GuardedContentValue = 150000,
            GuardedContentValuePerArea = 1000,
            UnguardedContentValue = 35000,
            UnguardedContentValuePerArea = 1000,
            ResourcesValue = 3000,
            ResourcesValuePerArea = 100,
            MainObjects = [],
            ZoneBiome = new BiomeSelector { Type = "MatchZone", Args = [] },
            ContentBiome = new BiomeSelector { Type = "MatchZone", Args = [] },
            MetaObjectsBiome = new BiomeSelector { Type = "MatchZone", Args = [] },
            CrossroadsPosition = 0,
        };
    }
}
