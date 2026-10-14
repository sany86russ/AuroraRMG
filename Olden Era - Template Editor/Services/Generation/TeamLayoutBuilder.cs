using System.Text.Json;
using Olden_Era___Template_Editor.Models;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.Generation;

/// <summary>Own graph construction; the preview is a layout hint, never a promise about game geometry.</summary>
public static class TeamLayoutBuilder
{
    public static void Apply(Variant variant, GeneratorSettings settings, AlliancePlan plan)
    {
        if (!plan.Enabled || settings.GameEndConditions.VictoryCondition == "win_condition_6" || settings.TournamentRules.Enabled) return;
        if (settings.ProfileOptions.Alliances.Layout == AllianceLayout.ExistingTopology && settings.NoDirectPlayerConnections && settings.MinNeutralZonesBetweenPlayers > 1)
            throw new ArgumentException("The existing-topology alliance overlay cannot guarantee more than one neutral buffer between enemy starts. Choose Team Regions, Team Lanes or Team Arena to enforce the requested depth.");
        if (settings.ProfileOptions.Alliances.Layout == AllianceLayout.ExistingTopology && settings.ProfileOptions.Symmetry == MapSymmetry.Teams)
            throw new ArgumentException("Mirrored team territories require Team Regions, Team Lanes or Team Arena. Existing topology preserves the authored graph and cannot guarantee mirrored roles.");
        var zones = variant.Zones ?? throw new InvalidOperationException("Team layouts need generated zones.");
        var starts = plan.Slots.ToDictionary(s => s.Player, s => zones.Single(z => z.MainObjects?.Any(o => o.Spawn == $"Player{s.Player}") == true));
        var startsByName = starts.ToDictionary(p => p.Value.Name, p => $"Player{p.Key}", StringComparer.Ordinal);
        var oldConnections = variant.Connections ?? [];
        var neutral = zones.Where(z => !startsByName.ContainsKey(z.Name)).ToList();
        var connections = new List<Connection>();
        int guard = settings.ZoneCfg.BorderGuardStrengthPercent == 0 ? 0 : (int)Math.Round(oldConnections.Where(c => (c.GuardValue ?? 0) > 0).Select(c => c.GuardValue!.Value).DefaultIfEmpty(10000).Average());
        var pairs = new HashSet<string>(StringComparer.Ordinal);
        bool rebuilding = settings.ProfileOptions.Alliances.Layout != AllianceLayout.ExistingTopology;

        void Link(Zone from, Zone to, bool allied = false)
        {
            if (from.Name == to.Name) return;
            string pair = string.CompareOrdinal(from.Name, to.Name) < 0 ? from.Name + "\n" + to.Name : to.Name + "\n" + from.Name;
            if (!pairs.Add(pair)) return;
            connections.Add(new()
            {
                Name = $"Alliance-{connections.Count + 1}", From = from.Name, To = to.Name, ConnectionType = "Direct",
                GuardZone = from.Name, GuardValue = allied ? 0 : guard,
                GuardWeeklyIncrement = allied ? 0 : 0.15, GuardEscape = false,
                GuardMatchGroup = allied ? null : "alliance-front", Road = settings.GenerateRoads,
            });
        }

        Zone AddBuffer()
        {
            if (zones.Count >= TemplateGenerator.ZoneLetters.Length) throw new ArgumentException("This team layout needs more than 48 zones. Reduce corridor depth or neutral-zone count.");
            var prototype = neutral.FirstOrDefault() ?? starts.Values.First();
            var copy = JsonSerializer.Deserialize<Zone>(JsonSerializer.Serialize(prototype, JsonExport.Options), JsonExport.Options)!;
            int serial = 1;
            while (zones.Any(z => z.Name == $"Alliance-Buffer-{serial}")) serial++;
            copy.Name = $"Alliance-Buffer-{serial}";
            copy.MainObjects = []; copy.MandatoryContent = null; copy.Roads = null;
            copy.ZoneBiome = new() { Type = "FromList", Args = ["Grass"] };
            copy.ContentBiome = new() { Type = "MatchZone", Args = [copy.Name] };
            copy.MetaObjectsBiome = new() { Type = "MatchZone", Args = [copy.Name] };
            copy.Layout = "zone_layout_sides";
            copy.GeneratorPosition = null; copy.GeneratorRing = null;
            zones.Add(copy); neutral.Add(copy);
            return copy;
        }

        void EnsureNeutral(int count) { while (neutral.Count < count) AddBuffer(); }
        void MirrorBranches(List<Zone>[] branches)
        {
            if (settings.ProfileOptions.Symmetry != MapSymmetry.Teams || branches.Length == 0) return;
            for (int group = 0; group < branches.Length; group++)
                for (int level = 0; level < branches[group].Count; level++)
                {
                    Zone target = branches[group][level];
                    if (group > 0)
                    {
                        Zone source = branches[0][level];
                        var copy = JsonSerializer.Deserialize<Zone>(JsonSerializer.Serialize(source, JsonExport.Options), JsonExport.Options)!;
                        copy.Name = target.Name;
                        foreach (var obj in copy.MainObjects ?? [])
                        {
                            FactionSelectors.RenameZone(obj.Faction, source.Name, copy.Name);
                            if (obj.Placement is "NearZone" && obj.PlacementArgs is { } args)
                                for (int a = 0; a < args.Count; a++) if (args[a] == source.Name) args[a] = copy.Name;
                        }
                        foreach (var biome in new[] { copy.ZoneBiome, copy.ContentBiome, copy.MetaObjectsBiome })
                            if (biome?.Type == "MatchZone" && biome.Args is { Count: > 0 } args && args[0] == source.Name) args[0] = copy.Name;
                        zones[zones.IndexOf(target)] = copy;
                        neutral[neutral.IndexOf(target)] = copy;
                        branches[group][level] = target = copy;
                    }
                    target.GeneratorAllianceKey = plan.Groups[group].Key;
                    target.GeneratorBranchDepth = level;
                }
        }
        int depth = Math.Max(1, settings.MinNeutralZonesBetweenPlayers);
        depth = Math.Max(depth, settings.ProfileOptions.Tempo == MapTempo.Exploration ? 3 : settings.ProfileOptions.Tempo == MapTempo.Balanced ? 2 : 1);

        switch (settings.ProfileOptions.Alliances.Layout)
        {
            case AllianceLayout.ExistingTopology:
                foreach (var edge in oldConnections)
                {
                    bool isEnemyPair = startsByName.TryGetValue(edge.From, out string? a) && startsByName.TryGetValue(edge.To, out string? b) && !plan.AreAllies(a, b);
                    if (settings.NoDirectPlayerConnections && isEnemyPair && ConnectionRules.AllowsTravel(edge)) continue;
                    connections.Add(edge);
                    pairs.Add(string.CompareOrdinal(edge.From, edge.To) < 0 ? edge.From + "\n" + edge.To : edge.To + "\n" + edge.From);
                }
                // An authored topology without neutral buffers can lose its whole enemy frontier when
                // no-direct is selected. Supply a common neutral connector instead of isolated teams.
                if (settings.NoDirectPlayerConnections)
                {
                    EnsureNeutral(1);
                    foreach (var group in plan.Groups) Link(starts[group.Players[0]], neutral[0]);
                }
                break;
            case AllianceLayout.TeamRegions:
                EnsureNeutral(plan.Groups.Count * depth);
                Zone? sharedGoal = null;
                if (settings.ProfileOptions.Symmetry == MapSymmetry.Teams)
                {
                    EnsureNeutral(plan.Groups.Count * depth + 1);
                    sharedGoal = neutral.FirstOrDefault(z => z.MainObjects?.Any(o => o.HoldCityWinCon == true) == true) ?? neutral[^1];
                    sharedGoal.GeneratorAllianceKey = "contested";
                    EnsureNeutral((int)Math.Ceiling((neutral.Count - 1) / (double)plan.Groups.Count) * plan.Groups.Count + 1);
                }
                var regional = Enumerable.Range(0, plan.Groups.Count).Select(_ => new List<Zone>()).ToArray();
                int regionIndex = 0;
                foreach (var zone in neutral.Where(z => !ReferenceEquals(z, sharedGoal))) regional[regionIndex++ % regional.Length].Add(zone);
                MirrorBranches(regional);
                for (int g = 0; g < regional.Length; g++)
                {
                    foreach (int player in plan.Groups[g].Players) Link(starts[player], regional[g][0]);
                    for (int n = 1; n < regional[g].Count; n++) Link(regional[g][n - 1], regional[g][n]);
                    Link(regional[g][^1], regional[(g + 1) % regional.Length][^1]);
                    if (sharedGoal is not null) Link(regional[g][^1], sharedGoal);
                }
                break;
            case AllianceLayout.TeamLanes:
                bool symmetricLanes = settings.ProfileOptions.Symmetry == MapSymmetry.Teams;
                int laneCount = symmetricLanes ? plan.Groups.Count : starts.Count;
                EnsureNeutral(laneCount * depth + 1);
                if (settings.ProfileOptions.Symmetry == MapSymmetry.Teams)
                    EnsureNeutral((int)Math.Ceiling((neutral.Count - 1) / (double)laneCount) * laneCount + 1);
                var arena = neutral.FirstOrDefault(z => z.MainObjects?.Any(o => o.HoldCityWinCon == true) == true) ?? neutral[^1];
                arena.GeneratorAllianceKey = "contested";
                var lanes = Enumerable.Range(0, laneCount).Select(_ => new List<Zone>()).ToArray();
                int next = 0;
                foreach (var zone in neutral.Where(z => !ReferenceEquals(z, arena))) lanes[next++ % lanes.Length].Add(zone);
                MirrorBranches(lanes);
                for (int p = 0; p < laneCount; p++)
                {
                    Zone previous = starts[symmetricLanes ? plan.Groups[p].Players[0] : p + 1];
                    foreach (var zone in lanes[p]) { Link(previous, zone); previous = zone; }
                    Link(previous, arena);
                    if (symmetricLanes) foreach (int member in plan.Groups[p].Players.Skip(1)) Link(starts[member], lanes[p][0]);
                }
                break;
            case AllianceLayout.TeamArena:
                EnsureNeutral(plan.Groups.Count * depth + 1);
                if (settings.ProfileOptions.Symmetry == MapSymmetry.Teams)
                    EnsureNeutral((int)Math.Ceiling((neutral.Count - 1) / (double)plan.Groups.Count) * plan.Groups.Count + 1);
                var center = neutral.FirstOrDefault(z => z.MainObjects?.Any(o => o.HoldCityWinCon == true) == true) ?? neutral[^1];
                center.GeneratorAllianceKey = "contested";
                var branches = Enumerable.Range(0, plan.Groups.Count).Select(_ => new List<Zone>()).ToArray();
                int branch = 0;
                foreach (var zone in neutral.Where(z => !ReferenceEquals(z, center))) branches[branch++ % branches.Length].Add(zone);
                MirrorBranches(branches);
                for (int g = 0; g < branches.Length; g++)
                {
                    foreach (int player in plan.Groups[g].Players) Link(starts[player], branches[g][0]);
                    for (int n = 1; n < branches[g].Count; n++) Link(branches[g][n - 1], branches[g][n]);
                    Link(branches[g][^1], center);
                }
                break;
            default: throw new ArgumentOutOfRangeException(nameof(settings), "Unknown alliance layout.");
        }

        foreach (var group in plan.Groups)
            for (int i = 1; i < group.Players.Count; i++) Link(starts[group.Players[i - 1]], starts[group.Players[i]], allied: true);
        if (rebuilding && settings.RandomPortals)
        {
            if (settings.ProfileOptions.Symmetry == MapSymmetry.Teams)
                AddMirroredPortals(connections, oldConnections, neutral, plan, settings);
            else foreach (var portal in oldConnections.Where(c => c.ConnectionType == "Portal"))
            {
                bool enemies = startsByName.TryGetValue(portal.From, out string? first) && startsByName.TryGetValue(portal.To, out string? second) && !plan.AreAllies(first, second);
                if (!settings.NoDirectPlayerConnections || !enemies) connections.Add(portal);
            }
        }
        variant.Connections = connections;
        PositionGroups(starts, neutral, plan);
        RebuildConnectionRoads(variant, settings.GenerateRoads);
    }

