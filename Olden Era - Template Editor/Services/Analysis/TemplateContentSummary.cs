using System;
using System.Collections.Generic;
using System.Linq;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services.Analysis
{
    /// <summary>What a zone is, for the "what's inside" breakdown.</summary>
    public enum ZoneRole { Player, Hub, NeutralCastle, Neutral }

    /// <summary>One row of the content breakdown.</summary>
    public sealed record ZoneInfo(
        string Name,
        ZoneRole Role,
        string? Biome,
        int Treasure,
        int Resources,
        int Connections);

    /// <summary>The result of <see cref="TemplateContentSummary.Analyze"/> — per-zone rows + map totals.</summary>
    public sealed class ContentSummary
    {
        public IReadOnlyList<ZoneInfo> Zones { get; init; } = [];
        public int ZoneCount { get; init; }
        public int PlayerZones { get; init; }
        /// <summary>Non-player, non-castle zones (plain neutrals + the hub).</summary>
        public int NeutralZones { get; init; }
        /// <summary>Neutral castle zones (a City object, no player spawn).</summary>
        public int CastleZones { get; init; }
        public int ConnectionCount { get; init; }
        public long TotalTreasure { get; init; }
        public long TotalResources { get; init; }
    }

    /// <summary>
    /// Pure, local "what's inside the map" analysis of a generated <see cref="RmgTemplate"/>: classifies
    /// every zone (player / hub / neutral castle / neutral), reads its treasure &amp; resource budgets and
    /// its connection degree, and rolls them up into map totals. No I/O — usable from any UI surface.
    /// </summary>
    public static class TemplateContentSummary
    {
        public static ContentSummary Analyze(RmgTemplate? template)
        {
            Variant? variant = template?.Variants?.FirstOrDefault();
            List<Zone> zones = variant?.Zones ?? [];
            List<Connection> connections = variant?.Connections ?? [];

            var validNames = new HashSet<string>(
                zones.Where(z => !string.IsNullOrWhiteSpace(z.Name)).Select(z => z.Name), StringComparer.Ordinal);

            // Degree per zone (undirected, valid endpoints, no self-loops).
            var degree = new Dictionary<string, int>(StringComparer.Ordinal);
            int connectionCount = 0;
            foreach (Connection c in connections)
            {
                if (string.IsNullOrEmpty(c.From) || string.IsNullOrEmpty(c.To) || c.From == c.To) continue;
                if (!validNames.Contains(c.From) || !validNames.Contains(c.To)) continue;
                connectionCount++;
                degree[c.From] = degree.GetValueOrDefault(c.From) + 1;
                degree[c.To] = degree.GetValueOrDefault(c.To) + 1;
            }

            var infos = new List<ZoneInfo>(zones.Count);
            foreach (Zone z in zones)
            {
                if (string.IsNullOrWhiteSpace(z.Name)) continue;
                ZoneRole role = ClassifyRole(z);
                int treasure = (z.GuardedContentValue ?? 0) + (z.UnguardedContentValue ?? 0);
                int resources = z.ResourcesValue ?? 0;
                infos.Add(new ZoneInfo(z.Name, role, ResolveBiome(z), treasure, resources,
                    degree.GetValueOrDefault(z.Name)));
            }

            return new ContentSummary
            {
                Zones = infos,
                ZoneCount = infos.Count,
                PlayerZones = infos.Count(i => i.Role == ZoneRole.Player),
                CastleZones = infos.Count(i => i.Role == ZoneRole.NeutralCastle),
                NeutralZones = infos.Count(i => i.Role is ZoneRole.Neutral or ZoneRole.Hub),
                ConnectionCount = connectionCount,
                TotalTreasure = infos.Sum(i => (long)i.Treasure),
                TotalResources = infos.Sum(i => (long)i.Resources),
            };
        }

        private static ZoneRole ClassifyRole(Zone z)
        {
            List<MainObject> objs = z.MainObjects ?? [];
            if (objs.Any(o => string.Equals(o.Type, "Spawn", StringComparison.Ordinal))) return ZoneRole.Player;
            if (objs.Any(o => string.Equals(o.Type, "City", StringComparison.Ordinal))) return ZoneRole.NeutralCastle;
            if (z.Layout is string layout && layout.Contains("center", StringComparison.OrdinalIgnoreCase))
                return ZoneRole.Hub;
            return ZoneRole.Neutral;
        }

        /// <summary>Best-effort biome token for the zone (single FromList biome), else null. Not localized.</summary>
        private static string? ResolveBiome(Zone z)
        {
            BiomeSelector? b = z.ZoneBiome;
            if (b is { Type: "FromList", Args: { Count: > 0 } args })
                return args.Count == 1 ? args[0] : null; // mixed → leave unset
            return null;
        }
    }
}
