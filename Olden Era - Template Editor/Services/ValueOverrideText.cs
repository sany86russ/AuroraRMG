using System.Globalization;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services;

internal static class ValueOverrideText
{
    public static List<ValueOverride>? Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var result = new List<ValueOverride>();
        foreach (string line in raw.Split('\n'))
        {
            int equals = line.IndexOf('=');
            if (equals <= 0 || !TryParseTarget(line[..equals], out string sid, out int variant) ||
                !int.TryParse(line[(equals + 1)..], out int guardValue)) continue;
            result.Add(new() { Sid = sid, Variant = variant, GuardValue = guardValue });
        }
        return result.Count > 0 ? result : null;
    }

    public static bool TryParseTarget(string target, out string sid, out int variant)
    {
        sid = target.Trim();
        variant = -1;
        if (sid.Length == 0) return false;
        int start = sid.LastIndexOf('[');
        if (!sid.EndsWith(']')) return start < 0 && !sid.Contains(']');
        if (start <= 0 || !int.TryParse(sid.AsSpan(start + 1, sid.Length - start - 2),
            NumberStyles.Integer, CultureInfo.InvariantCulture, out variant) || variant < -1)
            return false;
        sid = sid[..start];
        return !string.IsNullOrWhiteSpace(sid);
    }

    public static string FormatTarget(string sid, int variant)
        => variant >= 0 || sid.Length != sid.TrimEnd().Length
            ? string.Create(CultureInfo.InvariantCulture, $"{sid}[{variant}]")
            : sid;
}
