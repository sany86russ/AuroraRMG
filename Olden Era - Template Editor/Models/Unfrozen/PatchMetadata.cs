using System.Collections.Generic;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OldenEraTemplateEditor.Models;

/// <summary>Player-slot metadata. -1 keeps this slot independent; positive indices identify teams.</summary>
public class SpawnInfo : RmgNode
{
    [JsonPropertyName("spawn")]
    public string Spawn { get; set; } = string.Empty;

    [JsonPropertyName("allianceIndex")]
    public int? AllianceIndex { get; set; }

    [JsonPropertyName("isAllianceLocked")]
    public bool? IsAllianceLocked { get; set; }
}

/// <summary>Game picker description. Every block retains fields introduced by future patches.</summary>
public class DescriptionData : RmgNode
{
    [JsonPropertyName("blocks")]
    public List<DescriptionBlock>? Blocks { get; set; }
}

public class DescriptionBlock : RmgNode
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("p")]
    public List<string>? Parameters { get; set; }
}

public class RiverSettings : RmgNode
{
    [JsonPropertyName("createRiverSystem")]
    public bool? CreateRiverSystem { get; set; }

    [JsonPropertyName("tributarySpacing")]
    public double? TributarySpacing { get; set; }
}

/// <summary>Preserves the game's accepted collapsed numeric/bool arrays without coercing strings or unknown types.</summary>
public sealed class ScalarOrArrayConverter<T> : JsonConverter<List<T>> where T : struct
{
    public override List<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.StartArray) return [JsonSerializer.Deserialize<T>(ref reader, options)];
        var values = new List<T>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray) return values;
            values.Add(JsonSerializer.Deserialize<T>(ref reader, options));
        }
        throw new JsonException("Unterminated numeric/bool array.");
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (T item in value) JsonSerializer.Serialize(writer, item, options);
        writer.WriteEndArray();
    }
}
