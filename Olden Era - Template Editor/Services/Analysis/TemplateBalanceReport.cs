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
        long StartWealth,
        double ExpansionValue,
        int NearestOpponentHops,
        int? CastleHops)
    {
        public string AllianceKey { get; init; } = "";
        /// <summary>Sum of connection guardValue on the cheapest path, not a forecast of army strength.</summary>
        public long? ClosestCastleGuardBudget { get; init; }
        /// <summary>Minimum zone hops to nonplayer guarded-content budgets; no native treasure-class inference.</summary>
        public int? TreasureHops { get; init; }
        /// <summary>Cheapest sum of configured connection guards to any guarded-content target. This can be a different target/path from TreasureHops.</summary>
        public long? ClosestTreasureGuardBudget { get; init; }
        public int StartingCities { get; init; }
        public int StartingResourceValue { get; init; }
        public long StartingAreaCoefficient { get; init; }
        public double ExpansionAreaCoefficient { get; init; }
    }

    public sealed record TeamBalance(string AllianceKey, IReadOnlyList<int> Players,
        long StartingWealth, long StartingResources, int StartingCities,
        double AccessibleExpansionValue, int NearestOpponentHops)
    {
        public long StartingAreaCoefficient { get; init; }
        public double AccessibleExpansionAreaCoefficient { get; init; }
    }

    /// <summary>Configured endpoint flags only. Their presence does not establish one-way hero movement.</summary>
    public sealed record PortalBalance(string Name, string From, string To, bool? FromEnabled, bool? ToEnabled, long GuardBudget);

    /// <summary>The result of <see cref="TemplateBalanceReport.Analyze"/>.</summary>
    public sealed class BalanceReport
    {
        /// <summary>False when the map has fewer than two player spawns (nothing to compare).</summary>
        public bool Applicable { get; init; }
        /// <summary>Fairness/symmetry score 0–100 (higher = the players' starts are more equal).</summary>
        public int Score { get; init; }
        /// <summary>Compares opposing alliance totals rather than treating allied slots as opponents.</summary>
        public int TeamScore { get; init; }
        /// <summary>False whenever area coefficients are used: the engine's eligible area and full totals are unknown.</summary>
        public bool EconomyComparisonAvailable { get; init; } = true;
        public IReadOnlyList<PlayerBalance> Players { get; init; } = [];
        public IReadOnlyList<TeamBalance> Teams { get; init; } = [];
        public IReadOnlyList<PortalBalance> Portals { get; init; } = [];
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
            List<Zone> zones = (variant?.Zones ?? []).OfType<Zone>().ToList();
            List<Connection> connections = (variant?.Connections ?? []).OfType<Connection>().ToList();
            var portals = connections.Where(c => c.ConnectionType == "Portal")
                .Select(c => new PortalBalance(c.Name ?? "", c.From, c.To, c.PortalFromEnabled, c.PortalToEnabled, c.GuardValue ?? 0)).ToArray();
            bool hasAreaCoefficients = zones.Any(z => ZoneAreaCoefficient(z) > 0);
            bool areaOnly = hasAreaCoefficients && zones.All(z => ZoneValue(z) == 0);

            // Index zones + build an undirected adjacency over valid endpoints.
            var zoneByName = new Dictionary<string, Zone>(StringComparer.Ordinal);
            foreach (Zone z in zones)
                if (!string.IsNullOrWhiteSpace(z.Name)) zoneByName[z.Name] = z;

            var adj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string name in zoneByName.Keys) adj[name] = [];
            var travelConnections = connections.Where(c => ConnectionRules.AllowsTravel(c)
                && !string.IsNullOrEmpty(c.From) && !string.IsNullOrEmpty(c.To) && c.From != c.To
                && adj.ContainsKey(c.From) && adj.ContainsKey(c.To)).ToArray();
            foreach (Connection c in travelConnections)
            {
                if (!ConnectionRules.AllowsTravel(c)) continue;
                if (string.IsNullOrEmpty(c.From) || string.IsNullOrEmpty(c.To) || c.From == c.To) continue;
                if (!adj.ContainsKey(c.From) || !adj.ContainsKey(c.To)) continue;
                adj[c.From].Add(c.To);
                adj[c.To].Add(c.From);
            }

            // Player spawn zones (a zone holding a MainObject of Type "Spawn").
            var playerZones = new List<(string SpawnId, string ZoneName, Zone Zone)>();
            foreach (Zone z in zones)
            {
                MainObject? spawn = z.MainObjects?.FirstOrDefault(o => o is not null
                    && string.Equals(o.Type, "Spawn", StringComparison.Ordinal)
                    && Array.IndexOf(KnownValues.SpawnPlayers, o.Spawn ?? "") >= 0);
                if (spawn != null && !string.IsNullOrWhiteSpace(z.Name))
                    playerZones.Add((spawn.Spawn ?? z.Name, z.Name, z));
            }

            if (playerZones.Count < 2)
                return new BalanceReport { Applicable = false, Score = 100, EconomyComparisonAvailable = !hasAreaCoefficients, Players = [], Portals = portals, Findings = [] };

            var playerZoneNames = new HashSet<string>(playerZones.Select(p => p.ZoneName), StringComparer.Ordinal);
            var assignments = (template?.SpawnInfos ?? []).Where(s => s is not null)
                .GroupBy(s => s.Spawn, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            string TeamFor(string spawn) => assignments.TryGetValue(spawn, out var assignment) && assignment.AllianceIndex is > 0
                ? $"team{assignment.AllianceIndex}" : spawn;
            var zoneValues = zoneByName.ToDictionary(kv => kv.Key, kv => ZoneValue(kv.Value), StringComparer.Ordinal);

            // Neutral castle zones: a "City" object in a zone that is NOT a player spawn.
            var castleZones = new HashSet<string>(StringComparer.Ordinal);
            foreach (Zone z in zones)
            {
                if (string.IsNullOrWhiteSpace(z.Name) || playerZoneNames.Contains(z.Name)) continue;
                if (z.MainObjects?.Any(o => o is not null && string.Equals(o.Type, "City", StringComparison.Ordinal)) == true)
                    castleZones.Add(z.Name);
            }
            // A positive authored budget marks a potential guarded-content target. Actual native
            // per-area totals and the strength of spawned guards are deliberately not estimated.
            var treasureZones = zones.Where(z => !string.IsNullOrWhiteSpace(z.Name) && !playerZoneNames.Contains(z.Name)
                && ((z.GuardedContentValue ?? 0) > 0 || (z.GuardedContentValuePerArea ?? 0) > 0))
                .Select(z => z.Name).ToHashSet(StringComparer.Ordinal);

            // BFS hop distances from every player zone.
            var dist = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
            foreach (var p in playerZones) dist[p.ZoneName] = Bfs(p.ZoneName, adj);

            // Per-player metrics.
            var players = new List<PlayerBalance>();
            foreach (var p in playerZones)
            {
                Dictionary<string, int> d = dist[p.ZoneName];
                long startWealth = ZoneValue(p.Zone);

                double expansion = 0;
                double expansionCoefficient = 0;
                foreach (Zone z in zones)
                {
                    if (string.IsNullOrWhiteSpace(z.Name) || playerZoneNames.Contains(z.Name)) continue;
                    if (d.TryGetValue(z.Name, out int hops) && hops >= 1)
                    {
                        expansion += zoneValues[z.Name] / (double)hops; // closer neutral wealth counts more
                        expansionCoefficient += ZoneAreaCoefficient(z) / (double)hops;
                    }
                }

                int nearestOpp = int.MaxValue;
                foreach (var q in playerZones)
                {
                    if (q.ZoneName == p.ZoneName || TeamFor(p.SpawnId) == TeamFor(q.SpawnId)) continue;
                    if (d.TryGetValue(q.ZoneName, out int hops)) nearestOpp = Math.Min(nearestOpp, hops);
                }
                if (nearestOpp == int.MaxValue) nearestOpp = 0;

                int? castleHops = null;
                foreach (string castle in castleZones)
                    if (d.TryGetValue(castle, out int hops))
                        castleHops = castleHops is null ? hops : Math.Min(castleHops.Value, hops);
                int? treasureHops = null;
                foreach (string treasure in treasureZones)
                    if (d.TryGetValue(treasure, out int hops))
                        treasureHops = treasureHops is null ? hops : Math.Min(treasureHops.Value, hops);

                players.Add(new PlayerBalance(
                    PlayerNumber(p.SpawnId, players.Count + 1), p.SpawnId, p.ZoneName,
                    startWealth, Math.Round(expansion, 1), nearestOpp, castleHops)
                {
                    AllianceKey = TeamFor(p.SpawnId),
                    ClosestCastleGuardBudget = CheapestTargetGuardBudget(p.ZoneName, travelConnections, castleZones),
                    TreasureHops = treasureHops,
                    ClosestTreasureGuardBudget = CheapestTargetGuardBudget(p.ZoneName, travelConnections, treasureZones),
                    StartingCities = p.Zone.MainObjects?.Count(o => o is not null && (o.Type == "Spawn" && o.Spawn == p.SpawnId || o.Type == "City" && o.Owner == p.SpawnId)) ?? 0,
                    StartingResourceValue = p.Zone.ResourcesValue ?? 0,
                    StartingAreaCoefficient = ZoneAreaCoefficient(p.Zone),
                    ExpansionAreaCoefficient = Math.Round(expansionCoefficient, 1),
                });
            }

            int score = areaOnly ? ScoreAreaCoefficients(players) : hasAreaCoefficients
                ? (int)Math.Round((Score(players) + ScoreAreaCoefficients(players)) / 2.0) : Score(players);
            bool tournamentIslands = playerZones.Count == 2 && template?.GameRules?.WinConditions?.Tournament == true;
            bool disconnected = !tournamentIslands
                && playerZones.Any(p => playerZones.Any(q => TeamFor(p.SpawnId) != TeamFor(q.SpawnId) && !dist[p.ZoneName].ContainsKey(q.ZoneName)));
            if (disconnected) score = 0;
            List<BalanceFinding> findings = BuildFindings(players, playerZones, dist, castleZones.Count > 0, score);
            if (disconnected)
                findings.Insert(0, new BalanceFinding("S.Bal.Find.Disconnected", BalanceSeverity.Warning, []));
            if (hasAreaCoefficients)
            {
                findings.RemoveAll(f => f.Key == "S.Bal.Find.WellBalanced");
                findings.Insert(0, new BalanceFinding("S.Bal.Find.AreaBudgets", BalanceSeverity.Info, []));
            }

            var teams = players.GroupBy(p => p.AllianceKey, StringComparer.Ordinal).Select(group =>
            {
                double expansion = 0;
                double expansionCoefficient = 0;
                foreach (string zone in zoneValues.Keys.Where(n => !playerZoneNames.Contains(n)))
                {
                    int hops = group.Select(p => dist[p.ZoneName].GetValueOrDefault(zone, int.MaxValue)).Min();
                    if (hops is > 0 and < int.MaxValue)
                    {
                        expansion += zoneValues[zone] / (double)hops;
                        expansionCoefficient += ZoneAreaCoefficient(zoneByName[zone]) / (double)hops;
                    }
                }
                return new TeamBalance(group.Key, group.Select(p => p.PlayerNumber).ToArray(),
                    group.Sum(p => (long)p.StartWealth), group.Sum(p => (long)p.StartingResourceValue),
                    group.Sum(p => p.StartingCities), Math.Round(expansion, 1), group.Select(p => p.NearestOpponentHops).Where(h => h > 0).DefaultIfEmpty(0).Min())
                { StartingAreaCoefficient = group.Sum(p => p.StartingAreaCoefficient), AccessibleExpansionAreaCoefficient = Math.Round(expansionCoefficient, 1) };
            }).ToList();
            double teamWealth = 1 - RelSpread(teams.Select(t => areaOnly ? (double)t.StartingAreaCoefficient : t.StartingWealth));
            double teamExpansion = 1 - RelSpread(teams.Select(t => areaOnly ? t.AccessibleExpansionAreaCoefficient : t.AccessibleExpansionValue));
            if (hasAreaCoefficients && !areaOnly)
            {
                teamWealth = (teamWealth + 1 - RelSpread(teams.Select(t => (double)t.StartingAreaCoefficient))) / 2;
                teamExpansion = (teamExpansion + 1 - RelSpread(teams.Select(t => t.AccessibleExpansionAreaCoefficient))) / 2;
            }
            double teamDistance = 1 - RelSpread(teams.Select(t => (double)t.NearestOpponentHops));
            int teamScore = disconnected ? 0 : (int)Math.Round(100 * Math.Clamp(.5 * teamWealth + .3 * teamExpansion + .2 * teamDistance, 0, 1));
            return new BalanceReport { Applicable = teams.Count > 1, Score = score, TeamScore = teamScore, EconomyComparisonAvailable = !hasAreaCoefficients, Players = players, Teams = teams, Portals = portals, Findings = findings };
        }

        // ── Scoring ───────────────────────────────────────────────────────────────

        private static int Score(IReadOnlyList<PlayerBalance> players)
        {
            double wealthFair    = 1 - RelSpread(players.Select(p => (double)p.StartWealth));
            double expansionFair = 1 - RelSpread(players.Select(p => p.ExpansionValue));
            double proximityFair = 1 - RelSpread(players.Select(p => (double)p.NearestOpponentHops));
            double castleFair = 1 - RelSpread(players.Select(p => p.CastleHops is > 0 ? 1.0 / p.CastleHops.Value : 0));
            double combined = 0.40 * wealthFair + 0.30 * expansionFair + 0.20 * proximityFair + 0.10 * castleFair;
            return (int)Math.Round(100 * Math.Clamp(combined, 0, 1));
        }

        private static int ScoreAreaCoefficients(IReadOnlyList<PlayerBalance> players)
        {
            double densityFair = 1 - RelSpread(players.Select(p => (double)p.StartingAreaCoefficient));
            double expansionFair = 1 - RelSpread(players.Select(p => p.ExpansionAreaCoefficient));
            double proximityFair = 1 - RelSpread(players.Select(p => (double)p.NearestOpponentHops));
            double castleFair = 1 - RelSpread(players.Select(p => p.CastleHops is > 0 ? 1.0 / p.CastleHops.Value : 0));
            return (int)Math.Round(100 * Math.Clamp(.4 * densityFair + .3 * expansionFair + .2 * proximityFair + .1 * castleFair, 0, 1));
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
                    if (players[i].AllianceKey == players[j].AllianceKey) continue;
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

        private static long ZoneValue(Zone z) =>
            (long)(z.GuardedContentValue ?? 0) + (z.UnguardedContentValue ?? 0) + (z.ResourcesValue ?? 0);

        private static long ZoneAreaCoefficient(Zone z) => (long)(z.GuardedContentValuePerArea ?? 0)
            + (z.UnguardedContentValuePerArea ?? 0) + (z.ResourcesValuePerArea ?? 0);

        private static long? CheapestTargetGuardBudget(string start, IReadOnlyList<Connection> edges, HashSet<string> targets)
        {
            var best = new Dictionary<string, long>(StringComparer.Ordinal) { [start] = 0 };
            var queue = new PriorityQueue<string, long>(); queue.Enqueue(start, 0);
            while (queue.TryDequeue(out string? current, out long cost))
            {
                if (best[current] < cost) continue;
                if (targets.Contains(current)) return cost;
                foreach (var edge in edges.Where(ConnectionRules.AllowsTravel))
                {
                    string? next = edge.From == current ? edge.To : edge.To == current ? edge.From : null;
                    if (string.IsNullOrEmpty(next)) continue;
                    long candidate = cost + Math.Max(0, edge.GuardValue ?? 0);
                    if (best.TryGetValue(next, out long known) && known <= candidate) continue;
                    best[next] = candidate; queue.Enqueue(next, candidate);
                }
            }
            return null;
        }

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
