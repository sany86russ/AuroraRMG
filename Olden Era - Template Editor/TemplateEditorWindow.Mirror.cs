using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Mirror mode: build one half of the map and let the editor keep the other half in sync.
    /// <para>
    /// Symmetry is what makes a competitive map fair, and doing it by hand means duplicating every
    /// zone, every property and every connection — then keeping them in step through later edits.
    /// With mirror mode on, adding / moving / deleting a zone, editing its properties and linking two
    /// zones are all applied to the twin on the other side of the vertical axis as well.
    /// </para>
    /// <para>
    /// Zones sitting ON the axis (a central arena, a shared hub) are deliberately <b>not</b> twinned —
    /// they are the shared middle of the map.
    /// </para>
    /// </summary>
    public partial class TemplateEditorWindow
    {
        private bool _mirrorMode;
        private bool _mirrorProperties = true;
        private bool _mirrorConnections = true;

        /// <summary>zone → twin, stored in BOTH directions so either side can look the other up.</summary>
        private readonly Dictionary<string, string> _mirrorMap = new(StringComparer.Ordinal);

        private Line? _mirrorAxisLine;

        /// <summary>How close to the axis a zone may sit and still count as "shared, not twinned".</summary>
        private const double MirrorAxisTolerance = 24.0;

        /// <summary>How far two positions may drift and still be recognised as an existing mirrored pair.</summary>
        private const double MirrorPairTolerance = 40.0;

        private double MirrorAxisX => GraphCanvas.Width / 2.0;

        private Point MirrorPoint(Point p) => new(GraphCanvas.Width - p.X, p.Y);

        private bool IsOnMirrorAxis(Point p) => Math.Abs(p.X - MirrorAxisX) <= MirrorAxisTolerance;

        private Zone? ZoneByName(string? name) =>
            name is null ? null : Zones.FirstOrDefault(z => string.Equals(z.Name, name, StringComparison.Ordinal));

        private Zone? TwinOf(Zone z) =>
            _mirrorMap.TryGetValue(z.Name, out string? twin) ? ZoneByName(twin) : null;

        // ── Toggle ───────────────────────────────────────────────────────────────────

        private void BtnMirror_Click(object sender, RoutedEventArgs e)
        {
            Keyboard_ClearFocusSafe();

            if (_mirrorMode)
            {
                DisableMirrorMode();
                return;
            }

            var dlg = new MirrorSettingsWindow(_mirrorProperties, _mirrorConnections) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            _mirrorProperties = dlg.MirrorProperties;
            _mirrorConnections = dlg.MirrorConnections;
            _mirrorMode = true;
            BtnMirror.Background = new SolidColorBrush(Color.FromRgb(60, 50, 90));

            RebuildMirrorPairs();
            DrawMirrorAxis();

            if (dlg.MakeSymmetricNow) MakeSymmetricNow();
            else UpdateStatus(L("S.EC.MirrorOn", _mirrorMap.Count / 2));
        }

        private void DisableMirrorMode()
        {
            _mirrorMode = false;
            _mirrorMap.Clear();
            BtnMirror.Background = null;
            RemoveMirrorAxis();
            UpdateStatus(L("S.EC.MirrorOff"));
        }

        /// <summary>Focus commit that is safe to call before the window is fully loaded.</summary>
        private void Keyboard_ClearFocusSafe()
        {
            try { System.Windows.Input.Keyboard.ClearFocus(); } catch { /* not focused yet */ }
        }

        /// <summary>
        /// Verification hook used by <c>--shoot-mirror</c>: turns the mode on and rebuilds the right
        /// half from the left without showing the options dialog, then returns a textual dump of the
        /// resulting graph so the result can be checked outside the UI.
        /// </summary>
        internal string DebugMirrorAndSymmetrize()
        {
            _mirrorProperties = true;
            _mirrorConnections = true;
            _mirrorMode = true;
            RebuildMirrorPairs();
            DrawMirrorAxis();
            MakeSymmetricNow();

            var lines = new List<string>
            {
                $"zones={Zones.Count} connections={Connections.Count} pairs={_mirrorMap.Count / 2}",
                "-- zones --",
            };
            foreach (Zone z in Zones)
            {
                Point p = _positions.TryGetValue(z.Name, out Point pp) ? pp : new Point(double.NaN, double.NaN);
                string spawn = string.Join("/", (z.MainObjects ?? []).Select(o => o.Spawn).Where(s => s is not null));
                string twin = _mirrorMap.TryGetValue(z.Name, out string? t) ? t : "-";
                lines.Add($"{z.Name,-24} x={p.X,7:0.0} y={p.Y,7:0.0} layout={z.Layout,-28} spawn={spawn,-10} twin={twin}");
            }
            lines.Add("-- connections --");
            foreach (Connection c in Connections)
                lines.Add($"{c.Name,-32} {c.From,-22} -> {c.To,-22} guard={c.GuardValue}");
            lines.Add("-- validation --");
            lines.AddRange(Validate().DefaultIfEmpty("OK — no issues"));
            return string.Join(Environment.NewLine, lines);
        }

        // ── Axis overlay ─────────────────────────────────────────────────────────────

        private void DrawMirrorAxis()
        {
            RemoveMirrorAxis();
            _mirrorAxisLine = new Line
            {
                X1 = MirrorAxisX,
                Y1 = 0,
                X2 = MirrorAxisX,
                Y2 = GraphCanvas.Height,
                Stroke = new SolidColorBrush(Color.FromArgb(150, 150, 130, 220)),
                StrokeThickness = 1.5,
                StrokeDashArray = [6, 5],
                IsHitTestVisible = false,
            };
            System.Windows.Controls.Panel.SetZIndex(_mirrorAxisLine, 5);
            GraphCanvas.Children.Add(_mirrorAxisLine);
        }

        private void RemoveMirrorAxis()
        {
            if (_mirrorAxisLine is null) return;
            GraphCanvas.Children.Remove(_mirrorAxisLine);
            _mirrorAxisLine = null;
        }

        /// <summary>Re-draws the axis after a graph rebuild (which clears the canvas).</summary>
        private void RestoreMirrorAxisAfterRebuild()
        {
            if (_mirrorMode) DrawMirrorAxis();
        }

        // ── Pairing ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Recognises zones that are already mirror images of each other, so turning the mode on for an
        /// existing symmetric map keeps editing both sides instead of creating a second copy.
        /// </summary>
        private void RebuildMirrorPairs()
        {
            _mirrorMap.Clear();
            var unpaired = Zones
                .Where(z => _positions.TryGetValue(z.Name, out Point p) && !IsOnMirrorAxis(p))
                .ToList();

            foreach (Zone z in unpaired)
            {
                if (_mirrorMap.ContainsKey(z.Name)) continue;
                Point want = MirrorPoint(_positions[z.Name]);

                Zone? best = null;
                double bestDistance = MirrorPairTolerance;
                foreach (Zone other in unpaired)
                {
                    if (ReferenceEquals(other, z) || _mirrorMap.ContainsKey(other.Name)) continue;
                    Point p = _positions[other.Name];
                    double d = Math.Sqrt((p.X - want.X) * (p.X - want.X) + (p.Y - want.Y) * (p.Y - want.Y));
                    if (d > bestDistance) continue;
                    bestDistance = d;
                    best = other;
                }

                if (best is null) continue;
                _mirrorMap[z.Name] = best.Name;
                _mirrorMap[best.Name] = z.Name;
            }
        }

        // ── One-shot symmetrisation ──────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the right half of the map from the left half: drops everything right of the axis,
        /// then re-creates it as a mirror of the left (zones with their properties, and the matching
        /// connections). Zones on the axis are kept as the shared middle. Player starts on the new side
        /// are renumbered to the next free player, so a 1v1 becomes a real 1v1.
        /// </summary>
        private void MakeSymmetricNow()
        {
            var left = new List<Zone>();
            var axis = new List<Zone>();
            var right = new List<Zone>();

            foreach (Zone z in Zones.ToList())
            {
                if (!_positions.TryGetValue(z.Name, out Point p)) { left.Add(z); continue; }
                if (IsOnMirrorAxis(p)) axis.Add(z);
                else if (p.X < MirrorAxisX) left.Add(z);
                else right.Add(z);
            }

            if (left.Count == 0)
            {
                UpdateStatus(L("S.EC.MirrorNoSource"));
                return;
            }

            foreach (Zone z in right)
            {
                TemplateRefactor.RemoveZoneReferences(Variant, z.Name);
                Zones.Remove(z);
                _positions.Remove(z.Name);
            }

            _mirrorMap.Clear();
            var kept = new HashSet<string>(left.Concat(axis).Select(z => z.Name), StringComparer.Ordinal);

            foreach (Zone source in left)
                CreateTwin(source, _positions[source.Name]);

            // Mirror the connections whose BOTH endpoints survived (a link that touched the old right
            // half is gone with it).
            foreach (Connection c in Connections.ToList())
            {
                if (!kept.Contains(c.From) || !kept.Contains(c.To)) continue;
                MirrorConnection(c);
            }

            ReconnectStrandedAxisZones(axis);

            MarkDirty();
            RebuildGraph();
            BuildInspector();
            UpdateStatus(L("S.EC.Symmetrized", left.Count, axis.Count));
        }

        /// <summary>
        /// A zone on the axis whose only links pointed into the discarded right half would be left
        /// stranded by symmetrisation. Re-attach each such zone to its nearest neighbour on the left,
        /// then mirror that link so both sides stay identical.
        /// </summary>
        private void ReconnectStrandedAxisZones(List<Zone> axisZones)
        {
            foreach (Zone axisZone in axisZones)
            {
                bool linked = Connections.Any(c =>
                    string.Equals(c.From, axisZone.Name, StringComparison.Ordinal) ||
                    string.Equals(c.To, axisZone.Name, StringComparison.Ordinal));
                if (linked) continue;
                if (!_positions.TryGetValue(axisZone.Name, out Point axisPos)) continue;

                Zone? nearest = null;
                double best = double.MaxValue;
                foreach (Zone candidate in Zones)
                {
                    if (ReferenceEquals(candidate, axisZone)) continue;
                    if (!_positions.TryGetValue(candidate.Name, out Point p)) continue;
                    if (p.X >= MirrorAxisX) continue; // pick from the left half; the twin gets the mirror
                    double d = (p.X - axisPos.X) * (p.X - axisPos.X) + (p.Y - axisPos.Y) * (p.Y - axisPos.Y);
                    if (d >= best) continue;
                    best = d;
                    nearest = candidate;
                }

                if (nearest is null) continue;

                var link = new Connection
                {
                    Name = MakeUniqueConnectionName($"Mirror-{axisZone.Name}-{nearest.Name}"),
                    From = axisZone.Name,
                    To = nearest.Name,
                    ConnectionType = "Direct",
                    GuardZone = axisZone.Name,
                    GuardEscape = false,
                    SimTurnSquad = true,
                    // Same guard the generator gives a player↔medium-neutral border, so the repaired
                    // link is not a free corridor into the middle of the map.
                    GuardValue = 20000,
                    GuardWeeklyIncrement = 0.15,
                };
                Connections.Add(link);
                MirrorConnection(link);
            }
        }

        // ── Twin creation & sync hooks ───────────────────────────────────────────────

        /// <summary>
        /// Creates the mirrored copy of <paramref name="source"/> and registers the pair.
        /// Returns the twin, or <c>null</c> when the zone sits on the axis (shared, never twinned).
        /// </summary>
        private Zone? CreateTwin(Zone source, Point sourcePos)
        {
            if (IsOnMirrorAxis(sourcePos)) return null;
            if (_mirrorMap.ContainsKey(source.Name)) return TwinOf(source);

            Zone twin = CloneZone(source);
            twin.Name = MakeMirrorZoneName(StripMirrorSuffix(source.Name));

            RenumberTwinSpawns(twin);
            // The clone's biome may still copy the source zone by name; keep the twin self-contained.
            foreach (BiomeSelector? selector in new[] { twin.ZoneBiome, twin.ContentBiome, twin.MetaObjectsBiome })
                if (string.Equals(selector?.Type, "MatchZone", StringComparison.Ordinal))
                    selector!.Args = [];

            // Roads anchor on this zone's own connections, which the twin does not have yet.
            twin.Roads = twin.Roads is { Count: > 0 } ? [] : twin.Roads;

            Zones.Add(twin);
            _positions[twin.Name] = MirrorPoint(sourcePos);
            _mirrorMap[source.Name] = twin.Name;
            _mirrorMap[twin.Name] = source.Name;
            return twin;
        }

        /// <summary>"Spawn-A" → "Spawn-A (M)", then "(M2)", "(M3)" … — a name that reads as the twin.</summary>
        private string MakeMirrorZoneName(string baseName)
        {
            var existing = new HashSet<string>(Zones.Select(z => z.Name), StringComparer.Ordinal);
            string candidate = $"{baseName} (M)";
            for (int n = 2; existing.Contains(candidate); n++) candidate = $"{baseName} (M{n})";
            return candidate;
        }

        /// <summary>Removes a trailing " (M)" / " (M12)" so mirroring a twin does not stack suffixes.</summary>
        private static string StripMirrorSuffix(string name)
        {
            int open = name.LastIndexOf(" (M", StringComparison.Ordinal);
            if (open < 0 || !name.EndsWith(")", StringComparison.Ordinal)) return name;
            string inner = name[(open + 3)..^1];
            return inner.Length == 0 || inner.All(char.IsDigit) ? name[..open] : name;
        }

        /// <summary>
        /// Gives the twin's spawns the next free player slot — two zones claiming Player1 would leave
        /// one side without a start (the validator's duplicate-spawn check).
        /// </summary>
        private void RenumberTwinSpawns(Zone twin)
        {
            if (twin.MainObjects is null) return;

            var used = new HashSet<string>(
                Zones.SelectMany(z => z.MainObjects ?? [])
                     .Select(o => o.Spawn)
                     .Where(s => !string.IsNullOrEmpty(s))!,
                StringComparer.OrdinalIgnoreCase);

            foreach (MainObject mo in twin.MainObjects)
            {
                if (string.IsNullOrEmpty(mo.Spawn)) continue;
                string? free = KnownValues.SpawnPlayers.FirstOrDefault(p => !used.Contains(p));
                if (free is null) { mo.Spawn = null; continue; }
                mo.Spawn = free;
                used.Add(free);
                if (!string.IsNullOrEmpty(mo.Owner)) mo.Owner = free;
            }
        }

        /// <summary>Called right after the user adds a zone; creates its twin when mirroring is on.</summary>
        private void MirrorAfterZoneAdded(Zone z, Point pos)
        {
            if (!_mirrorMode) return;
            CreateTwin(z, pos);
        }

        /// <summary>Keeps the twin at the mirrored position while a zone is dragged.</summary>
        private void MirrorAfterZoneMoved(Zone z, Point pos)
        {
            if (!_mirrorMode) return;
            Zone? twin = TwinOf(z);
            if (twin is null) return;
            Point mirrored = MirrorPoint(pos);
            _positions[twin.Name] = mirrored;
            RepositionZone(twin, mirrored);
        }

        /// <summary>Returns the twin that should be deleted alongside <paramref name="z"/>.</summary>
        private Zone? MirrorTwinToDelete(Zone z)
        {
            if (!_mirrorMode) return null;
            Zone? twin = TwinOf(z);
            if (twin is null) return null;
            _mirrorMap.Remove(z.Name);
            _mirrorMap.Remove(twin.Name);
            return twin;
        }

        /// <summary>Duplicates a freshly created connection between the two twins.</summary>
        private void MirrorAfterConnectionAdded(Connection c)
        {
            if (!_mirrorMode || !_mirrorConnections) return;
            MirrorConnection(c);
        }

        private void MirrorConnection(Connection source)
        {
            string from = _mirrorMap.TryGetValue(source.From, out string? mf) ? mf : source.From;
            string to = _mirrorMap.TryGetValue(source.To, out string? mt) ? mt : source.To;

            // Both endpoints on the axis → the link IS its own mirror; nothing to duplicate.
            if (string.Equals(from, source.From, StringComparison.Ordinal)
             && string.Equals(to, source.To, StringComparison.Ordinal)) return;
            if (string.Equals(from, to, StringComparison.Ordinal)) return;

            bool exists = Connections.Any(c =>
                (string.Equals(c.From, from, StringComparison.Ordinal) && string.Equals(c.To, to, StringComparison.Ordinal)) ||
                (string.Equals(c.From, to, StringComparison.Ordinal) && string.Equals(c.To, from, StringComparison.Ordinal)));
            if (exists) return;

            Connection twin = JsonCloneConnection(source);
            twin.From = from;
            twin.To = to;
            twin.Name = MakeUniqueConnectionName($"{StripMirrorSuffix(source.Name ?? "Direct")}-M");
            if (string.Equals(twin.GuardZone, source.From, StringComparison.Ordinal)) twin.GuardZone = from;
            else if (string.Equals(twin.GuardZone, source.To, StringComparison.Ordinal)) twin.GuardZone = to;

            Connections.Add(twin);
        }

        private static Connection JsonCloneConnection(Connection c) =>
            System.Text.Json.JsonSerializer.Deserialize<Connection>(
                System.Text.Json.JsonSerializer.Serialize(c, SnapshotOptions), SnapshotOptions)!;

        /// <summary>
        /// Copies the edited zone's properties onto its twin, keeping the twin's own identity
        /// (name, position, player start and owner).
        /// </summary>
        private void MirrorZoneProperties(Zone source)
        {
            if (!_mirrorMode || !_mirrorProperties) return;
            Zone? twin = TwinOf(source);
            if (twin is null) return;

            var spawns = (twin.MainObjects ?? []).Select(o => (o.Spawn, o.Owner)).ToList();
            string keepName = twin.Name;

            Zone fresh = CloneZone(source);
            fresh.Name = keepName;
            fresh.Roads = twin.Roads;              // roads point at this side's own connections

            // Restore the twin's own player identity onto the copied main objects.
            if (fresh.MainObjects is not null)
                for (int i = 0; i < fresh.MainObjects.Count && i < spawns.Count; i++)
                {
                    fresh.MainObjects[i].Spawn = spawns[i].Spawn;
                    fresh.MainObjects[i].Owner = spawns[i].Owner;
                }

            foreach (BiomeSelector? selector in new[] { fresh.ZoneBiome, fresh.ContentBiome, fresh.MetaObjectsBiome })
                if (string.Equals(selector?.Type, "MatchZone", StringComparison.Ordinal)
                    && selector!.Args is { Count: > 0 } args
                    && string.Equals(args[0], source.Name, StringComparison.Ordinal))
                    selector.Args = [];

            int index = Zones.IndexOf(twin);
            if (index < 0) return;
            Zones[index] = fresh;
            _mirrorMap[source.Name] = fresh.Name;
            _mirrorMap[fresh.Name] = source.Name;
        }
    }
}
