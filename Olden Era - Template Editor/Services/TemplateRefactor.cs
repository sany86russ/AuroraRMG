using System;
using System.Collections.Generic;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>
    /// Model-level edits that have to touch more than one place at once. Kept out of the editor window
    /// so the rewiring rules are unit-testable and shared by every caller.
    /// </summary>
    public static class TemplateRefactor
    {
        /// <summary>
        /// Renames a zone and re-points every reference to it inside the variant.
        /// <para>
        /// A zone name is not only used by <c>zones[].name</c>: connections reference it through
        /// <c>from</c>/<c>to</c> <b>and</b> <c>guardZone</c>, other zones may copy its terrain via a
        /// <c>MatchZone</c> biome selector, and the variant's <c>orientation.zeroAngleZone</c> anchors
        /// the map's rotation on it. Renaming only the zone leaves those pointing at a name that no
        /// longer exists, which silently breaks guard placement, terrain matching and the rotation anchor.
        /// </para>
        /// Does nothing when the names are equal or either is blank.
        /// </summary>
        public static void RenameZoneReferences(Variant variant, string oldName, string newName)
        {
            if (variant is null) return;
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName)) return;
            if (string.Equals(oldName, newName, StringComparison.Ordinal)) return;

            foreach (Connection c in variant.Connections ?? [])
            {
                if (string.Equals(c.From, oldName, StringComparison.Ordinal)) c.From = newName;
                if (string.Equals(c.To, oldName, StringComparison.Ordinal)) c.To = newName;
                if (string.Equals(c.GuardZone, oldName, StringComparison.Ordinal)) c.GuardZone = newName;
            }

            foreach (Zone zone in variant.Zones ?? [])
            foreach (BiomeSelector? selector in new[] { zone.ZoneBiome, zone.ContentBiome, zone.MetaObjectsBiome })
            {
                if (!string.Equals(selector?.Type, "MatchZone", StringComparison.Ordinal)) continue;
                List<string>? args = selector!.Args;
                if (args is null) continue;
                for (int i = 0; i < args.Count; i++)
                    if (string.Equals(args[i], oldName, StringComparison.Ordinal)) args[i] = newName;
            }

            if (string.Equals(variant.Orientation?.ZeroAngleZone, oldName, StringComparison.Ordinal))
                variant.Orientation!.ZeroAngleZone = newName;
        }

        /// <summary>
        /// Removes every reference to a zone that is being deleted: its connections (and the roads that
        /// pointed at them), any <c>MatchZone</c> biome selector aimed at it (reset to "this zone's own
        /// terrain") and the orientation anchor (moved to the first remaining zone). The zone itself is
        /// left for the caller to remove. Returns how many connections were dropped.
        /// </summary>
        public static int RemoveZoneReferences(Variant variant, string zoneName)
        {
            if (variant is null || string.IsNullOrWhiteSpace(zoneName)) return 0;

            var doomed = new List<string>();
            foreach (Connection c in variant.Connections ?? [])
                if (string.Equals(c.From, zoneName, StringComparison.Ordinal)
                 || string.Equals(c.To, zoneName, StringComparison.Ordinal))
                    if (!string.IsNullOrEmpty(c.Name)) doomed.Add(c.Name!);

            int removed = variant.Connections?.RemoveAll(c =>
                string.Equals(c.From, zoneName, StringComparison.Ordinal) ||
                string.Equals(c.To, zoneName, StringComparison.Ordinal)) ?? 0;

            foreach (string name in doomed) RemoveRoadsFor(variant, name);

            foreach (Zone zone in variant.Zones ?? [])
            {
                if (string.Equals(zone.Name, zoneName, StringComparison.Ordinal)) continue;
                foreach (BiomeSelector? selector in new[] { zone.ZoneBiome, zone.ContentBiome, zone.MetaObjectsBiome })
                {
                    if (!string.Equals(selector?.Type, "MatchZone", StringComparison.Ordinal)) continue;
                    // Empty args = "match this zone" — the engine default for a zone without a town.
                    selector!.Args?.RemoveAll(a => string.Equals(a, zoneName, StringComparison.Ordinal));
                }
            }

            if (string.Equals(variant.Orientation?.ZeroAngleZone, zoneName, StringComparison.Ordinal))
            {
                Zone? survivor = (variant.Zones ?? []).Find(z => !string.Equals(z.Name, zoneName, StringComparison.Ordinal));
                variant.Orientation!.ZeroAngleZone = survivor?.Name;
            }

            return removed;
        }

        /// <summary>
        /// Drops the roads that anchor on a connection which no longer exists. A dangling
        /// <c>Connection</c> road endpoint makes the engine's road builder fail while generating.
        /// </summary>
        public static void RemoveRoadsFor(Variant variant, string connectionName)
        {
            if (variant is null || string.IsNullOrWhiteSpace(connectionName)) return;

            static bool Points(RoadEndpoint? ep, string name) =>
                string.Equals(ep?.Type, "Connection", StringComparison.Ordinal)
                && ep!.Args is { Count: > 0 } args
                && string.Equals(args[0], name, StringComparison.Ordinal);

            foreach (Zone zone in variant.Zones ?? [])
                zone.Roads?.RemoveAll(r => Points(r.From, connectionName) || Points(r.To, connectionName));
        }
    }
}
