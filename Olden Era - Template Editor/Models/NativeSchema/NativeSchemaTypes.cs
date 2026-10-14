using System.Text.Json;
using System.Text.Json.Nodes;

namespace Olden_Era___Template_Editor.Models.NativeSchema;

/// <summary>Native schema flags, distinct from Aurora generator options.</summary>
[Flags]
public enum NativeSchemaFlags : ushort
{
    None = 0,
    SingleHero = 1,
    Symmetrical = 2,
    ClassicWinCon = 4,
    HoldCapitalWinCon = 8,
    HoldCityWinCon = 16,
    StartingHeroWinCon = 32,
    FinalBattleWinCon = 64,
    TournamentWinCon = 128,
    RandomTeams = 256
}

/// <summary>Negative context quantities are unavailable, as in the game's conditional selector.</summary>
public sealed record NativeSchemaContext(
    int Difficulty,
    int Richness,
    NativeSchemaFlags Flags,
    int MapSize,
    int PrimarySpawnDistance = -1,
    int ZoneArea = -1,
    int ProgressionPoints = -1);

/// <summary>A detached parameter row; Raw retains unknown fields without normalizing identifiers.</summary>
public sealed record NativeConditionalEntry(int Index, JsonObject Raw)
{
    public JsonNode? Value => Raw["value"]?.DeepClone();
    public bool IsFallback => Raw["fallback"]?.GetValue<bool>() ?? false;
    public double Weight => Raw["weight"] is not { } value ? 1 : TryReadNumber(value, out var weight)
        ? weight : throw new JsonException("A conditional weight must be a JSON number.");

    // Parsed JSON uses JsonElement-backed values; programmatic edits may use any CLR numeric type.
    // GetValue<double>() alone rejects a valid JsonValue<int> rather than converting its number.
    internal static bool TryReadNumber(JsonNode? node, out double number)
    {
        number = 0;
        if (node is not JsonValue value) return false;
        if (value.TryGetValue<double>(out number)) return true;
        if (value.GetValueKind() != JsonValueKind.Number) return false;
        if (value.TryGetValue<int>(out var intValue)) { number = intValue; return true; }
        if (value.TryGetValue<long>(out var longValue)) { number = longValue; return true; }
        if (value.TryGetValue<uint>(out var uintValue)) { number = uintValue; return true; }
        if (value.TryGetValue<ulong>(out var ulongValue)) { number = ulongValue; return true; }
        if (value.TryGetValue<float>(out var floatValue)) { number = floatValue; return true; }
        if (value.TryGetValue<decimal>(out var decimalValue)) { number = (double)decimalValue; return true; }
        if (value.TryGetValue<short>(out var shortValue)) { number = shortValue; return true; }
        if (value.TryGetValue<ushort>(out var ushortValue)) { number = ushortValue; return true; }
        if (value.TryGetValue<byte>(out var byteValue)) { number = byteValue; return true; }
        if (value.TryGetValue<sbyte>(out var sbyteValue)) { number = sbyteValue; return true; }
        return false;
    }
}

public sealed record NativeSchemaSelection(int Index, JsonNode? Value, bool UsedFallback);

/// <summary>Raw native configuration. Compact codes do not contain hero limits.</summary>
public sealed record NativeGeneratorConfiguration(JsonObject Raw, bool FromCompactCode)
{
    public NativeSchemaFlags Flags
    {
        get
        {
            int value = Raw["flags"]?.GetValue<int>() ?? 0;
            if (value is < 0 or > 511) throw new NotSupportedException("Native settings contain unknown flags.");
            return (NativeSchemaFlags)value;
        }
    }
    public int MapSize => Raw["size"]?.GetValue<int>() ?? 0;
    public int Players => Raw["spawnCount"]?.GetValue<int>() ?? 0;
    public int Richness => Raw["richness"]?.GetValue<int>() ?? 0;
    public int Difficulty => Raw["difficulty"]?.GetValue<int>() ?? 0;
    public int? HeroCountMin => Raw["heroCountMin"]?.GetValue<int>();
    public int? HeroCountMax => Raw["heroCountMax"]?.GetValue<int>();
}

public sealed record NativeAiProfile(string Id, string SourceEntry, bool IsCampaign, bool IsCustom,
    IReadOnlyList<string> Roles, IReadOnlyList<string> Actions);
