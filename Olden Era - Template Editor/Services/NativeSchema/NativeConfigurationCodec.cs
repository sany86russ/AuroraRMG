using System.Text.Json;
using System.Text.Json.Nodes;
using Olden_Era___Template_Editor.Models.NativeSchema;

namespace Olden_Era___Template_Editor.Services.NativeSchema;

/// <summary>
/// Imports native JSON settings and the game's compact settings code.
/// The compact format describes inputs, not a generated map; hero limits are absent.
/// </summary>
public static class NativeConfigurationCodec
{
    private static readonly (string Property, NativeSchemaFlags Flag)[] DebugFlags =
    [
        ("singleHero", NativeSchemaFlags.SingleHero), ("symmetrical", NativeSchemaFlags.Symmetrical),
        ("classicWinCon", NativeSchemaFlags.ClassicWinCon), ("holdCapitalWinCon", NativeSchemaFlags.HoldCapitalWinCon),
        ("holdCityWinCon", NativeSchemaFlags.HoldCityWinCon), ("startingHeroWinCon", NativeSchemaFlags.StartingHeroWinCon),
        ("finalBattleWinCon", NativeSchemaFlags.FinalBattleWinCon), ("tournamentWinCon", NativeSchemaFlags.TournamentWinCon)
    ];

    public static NativeGeneratorConfiguration ParseJson(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject
            ?? throw new JsonException("Native generator settings must be an object.");
        // Requiring both distinguishes native inputs from map/save/schema objects.
        if (root["size"] is null || root["spawnCount"] is null)
            throw new JsonException("Native settings require size and spawnCount.");
        var flags = root["flags"] switch
        {
            null => NativeSchemaFlags.None,
            JsonValue value when value.TryGetValue<string>(out var text) => NativeSchemaEvaluator.ParseFlags(text),
            _ => ParseNumericFlags(root["flags"]!.GetValue<int>())
        };
        foreach (var (property, flag) in DebugFlags)
        {
            if (root[property] is null) continue;
            flags = root[property]!.GetValue<bool>() ? flags | flag : flags & ~flag;
        }
        root["flags"] = (int)flags;
        var result = new NativeGeneratorConfiguration(root, false);
        Validate(result);
        return result;
    }

    public static NativeGeneratorConfiguration Decode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var bytes = Convert.FromBase64String(code);
        if (bytes.Length > 16) throw new FormatException("Native settings code exceeds the verified 16-byte buffer.");
        var bit = 0;
        var flags = (NativeSchemaFlags)Read(16);
        var root = new JsonObject
        {
            ["flags"] = (int)flags,
            ["size"] = Read(5) * 16,
            ["spawnCount"] = Read(3),
            ["richness"] = Read(4),
            ["difficulty"] = Read(4)
        };
        if (flags.HasFlag(NativeSchemaFlags.FinalBattleWinCon)) root["finalBattleDay"] = Read(8);
        if (flags.HasFlag(NativeSchemaFlags.TournamentWinCon))
        {
            var count = Read(3);
            if (count == 0) throw new FormatException("Native tournament settings require at least one battle day.");
            var days = new JsonArray();
            for (var index = 0; index < count; index++) days.Add(Read(8));
            root["tournamentDays"] = days;
        }
        // A future format must not be silently mistaken for this verified layout.
        if (bytes.Length != (bit + 7) / 8)
            throw new FormatException("Native settings code contains unrecognized trailing bytes.");
        for (var padding = bit; padding < bytes.Length * 8; padding++)
            if ((bytes[padding / 8] & (1 << (padding % 8))) != 0)
                throw new FormatException("Native settings code contains nonzero padding bits.");
        var result = new NativeGeneratorConfiguration(root, true);
        Validate(result);
        return result;

