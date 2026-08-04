using System.Collections.Generic;
using System.Linq;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>
    /// Pure validation of a zone graph (zones + connections) for the visual editor.
    /// Surfaces the issues that break a template in-game: missing names, duplicate
    /// names, dangling connection endpoints, self-loops, unreachable zones, a broken
    /// player-start chain, dangling road endpoints and circular biome selectors.
    /// </summary>
    public static class ZoneGraphValidator
    {
        private static string L(string key, params object[] args) => Localization.LocalizationManager.T(key, args);

        public static List<string> Validate(IReadOnlyList<Zone> zones, IReadOnlyList<Connection> connections)
        {
            var issues = new List<string>();
            var names = new HashSet<string>(System.StringComparer.Ordinal);

            foreach (var z in zones)
            {
                if (string.IsNullOrWhiteSpace(z.Name)) issues.Add(L("S.V.NoName"));
                else if (!names.Add(z.Name)) issues.Add(L("S.V.DupName", z.Name));
            }

            var connNames = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var c in connections)
            {
                if (!names.Contains(c.From)) issues.Add(L("S.V.Dangling", c.From));
                if (!names.Contains(c.To))   issues.Add(L("S.V.Dangling", c.To));
                if (string.Equals(c.From, c.To, System.StringComparison.Ordinal) && !string.IsNullOrEmpty(c.From))
                    issues.Add(L("S.V.SelfLoop", c.From));
                if (!string.IsNullOrWhiteSpace(c.Name) && !connNames.Add(c.Name))
                    issues.Add(L("S.V.DupConnName", c.Name));
            }

            if (zones.Count > 1)
            {
                var connected = new HashSet<string>(System.StringComparer.Ordinal);
                foreach (var c in connections) { connected.Add(c.From); connected.Add(c.To); }
                foreach (var z in zones)
                    if (!string.IsNullOrWhiteSpace(z.Name) && !connected.Contains(z.Name))
                        issues.Add(L("S.V.Isolated", z.Name));

                issues.AddRange(ValidateReachability(zones, connections, connected));
            }

            issues.AddRange(ValidateSpawns(zones));
            issues.AddRange(ValidateRoads(zones, connNames));
            issues.AddRange(ValidateBiomes(zones, names));

            return issues;
        }

        /// <summary>
        /// Reports zones that have connections but sit in a separate component: a player stranded in
        /// their own island can never meet anyone, which the per-zone "isolated" check cannot see.
        /// Zones with no connections at all are already reported by <c>S.V.Isolated</c>, so they are
        /// excluded here to avoid a duplicate complaint about the same zone.
        /// </summary>
        private static IEnumerable<string> ValidateReachability(
            IReadOnlyList<Zone> zones, IReadOnlyList<Connection> connections, HashSet<string> connectedZones)
        {
            var adjacency = new Dictionary<string, List<string>>(System.StringComparer.Ordinal);
            foreach (var z in zones)
                if (!string.IsNullOrWhiteSpace(z.Name)) adjacency[z.Name] = [];

            foreach (var c in connections)
            {
                if (!adjacency.TryGetValue(c.From, out var a) || !adjacency.TryGetValue(c.To, out var b)) continue;
                a.Add(c.To);
                b.Add(c.From);
            }

            // Start from a zone that actually has connections, so a single stray zone does not make
            // the whole (otherwise fine) map look unreachable.
            string? start = zones.Select(z => z.Name).FirstOrDefault(n => connectedZones.Contains(n));
            if (start is null) yield break;

            var seen = new HashSet<string>(System.StringComparer.Ordinal) { start };
            var queue = new Queue<string>();
            queue.Enqueue(start);
            while (queue.Count > 0)
                foreach (string nb in adjacency[queue.Dequeue()])
                    if (seen.Add(nb)) queue.Enqueue(nb);

            // One message for the whole split (not one per zone) so a large disconnected map does not
            // bury the rest of the report.
            var unreachable = zones
                .Select(z => z.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n) && connectedZones.Contains(n) && !seen.Contains(n))
                .ToList();

            if (unreachable.Count > 0)
                yield return L("S.V.Unreachable",
                    string.Join(", ", unreachable.Take(3).Select(n => $"\"{n}\"")) + (unreachable.Count > 3 ? $" (+{unreachable.Count - 3})" : ""),
                    start);
        }

        /// <summary>
        /// Validates the player-start chain: at least one <c>Spawn</c> must exist, no two zones may
        /// claim the same player, and the players present must run consecutively from Player1 (the
        /// engine assigns sides by index, so a hole leaves a side with no starting town).
        /// </summary>
        public static List<string> ValidateSpawns(IReadOnlyList<Zone> zones)
        {
            var issues = new List<string>();
            var present = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var z in zones)
            foreach (var mo in z.MainObjects ?? [])
            {
                if (string.IsNullOrEmpty(mo.Spawn)) continue;
                if (System.Array.IndexOf(KnownValues.SpawnPlayers, mo.Spawn) < 0) continue;
                if (!present.Add(mo.Spawn!)) issues.Add(L("S.V.DupSpawn", mo.Spawn!));
            }

            if (present.Count == 0)
            {
                issues.Add(L("S.V.NoSpawn"));
                return issues;
            }

            int maxIndex = present.Max(p => System.Array.IndexOf(KnownValues.SpawnPlayers, p));
            for (int i = 0; i <= maxIndex; i++)
                if (!present.Contains(KnownValues.SpawnPlayers[i]))
                    issues.Add(L("S.V.SpawnGap", KnownValues.SpawnPlayers[i]));

            return issues;
        }

        /// <summary>
        /// Roads are drawn between anchors inside a zone: a <c>Connection</c> endpoint must name an
        /// existing connection and a <c>MainObject</c> endpoint must index an object the zone actually
        /// has, otherwise the engine's road builder fails while generating the map.
        /// </summary>
        private static IEnumerable<string> ValidateRoads(IReadOnlyList<Zone> zones, HashSet<string> connectionNames)
        {
            foreach (var z in zones)
            foreach (var road in z.Roads ?? [])
            foreach (var endpoint in new[] { road.From, road.To })
            {
                if (endpoint is null) continue;
                string? arg = endpoint.Args is { Count: > 0 } ? endpoint.Args[0] : null;

                if (string.Equals(endpoint.Type, "Connection", System.StringComparison.Ordinal)
                    && (arg is null || !connectionNames.Contains(arg)))
                    yield return L("S.V.RoadConn", z.Name, arg ?? "");

                if (string.Equals(endpoint.Type, "MainObject", System.StringComparison.Ordinal)
                    && int.TryParse(arg, out int index)
                    && (index < 0 || index >= (z.MainObjects?.Count ?? 0)))
                    yield return L("S.V.RoadObj", z.Name, index, z.MainObjects?.Count ?? 0);
            }
        }

        /// <summary>
        /// A <c>MatchZone</c> biome copies another zone's terrain. Pointing a zone's own
        /// <c>zoneBiome</c> at itself is circular (never resolves), and pointing any selector at a
        /// zone that does not exist leaves the terrain undefined.
        /// </summary>
        private static IEnumerable<string> ValidateBiomes(IReadOnlyList<Zone> zones, HashSet<string> zoneNames)
        {
            foreach (var z in zones)
            {
                if (string.Equals(z.ZoneBiome?.Type, "MatchZone", System.StringComparison.Ordinal)
                    && z.ZoneBiome!.Args is { Count: > 0 } selfArgs
                    && string.Equals(selfArgs[0], z.Name, System.StringComparison.Ordinal))
                    yield return L("S.V.BiomeSelf", z.Name);

                foreach (var selector in new[] { z.ZoneBiome, z.ContentBiome, z.MetaObjectsBiome })
                {
                    if (!string.Equals(selector?.Type, "MatchZone", System.StringComparison.Ordinal)) continue;
                    if (selector!.Args is not { Count: > 0 } args) continue;
                    if (!zoneNames.Contains(args[0])) yield return L("S.V.BiomeZone", z.Name, args[0]);
                }
            }
        }
    }
}
