using System.Collections.Generic;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>
    /// Pure validation of a zone graph (zones + connections) for the visual editor.
    /// Surfaces the issues that break a template in-game: missing names, duplicate
    /// names, dangling connection endpoints, self-loops and isolated zones.
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

            foreach (var c in connections)
            {
                if (!names.Contains(c.From)) issues.Add(L("S.V.Dangling", c.From));
                if (!names.Contains(c.To))   issues.Add(L("S.V.Dangling", c.To));
                if (string.Equals(c.From, c.To, System.StringComparison.Ordinal) && !string.IsNullOrEmpty(c.From))
                    issues.Add(L("S.V.SelfLoop", c.From));
            }

            if (zones.Count > 1)
            {
                var connected = new HashSet<string>(System.StringComparer.Ordinal);
                foreach (var c in connections) { connected.Add(c.From); connected.Add(c.To); }
                foreach (var z in zones)
                    if (!string.IsNullOrWhiteSpace(z.Name) && !connected.Contains(z.Name))
                        issues.Add(L("S.V.Isolated", z.Name));
            }

            return issues;
        }
    }
}
