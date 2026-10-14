using System.Text.Json;
using System.Text.Json.Nodes;
using Olden_Era___Template_Editor.Models.NativeSchema;

namespace Olden_Era___Template_Editor.Services.NativeSchema;

/// <summary>
/// Independently implemented table evaluation from verified format behavior.
/// It does not reproduce native map geometry or native random-number consumption.
/// </summary>
public static class NativeSchemaEvaluator
{
    private static readonly HashSet<string> ConditionKeys = new(StringComparer.Ordinal)
    {
        "difficulties", "richnesses", "flagOn", "flagOff", "primarySpawnDistanceMin", "primarySpawnDistanceMax",
        "mapSizeMin", "mapSizeMax", "zoneAreaMin", "zoneAreaMax", "progressionPointsMin", "progressionPointsMax"
    };

    public static NativeSchemaSelection? Select(IReadOnlyList<NativeConditionalEntry> rows,
        NativeSchemaContext context, Random random)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(random);
        var candidates = rows.Where(row => !row.IsFallback
            && Matches(row.Raw["conditionsAnd"], context, false)
            && Matches(row.Raw["conditionsOr"], context, true)).ToArray();
        var fallback = candidates.Length == 0;
        if (fallback) candidates = rows.Where(row => row.IsFallback).ToArray();
        if (candidates.Length == 0) return null;
        if (candidates.Any(row => !NativeSchemaWeight.IsRepresentable(row.Weight)))
            throw new JsonException("Conditional weights must be nonnegative and representable as finite native floats.");
        var positive = candidates.Where(row => row.Weight > 0).ToArray();
        if (positive.Length == 0) throw new JsonException("Matching conditional rows have no positive weight.");
        NativeSchemaWeight.RequireUsableSum(positive.Select(row => row.Weight));
        // Scale before summing to avoid overflow for otherwise valid finite weights.
        var scale = positive.Max(row => row.Weight);
        var total = positive.Sum(row => row.Weight / scale);
        var point = random.NextDouble() * total;
        foreach (var row in positive)
        {
            point -= row.Weight / scale;
            if (point < 0) return new(row.Index, row.Value, fallback);
        }
        var last = positive[^1];
        return new(last.Index, last.Value, fallback);
    }

    public static bool Matches(JsonNode? conditions, NativeSchemaContext context, bool useOr)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (conditions is null) return true;
        if (conditions is not JsonObject obj) throw new JsonException("Native conditions must be an object.");
        if (obj.Any(property => !ConditionKeys.Contains(property.Key) && !property.Key.StartsWith('_')))
            throw new NotSupportedException("The schema uses an unknown condition; it is preserved but cannot be evaluated.");
        var checks = new List<bool>();
        ListMatch("difficulties", context.Difficulty);
        ListMatch("richnesses", context.Richness);
        FlagMatch("flagOn", true);
        FlagMatch("flagOff", false);
        Bound("primarySpawnDistanceMin", context.PrimarySpawnDistance, true);
        Bound("primarySpawnDistanceMax", context.PrimarySpawnDistance, false);
        Bound("mapSizeMin", context.MapSize, true);
        Bound("mapSizeMax", context.MapSize, false);
        Bound("zoneAreaMin", context.ZoneArea, true);
        Bound("zoneAreaMax", context.ZoneArea, false);
        Bound("progressionPointsMin", context.ProgressionPoints, true);
        Bound("progressionPointsMax", context.ProgressionPoints, false);
        return checks.Count == 0 || (useOr ? checks.Any(value => value) : checks.All(value => value));

        void ListMatch(string key, int actual)
        {
            if (obj[key] is null) return;
            if (obj[key] is not JsonArray values) throw new JsonException($"{key} must be an array.");
            if (values.Count == 0) return;
            checks.Add(values.Any(value => value?.GetValue<int>() == actual));
        }

        void FlagMatch(string key, bool enabled)
        {
            if (obj[key] is null) return;
            if (obj[key] is not JsonArray values) throw new JsonException($"{key} must be an array.");
            if (values.Count == 0) return;
            var flags = values.Select(value => ParseFlags(value?.GetValue<string>()
                ?? throw new JsonException($"{key} contains a null flag."))).ToArray();
            var results = flags.Select(flag => enabled
                ? (context.Flags & flag) == flag
                : (context.Flags & flag) == 0).ToArray();
            checks.Add(useOr ? results.Any(value => value) : results.All(value => value));
        }

        void Bound(string key, int actual, bool minimum)
        {
            if (obj[key] is null) return;
            var bound = obj[key]!.GetValue<int>();
            if (bound < 0 || actual < 0) return;
            checks.Add(minimum ? actual >= bound : actual <= bound);
        }
    }

    public static NativeSchemaFlags ParseFlags(string value)
    {
        if (!Enum.TryParse<NativeSchemaFlags>(value, ignoreCase: false, out var flags)
            || (flags & ~(NativeSchemaFlags)511) != 0)
            throw new NotSupportedException($"Unknown native generator flag '{value}'.");
        return flags;
    }
}
