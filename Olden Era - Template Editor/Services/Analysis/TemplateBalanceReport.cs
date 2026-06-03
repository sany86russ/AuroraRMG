using System;
using System.Collections.Generic;
using System.Linq;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.Analysis
{
    /// <summary>How serious a balance finding is (drives colour/ordering in the UI).</summary>
    public enum BalanceSeverity { Good, Info, Warning }

    /// <summary>
    /// A single human-readable balance observation. Carries a localization <see cref="Key"/> + format
    /// <see cref="Args"/> rather than a finished string, so the report stays language-independent and
    /// unit-testable; the UI resolves it via <c>L.Get(Key, Args)</c>.
    /// </summary>
    public sealed record BalanceFinding(string Key, BalanceSeverity Severity, object[] Args);

    /// <summary>Per-player balance metrics, all derived from the generated zone graph.</summary>
    public sealed record PlayerBalance(
        int PlayerNumber,
        string SpawnId,
        string ZoneName,
        int StartWealth,
        double ExpansionValue,
        int NearestOpponentHops,
        int? CastleHops);

    /// <summary>The result of <see cref="TemplateBalanceReport.Analyze"/>.</summary>
    public sealed class BalanceReport
    {
        /// <summary>False when the map has fewer than two player spawns (nothing to compare).</summary>
        public bool Applicable { get; init; }
        /// <summary>Fairness/symmetry score 0–100 (higher = the players' starts are more equal).</summary>
        public int Score { get; init; }
        public IReadOnlyList<PlayerBalance> Players { get; init; } = [];
        public IReadOnlyList<BalanceFinding> Findings { get; init; } = [];
    }

    /// <summary>
    /// Pure, local analysis of a generated <see cref="RmgTemplate"/>'s fairness. Builds the zone graph
    /// from the variant's connections, measures each player's starting wealth, nearby expansion room,
    /// distance to the nearest opponent and access to neutral castles, then scores how EQUAL those are
    /// across players (the format has no symmetry field — symmetry is emergent from topology/orientation,
    /// so we measure it rather than enforce it). No I/O, no network — usable from any UI surface.
    /// </summary>
    public static class TemplateBalanceReport
    {
        /// <summary>Analyses the first variant of <paramref name="template"/> and returns a balance report.</summary>
        public static BalanceReport Analyze(RmgTemplate? template)
        {
            Variant? variant = template?.Variants?.FirstOrDefault();
            List<Zone> zones = variant?.Zones ?? [];
            List<Connection> connections = variant?.Connections ?? [];

            // Index zones + build an undirected adjacency over valid endpoints.
            var zoneByName = new Dictionary<string, Zone>(StringComparer.Ordinal);
            foreach (Zone z in zones)
                if (!string.IsNullOrWhiteSpace(z.Name)) zoneByName[z.Name] = z;

            var adj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string name in zoneByName.Keys) adj[name] = [];
            foreach (Connection c in connections)
            {
                if (string.IsNullOrEmpty(c.From) || string.IsNullOrEmpty(c.To) || c.From == c.To) continue;
                if (!adj.ContainsKey(c.From) || !adj.ContainsKey(c.To)) continue;
                adj[c.From].Add(c.To);
                adj[c.To].Add(c.From);
            }

            // Player spawn zones (a zone holding a MainObject of Type "Spawn").
            var playerZones = new List<(string SpawnId, string ZoneName, Zone Zone)>();
            foreach (Zone z in zones)
            {
                MainObject? spawn = z.MainObjects?.FirstOrDefault(o => string.Equals(o.Type, "Spawn", StringComparison.Ordinal));
                if (spawn != null && !string.IsNullOrWhiteSpace(z.Name))
                    playerZones.Add((spawn.Spawn ?? z.Name, z.Name, z));
            }

            if (playerZones.Count < 2)
                return new BalanceReport { Applicable = false, Score = 100, Players = [], Findings = [] };

            var playerZoneNames = new HashSet<string>(playerZones.Select(p => p.ZoneName), StringComparer.Ordinal);

            // Neutral castle zones: a "City" object in a zone that is NOT a player spawn.
            var castleZones = new HashSet<string>(StringComparer.Ordinal);
            foreach (Zone z in zones)
            {
                if (string.IsNullOrWhiteSpace(z.Name) || playerZoneNames.Contains(z.Name)) continue;
                if (z.MainObjects?.Any(o => string.Equals(o.Type, "City", StringComparison.Ordinal)) == true)
                    castleZones.Add(z.Name);
            }

            // BFS hop distances from every player zone.
            var dist = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
            foreach (var p in playerZones) dist[p.ZoneName] = Bfs(p.ZoneName, adj);

            // Per-player metrics.
            var players = new List<PlayerBalance>();
            foreach (var p in playerZones)
            {
                Dictionary<string, int> d = dist[p.ZoneName];
                int startWealth = ZoneValue(p.Zone);

                double expansion = 0;
                foreach (Zone z in zones)
                {
                    if (string.IsNullOrWhiteSpace(z.Name) || playerZoneNames.Contains(z.Name)) continue;
                    if (d.TryGetValue(z.Name, out int hops) && hops >= 1)
                        expansion += ZoneValue(z) / (double)hops; // closer neutral wealth counts more
                }

                int nearestOpp = int.MaxValue;
                foreach (var q in playerZones)
                {
                    if (q.ZoneName == p.ZoneName) continue;
                    if (d.TryGetValue(q.ZoneName, out int hops)) nearestOpp = Math.Min(nearestOpp, hops);
                }
                if (nearestOpp == int.MaxValue) nearestOpp = 0;

                int? castleHops = null;
                foreach (string castle in castleZones)
                    if (d.TryGetValue(castle, out int hops))
                        castleHops = castleHops is null ? hops : Math.Min(castleHops.Value, hops);

                players.Add(new PlayerBalance(
                    PlayerNumber(p.SpawnId, players.Count + 1), p.SpawnId, p.ZoneName,
                    startWealth, Math.Round(expansion, 1), nearestOpp, castleHops));
            }

            int score = Score(players);
            List<BalanceFinding> findings = BuildFindings(players, playerZones, dist, castleZones.Count > 0, score);

            return new BalanceReport { Applicable = true, Score = score, Players = players, Findings = findings };
        }

        // ── Scoring ───────────────────────────────────────────────────────────────

        private static int Score(IReadOnlyList<PlayerBalance> players)
        {
            double wealthFair    = 1 - RelSpread(players.Select(p => (double)p.StartWealth));
            double expansionFair = 1 - RelSpread(players.Select(p => p.ExpansionValue));
            double proximityFair = 1 - RelSpread(players.Select(p => (double)p.NearestOpponentHops));
            double combined = 0.45 * wealthFair + 0.35 * expansionFair + 0.20 * proximityFair;
            return (int)Math.Round(100 * Math.Clamp(combined, 0, 1));
        }

        private static List<BalanceFinding> BuildFindings(
            IReadOnlyList<PlayerBalance> players,
            IReadOnlyList<(string SpawnId, string ZoneName, Zone Zone)> playerZones,
            IReadOnlyDictionary<string, Dictionary<string, int>> dist,
            bool hasCastles, int score)
        {
            var findings = new List<BalanceFinding>();

            // Uneven starting wealth → name the poorest player and by how much.
            var wealths = players.Select(p => p.StartWealth).ToList();
            if (wealths.Max() > 0)
            {
                double spread = (wealths.Max() - (double)wealths.Min()) / wealths.Max();
                if (spread >= 0.12)
                {
                    PlayerBalance poorest = players.OrderBy(p => p.StartWealth).First();
                    int pct = (int)Math.Round(spread * 100);
                    findings.Add(new BalanceFinding("S.Bal.Find.PoorStart", BalanceSeverity.Warning, [poorest.PlayerNumber, pct]));
                }
            }

            // Uneven castle access: some players can't reach a neutral castle while others can, or the
            // distance varies wildly.
            if (hasCastles)
            {
                bool someNone = players.Any(p => p.CastleHops is null);
                bool someHave = players.Any(p => p.CastleHops is not null);
                var hopVals = players.Where(p => p.CastleHops is not null).Select(p => (double)p.CastleHops!.Value).ToList();
                bool bigSpread = hopVals.Count >= 2 && RelSpread(hopVals) >= 0.5;
                if ((someNone && someHave) || bigSpread)
                    findings.Add(new BalanceFinding("S.Bal.Find.CastleUneven", BalanceSeverity.Warning, []));
            }

            // For 3+ players: flag one pair that starts unusually close compared to the average.
            if (players.Count > 2)
            {
                int closestA = -1, closestB = -1, closestHops = int.MaxValue;
                var pairHops = new List<int>();
                for (int i = 0; i < playerZones.Count; i++)
                for (int j = i + 1; j < playerZones.Count; j++)
                {
                    if (!dist[playerZones[i].ZoneName].TryGetValue(playerZones[j].ZoneName, out int h)) continue;
                    pairHops.Add(h);
                    if (h < closestHops) { closestHops = h; closestA = i; closestB = j; }
                }
                if (closestA >= 0 && pairHops.Count > 0)
                {
                    double avg = pairHops.Average();
                    if (closestHops <= 2 && avg > 0 && closestHops < avg * 0.6)
                        findings.Add(new BalanceFinding("S.Bal.Find.TooClose", BalanceSeverity.Info,
                            [players[closestA].PlayerNumber, players[closestB].PlayerNumber]));
                }
            }

            // Nothing wrong + a high score → say so.
            if (score >= 85 && !findings.Any(f => f.Severity == BalanceSeverity.Warning))
                findings.Add(new BalanceFinding("S.Bal.Find.WellBalanced", BalanceSeverity.Good, []));

            return findings;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────

        private static int ZoneValue(Zone z) =>
            (z.GuardedContentValue ?? 0) + (z.UnguardedContentValue ?? 0) + (z.ResourcesValue ?? 0);

        private static Dictionary<string, int> Bfs(string start, IReadOnlyDictionary<string, List<string>> adj)
        {
            var d = new Dictionary<string, int>(StringComparer.Ordinal) { [start] = 0 };
            var queue = new Queue<string>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                string cur = queue.Dequeue();
                int dcur = d[cur];
                if (!adj.TryGetValue(cur, out List<string>? nbrs)) continue;
                foreach (string n in nbrs)
                    if (!d.ContainsKey(n)) { d[n] = dcur + 1; queue.Enqueue(n); }
            }
            return d;
        }

        /// <summary>Spread of a set in [0,1]: 0 = all equal, →1 = the smallest is far below the largest.</summary>
        private static double RelSpread(IEnumerable<double> values)
        {
            List<double> list = values.ToList();
            if (list.Count == 0) return 0;
            double max = list.Max();
            if (max <= 0) return 0;
            return Math.Clamp((max - list.Min()) / max, 0, 1);
        }

        /// <summary>"Player3" → 3; falls back to the player's positional index.</summary>
        private static int PlayerNumber(string spawnId, int fallback)
        {
            string digits = new(spawnId.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out int n) && n > 0 ? n : fallback;
        }
    }
}