    private static void AddMirroredPortals(List<Connection> connections, List<Connection> oldConnections,
        List<Zone> neutral, AlliancePlan plan, GeneratorSettings settings)
    {
        var oldPortals = oldConnections.Where(c => c.ConnectionType == "Portal").ToArray();
        if (settings.MaxPortalConnections <= 0 || oldPortals.Length == 0) return;
        if (settings.MaxPortalConnections < plan.Groups.Count)
            throw new ArgumentException("Mirrored team portals need at least one portal per team. Increase the portal limit or disable additional portals/team symmetry.");
        var branches = plan.Groups.Select(group => neutral.Where(z => z.GeneratorAllianceKey == group.Key)
            .OrderBy(z => z.GeneratorBranchDepth).ToArray()).ToArray();
        Zone common = neutral.First(z => z.GeneratorAllianceKey == "contested");
        int branchLength = branches[0].Length;
        // Each candidate is an existing direct edge at the same role/depth in every team.
        // Parallel portals add an alternate entrance without shortening the exported zone graph.
        // Real placement and movement costs remain the engine's responsibility.
        var levels = Enumerable.Range(0, branchLength).ToArray();
        var random = new Random((settings.Seed ?? Random.Shared.Next()) ^ 0x50A7A1);
        for (int i = levels.Length - 1; i > 0; i--)
        { int other = random.Next(i + 1); (levels[i], levels[other]) = (levels[other], levels[i]); }
        int bundles = Math.Min(branchLength, Math.Min(settings.MaxPortalConnections / plan.Groups.Count,
            (int)Math.Ceiling(oldPortals.Length / (double)plan.Groups.Count)));
        for (int bundle = 0; bundle < bundles; bundle++)
            for (int group = 0; group < branches.Length; group++)
            {
                int level = levels[bundle];
                Zone from = branches[group][level];
                Zone to = level + 1 < branchLength ? branches[group][level + 1] : common;
                var portal = JsonSerializer.Deserialize<Connection>(JsonSerializer.Serialize(oldPortals[0], JsonExport.Options), JsonExport.Options)!;
                portal.Name = $"Alliance-Portal-{bundle + 1}-{group + 1}";
                portal.From = from.Name; portal.To = to.Name; portal.GuardZone = from.Name;
                connections.Add(portal);
            }
    }

