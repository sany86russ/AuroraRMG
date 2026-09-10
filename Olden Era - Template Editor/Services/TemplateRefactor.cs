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
        /// <summary>Neutral ownership is independent of the existing faction rule.</summary>
        public static void MakeCapturable(MainObject obj)
        {
            obj.Type = "City";
            obj.Owner = null;
            obj.Spawn = null;
            obj.IsStartCity = false;
            obj.RemoveGuardIfHasOwner = null;
            obj.Faction ??= new TypedSelector { Type = "Random", Args = [] };
            if (obj.GuardValue is null or 0) obj.GuardValue = 5000;
            if (obj.GuardChance is null or 0) obj.GuardChance = 1.0;
            obj.GuardWeeklyIncrement ??= .10;
            obj.BuildingsConstructionSid ??= "default_buildings_construction";
            obj.Placement ??= "Uniform";
        }

        public static int RemoveMainObject(Variant variant, Zone zone, int index)
        {
            if (zone.MainObjects is not { } objects || index < 0 || index >= objects.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            objects.RemoveAt(index);
            int reset = 0;
            foreach (var z in variant.Zones ?? [])
            foreach (var obj in z.MainObjects ?? [])
                reset += FactionSelectors.RemoveSource(obj.Faction, z.Name, zone.Name, index);
            bool PointsAt(RoadEndpoint? endpoint) => endpoint is { Type: "MainObject", Args.Count: > 0 }
                && int.TryParse(endpoint.Args[0], out var value) && value == index;
            zone.Roads?.RemoveAll(road => PointsAt(road.From) || PointsAt(road.To));
            foreach (var road in zone.Roads ?? [])
            foreach (var endpoint in new[] { road.From, road.To })
                if (endpoint is { Type: "MainObject", Args.Count: > 0 } && int.TryParse(endpoint.Args[0], out var value) && value > index)
                    endpoint.Args[0] = (value - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return reset;
        }
        /// <summary>Each editor owns its document, including unknown fields kept by JsonExtensionData.</summary>
        public static RmgTemplate CreateEditingCopy(RmgTemplate source) =>
            System.Text.Json.JsonSerializer.Deserialize<RmgTemplate>(
                System.Text.Json.JsonSerializer.Serialize(source, JsonExport.Options), JsonExport.Options)!;

        /// <summary>A copied start becomes a neutral town instead of duplicating a player side.</summary>
        public static bool PreparePastedZone(Zone clone, IReadOnlyList<Zone> existingZones, string? sourceName = null)
        {
            var taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (var zone in existingZones)
            foreach (var obj in zone.MainObjects ?? [])
            {
                if (!string.IsNullOrEmpty(obj.Owner)) taken.Add(obj.Owner);
                if (!string.IsNullOrEmpty(obj.Spawn)) taken.Add(obj.Spawn);
            }
            bool cleared = false;
            foreach (var obj in clone.MainObjects ?? [])
            {
                if (!string.IsNullOrEmpty(sourceName)) FactionSelectors.RenameZone(obj.Faction, sourceName, clone.Name);
                if (!string.IsNullOrEmpty(obj.Owner) && taken.Contains(obj.Owner))
                { obj.Owner = null; obj.IsStartCity = false; cleared = true; }
                if (!string.IsNullOrEmpty(obj.Spawn) && taken.Contains(obj.Spawn))
                {
                    obj.Spawn = null;
                    if (obj.Type == "Spawn") obj.Type = "City";
                    obj.IsStartCity = false;
                    cleared = true;
                }
            }
            // Connections are not pasted with a zone; their road anchors belong to the source zone.
            clone.Roads?.RemoveAll(r => r.From?.Type == "Connection" || r.To?.Type == "Connection");
            return cleared;
        }

        /// <summary>Renames roads in one pass, including swaps of two connection names.</summary>
        public static void RenameConnectionReferences(Variant variant, IReadOnlyDictionary<string, string> names)
        {
            foreach (var zone in variant.Zones ?? [])
            foreach (var road in zone.Roads ?? [])
            foreach (var endpoint in new[] { road.From, road.To })
                if (endpoint is { Type: "Connection", Args.Count: > 0 }
                    && names.TryGetValue(endpoint.Args[0], out string? replacement))
                    endpoint.Args[0] = replacement;
        }

        /// <summary>Commits a connection-manager draft only after every row has been validated.</summary>
        public static void ApplyConnectionEdits(Variant variant, IReadOnlyList<Connection> edited)
        {
            var original = variant.Connections ?? [];
            if (original.Count != edited.Count) throw new InvalidOperationException(Localization.LocalizationManager.T("S.Label.ConnectionCount"));
            var unique = new HashSet<string>(StringComparer.Ordinal);
            var renamed = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < edited.Count; i++)
            {
                var before = original[i];
                var after = edited[i];
                if (string.IsNullOrWhiteSpace(after.Name) || !unique.Add(after.Name))
                    throw new InvalidOperationException(Localization.LocalizationManager.T("S.CM.InvalidName"));
                if (before.From != after.From || before.To != after.To)
                    throw new InvalidOperationException(Localization.LocalizationManager.T("S.Label.ConnectionEnds"));
                if (!string.IsNullOrEmpty(before.Name) && before.Name != after.Name)
                    if (!renamed.TryAdd(before.Name, after.Name))
                        throw new InvalidOperationException(Localization.LocalizationManager.T("S.CM.InvalidName"));
            }
            if (original.Where(c => !string.IsNullOrEmpty(c.Name)).GroupBy(c => c.Name, StringComparer.Ordinal)
                .Any(group => group.Count() > 1 && renamed.ContainsKey(group.Key!)))
                throw new InvalidOperationException(Localization.LocalizationManager.T("S.CM.InvalidName"));
            RenameConnectionReferences(variant, renamed);
            variant.Connections = new List<Connection>(edited);
        }

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

            foreach (var zone in variant.Zones ?? [])
            foreach (var obj in zone.MainObjects ?? [])
                FactionSelectors.RenameZone(obj.Faction, oldName, newName);

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

            foreach (var connection in variant.Connections ?? [])
                if (string.Equals(connection.GuardZone, zoneName, StringComparison.Ordinal))
                    connection.GuardZone = connection.From;

            foreach (string name in doomed) RemoveRoadsFor(variant, name);

            foreach (Zone zone in variant.Zones ?? [])
            {
                if (string.Equals(zone.Name, zoneName, StringComparison.Ordinal)) continue;
                foreach (var obj in zone.MainObjects ?? [])
                    FactionSelectors.RemoveSource(obj.Faction, zone.Name, zoneName, null);
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
