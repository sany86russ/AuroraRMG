using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>
    /// Remembers where the user dragged each zone in the visual editor.
    /// <para>
    /// Without this, positions are re-derived from the topology on every load, so a hand-arranged
    /// graph is thrown away the moment the template is saved and reopened. The positions are
    /// deliberately <b>not</b> written into the <c>.rmg.json</c> itself — that file is consumed by the
    /// game, and adding an editor-only block to it (or a stray file next to it in
    /// <c>map_templates</c>) risks confusing the game's template loader. Instead each template gets a
    /// sidecar under <c>%LOCALAPPDATA%\AuroraRMG\editor-layouts\</c>, keyed by the template's full
    /// path, exactly like the custom content pools.
    /// </para>
    /// Every operation is best-effort: a missing, unreadable or stale sidecar simply means the layout
    /// falls back to the computed one.
    /// </summary>
    public static class EditorLayoutStore
    {
        private static string Dir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AuroraRMG", "editor-layouts");

        /// <summary>
        /// Sidecar path for a template file: a readable prefix (so the folder can be browsed by hand)
        /// plus a hash of the full path, which keeps two same-named templates in different folders apart.
        /// </summary>
        private static string SidecarFor(string templatePath)
        {
            string full = Path.GetFullPath(templatePath);
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(full.ToLowerInvariant()));
            string shortHash = Convert.ToHexString(hash, 0, 6);

            string stem = Path.GetFileNameWithoutExtension(full);
            foreach (char bad in Path.GetInvalidFileNameChars()) stem = stem.Replace(bad, '_');
            if (stem.Length > 60) stem = stem[..60];

            return Path.Combine(Dir, $"{stem}-{shortHash}.json");
        }

        /// <summary>Stores the canvas position of every zone for the given template file.</summary>
        public static void Save(string templatePath, IReadOnlyDictionary<string, Point> positions)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var payload = new Dictionary<string, double[]>(StringComparer.Ordinal);
                foreach (var kv in positions) payload[kv.Key] = [kv.Value.X, kv.Value.Y];
                File.WriteAllText(SidecarFor(templatePath), JsonSerializer.Serialize(payload));
            }
            catch
            {
                // Layout memory is a convenience; never let it break a save.
            }
        }

        /// <summary>
        /// Returns the remembered positions for the given template file, or <c>null</c> when there are
        /// none. Zone names that no longer exist are the caller's problem to ignore — they are simply
        /// never looked up.
        /// </summary>
        public static Dictionary<string, Point>? Load(string templatePath)
        {
            try
            {
                string file = SidecarFor(templatePath);
                if (!File.Exists(file)) return null;

                var payload = JsonSerializer.Deserialize<Dictionary<string, double[]>>(File.ReadAllText(file));
                if (payload is null) return null;

                var result = new Dictionary<string, Point>(StringComparer.Ordinal);
                foreach (var kv in payload)
                    if (kv.Value is { Length: 2 } xy && !double.IsNaN(xy[0]) && !double.IsNaN(xy[1]))
                        result[kv.Key] = new Point(xy[0], xy[1]);

                return result.Count > 0 ? result : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