    private static void PositionGroups(Dictionary<int, Zone> starts, List<Zone> neutral, AlliancePlan plan)
    {
        for (int g = 0; g < plan.Groups.Count; g++)
        {
            var players = plan.Groups[g].Players;
            for (int i = 0; i < players.Count; i++)
            {
                double angle = (g + (i + .5) / players.Count) * Math.Tau / plan.Groups.Count;
                starts[players[i]].GeneratorPosition = (.5 + .43 * Math.Cos(angle), .5 + .43 * Math.Sin(angle));
                starts[players[i]].GeneratorRing = null;
            }
        }
        for (int i = 0; i < neutral.Count; i++)
        {
            double angle = Math.Tau * i / Math.Max(1, neutral.Count);
            neutral[i].GeneratorPosition = (.5 + .24 * Math.Cos(angle), .5 + .24 * Math.Sin(angle));
            neutral[i].GeneratorRing = null;
        }
    }

    internal static void RebuildConnectionRoads(Variant variant, bool enabled)
    {
        foreach (var zone in variant.Zones ?? [])
        {
            if (!enabled) { zone.Roads = null; continue; }
            var roads = (zone.Roads ?? []).Where(r => r.From?.Type != "Connection" && r.To?.Type != "Connection").ToList();
            var edges = (variant.Connections ?? []).Where(c => c.From == zone.Name || c.To == zone.Name).Where(c => c.Name is not null && ConnectionRules.AllowsTravel(c)).ToList();
            foreach (var edge in edges)
                roads.Add(new()
                {
                    Type = "Dirt", From = zone.MainObjects is { Count: > 0 }
                        ? new() { Type = "MainObject", Args = ["0"] }
                        : new() { Type = "Connection", Args = [edges[0].Name!] },
                    To = new() { Type = "Connection", Args = [edge.Name!] },
                });
            zone.Roads = roads;
        }
    }
}
