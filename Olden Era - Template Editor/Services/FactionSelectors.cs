using System.Globalization;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services;

/// <summary>Faction arguments are ordered strings; a space belongs to an argument, not a separator.</summary>
public static class FactionSelectors
{
    public static List<string> ParseArguments(string text) => text
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

    public static void SetType(MainObject target, string type)
    {
        target.Faction ??= new TypedSelector();
        target.Faction.Type = type;
        // Changing a selector must not silently discard user-entered or imported arguments.
    }

    public static void SetReference(MainObject target, IReadOnlyList<Zone> zones,
        string zoneName, int objectIndex, bool different)
    {
        var zone = zones.FirstOrDefault(z => z.Name == zoneName);
        if (objectIndex < 0 || zone?.MainObjects is not { } objects || objectIndex >= objects.Count)
            throw new ArgumentException(nameof(objectIndex));
        if (ReferenceEquals(objects[objectIndex], target)) throw new ArgumentException(nameof(target));
        SetType(target, different ? "FromList" : "Match");
        string index = objectIndex.ToString(CultureInfo.InvariantCulture);
        target.Faction!.Args = different ? [$"differentFrom: {index} {zoneName}"] : [index, zoneName];
        // A matching faction does not imply ownership: neutral towns stay neutral.
    }

    public static void RenameZone(TypedSelector? selector, string oldName, string newName)
    {
        if (selector?.Args is not { } args) return;
        if (selector.Type == "Match" && args.Count >= 2 && args[1] == oldName) args[1] = newName;
        if (selector.Type != "FromList") return;
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i]?.StartsWith("differentFrom:", StringComparison.Ordinal) != true) continue;
            var parts = args[i]["differentFrom:".Length..].Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[1] == oldName)
                args[i] = $"differentFrom: {parts[0]} {newName}";
        }
    }

    /// <summary>Remaps each reference once, including bidirectional mirror maps.</summary>
    public static void RemapZones(TypedSelector? selector, IReadOnlyDictionary<string, string> names)
    {
        if (selector?.Args is not { } args) return;
        if (selector.Type == "Match" && args.Count >= 2 && args[1] is { } zone && names.TryGetValue(zone, out var target))
            args[1] = target;
        if (selector.Type != "FromList") return;
        for (int i = 0; i < args.Count; i++)
            if (TryExclusion(args[i], out var index, out var name) && name is not null && names.TryGetValue(name, out var replacement))
                args[i] = $"differentFrom: {index} {replacement}";
    }

    private static bool TryExclusion(string? arg, out string index, out string? zone)
    {
        index = ""; zone = null;
        if (arg?.StartsWith("differentFrom:", StringComparison.Ordinal) != true) return false;
        var parts = arg["differentFrom:".Length..].Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        index = parts[0]; zone = parts.Length > 1 ? parts[1] : null;
        return true;
    }

    /// <summary>Repairs references after deleting an object (null index means the whole zone).</summary>
    public static int RemoveSource(TypedSelector? selector, string currentZone, string removedZone, int? removedIndex)
    {
        if (selector?.Args is not { } args) return 0;
        if (selector.Type == "Match" && args.Count > 0 && (args.Count > 1 ? args[1] : currentZone) == removedZone)
        {
            if (removedIndex is null || int.TryParse(args[0], out int index) && index == removedIndex)
            { selector.Type = "Random"; selector.Args = []; return 1; }
            if (int.TryParse(args[0], out int later) && later > removedIndex) args[0] = (later - 1).ToString(CultureInfo.InvariantCulture);
        }
        int removed = 0;
        if (selector.Type != "FromList") return removed;
        for (int i = args.Count - 1; i >= 0; i--)
        {
            if (!TryExclusion(args[i], out var indexText, out var name) || (name ?? currentZone) != removedZone) continue;
            if (removedIndex is null || int.TryParse(indexText, out int index) && index == removedIndex)
            { args.RemoveAt(i); removed++; }
            else if (int.TryParse(indexText, out int later) && later > removedIndex)
                args[i] = $"differentFrom: {later - 1}" + (name is null ? "" : $" {name}");
        }
        if (removed > 0 && args.Count == 0) selector.Type = "Random";
        return removed;
    }

    public static IEnumerable<string> ValidateReferences(IReadOnlyList<Zone> zones)
    {
        var named = zones.Where(z => !string.IsNullOrWhiteSpace(z.Name))
            .GroupBy(z => z.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var matches = new Dictionary<MainObject, MainObject>();
        foreach (var zone in zones)
        foreach (var obj in zone.MainObjects ?? [])
        {
            var selector = obj.Faction;
            var args = selector?.Args ?? [];
            var refs = new List<(string Index, string Zone)>();
            if (selector?.Type == "Match") refs.Add((args.FirstOrDefault() ?? "", args.Count > 1 ? args[1] : zone.Name));
            if (selector?.Type == "FromList")
                foreach (var arg in args.Where(a => a?.StartsWith("differentFrom:", StringComparison.Ordinal) == true))
                {
                    var parts = arg["differentFrom:".Length..].Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    refs.Add((parts.FirstOrDefault() ?? "", parts.Length > 1 ? parts[1] : zone.Name));
                }
            foreach (var reference in refs)
            {
                if (!int.TryParse(reference.Index, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                    || index < 0 || reference.Zone is null || !named.TryGetValue(reference.Zone, out var source)
                    || source.MainObjects is not { } objects || index >= objects.Count
                    || ReferenceEquals(obj, objects[index]))
                    yield return Localization.LocalizationManager.T("S.Faction.InvalidRef", zone.Name, reference.Index, reference.Zone ?? "");
                else if (selector?.Type == "Match") matches[obj] = objects[index];
            }
        }
        var finished = new HashSet<MainObject>();
        foreach (var start in matches.Keys)
        {
            var path = new HashSet<MainObject>();
            var current = start;
            while (!finished.Contains(current) && matches.TryGetValue(current, out var next))
            {
                if (!path.Add(current))
                {
                    yield return Localization.LocalizationManager.T("S.Faction.Cycle");
                    break;
                }
                current = next;
            }
            finished.UnionWith(path);
        }
    }
}