        int Read(int count)
        {
            if (bit + count > bytes.Length * 8) throw new FormatException("Native settings code is truncated.");
            var value = 0;
            for (var offset = 0; offset < count; offset++, bit++)
                value |= ((bytes[bit / 8] >> (bit % 8)) & 1) << offset;
            return value;
        }
    }

    /// <summary>
    /// Encodes only representable native inputs. Rejects hero limits rather than silently losing them.
    /// The game's 3-bit count cannot represent eight players; JSON inputs can.
    /// </summary>
    public static string Encode(NativeGeneratorConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Validate(configuration);
        var encodedFields = new HashSet<string>(StringComparer.Ordinal)
        { "flags", "size", "spawnCount", "richness", "difficulty", "finalBattleDay", "tournamentDays" };
        if (configuration.Raw.Any(property => property.Value is not null && !encodedFields.Contains(property.Key)))
            throw new InvalidOperationException("The native compact format cannot preserve all JSON settings (including hero limits and seed); export JSON instead.");
        var bytes = new byte[16];
        var bit = 0;
        Write((int)configuration.Flags, 16);
        if (configuration.MapSize % 16 != 0)
            throw new InvalidOperationException("Native compact map size must be divisible by 16.");
        Write(configuration.MapSize / 16, 5);
        Write(configuration.Players, 3);
        Write(configuration.Richness, 4);
        Write(configuration.Difficulty, 4);
        if (configuration.Flags.HasFlag(NativeSchemaFlags.FinalBattleWinCon))
            Write(configuration.Raw["finalBattleDay"]!.GetValue<int>(), 8);
        if (configuration.Flags.HasFlag(NativeSchemaFlags.TournamentWinCon))
        {
            var days = configuration.Raw["tournamentDays"]!.AsArray();
            Write(days.Count, 3);
            foreach (var day in days) Write(day!.GetValue<int>(), 8);
        }
        return Convert.ToBase64String(bytes.AsSpan(0, (bit + 7) / 8));

        void Write(int value, int count)
        {
            if (value < 0 || value >= 1 << count)
                throw new InvalidOperationException($"Value {value} does not fit the native {count}-bit field.");
            if (bit + count > bytes.Length * 8) throw new InvalidOperationException("Native settings code is too large.");
            for (var offset = 0; offset < count; offset++, bit++)
                bytes[bit / 8] |= (byte)(((value >> offset) & 1) << (bit % 8));
        }
    }

    public static void Validate(NativeGeneratorConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if ((configuration.Flags & ~(NativeSchemaFlags)511) != 0)
            throw new NotSupportedException("Native settings contain unknown flags.");
        if (configuration.MapSize <= 0)
            throw new JsonException("Native generator map size must be positive.");
        if (configuration.Players is < 2 or > 8)
            throw new JsonException("Native generator player count must be between two and eight.");
        if (configuration.Richness is < 0 or > 4 || configuration.Difficulty is < 0 or > 5)
            throw new JsonException("Native richness or difficulty is outside its verified range.");
        if (configuration.HeroCountMin is < 1 || configuration.HeroCountMax is < 1
            || configuration.HeroCountMin > configuration.HeroCountMax)
            throw new JsonException("Native hero limits are invalid.");
        if (configuration.Flags.HasFlag(NativeSchemaFlags.FinalBattleWinCon)
            && (configuration.Raw["finalBattleDay"] is not JsonValue finalDay || !finalDay.TryGetValue<int>(out var finalBattleDay) || finalBattleDay < 1))
            throw new JsonException("Native final battle requires a zero-based day greater than zero.");
        if (configuration.Flags.HasFlag(NativeSchemaFlags.TournamentWinCon))
        {
            if (configuration.Raw["tournamentDays"] is not JsonArray days || days.Count == 0)
                throw new JsonException("Native tournament requires battle days.");
            var last = 0;
            foreach (var value in days)
            {
                var day = value?.GetValue<int>() ?? throw new JsonException("Native tournament contains a null day.");
                if (day <= last) throw new JsonException("Native zero-based battle days must increase and cannot be zero (the first game day).");
                last = day;
            }
        }
    }

    private static NativeSchemaFlags ParseNumericFlags(int value)
    {
        if (value < 0 || value > 511) throw new NotSupportedException("Native settings contain unknown flags.");
        return (NativeSchemaFlags)value;
    }
}
