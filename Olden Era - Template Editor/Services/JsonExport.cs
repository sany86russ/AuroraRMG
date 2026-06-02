using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Olden_Era___Template_Editor.Services
{
    /// <summary>
    /// Shared <see cref="JsonSerializerOptions"/> for writing <c>.rmg.json</c> templates and
    /// <c>.oetgs</c> settings.
    /// <para>
    /// Critically, the encoder is <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> so that
    /// non-ASCII text (e.g. Cyrillic template names) is written as <b>literal UTF-8</b> instead of
    /// <c>\uXXXX</c> escapes. The game's JSON reader expects no-BOM UTF-8 and was mis-reading the
    /// escapes, which broke the template. <see cref="System.IO.File.WriteAllText(string,string)"/>
    /// already emits UTF-8 without a BOM, so together this produces game-compatible output.
    /// "Unsafe" here only refers to HTML/JS-injection contexts, which are irrelevant for a local
    /// game-template file.
    /// </para>
    /// <para>
    /// Two converters make every list field as lenient as the game's own reader — the official
    /// templates routinely collapse single-element collections to a bare value:
    /// <list type="bullet">
    /// <item><see cref="StringOrStringArrayConverter"/> — a <c>List&lt;string&gt;</c> field may be a
    /// bare string (or even a bare number), not just an array (e.g. a zone's <c>contentCountLimits</c>
    /// is a string in some zones and an array in others; rule <c>args</c> are sometimes numeric like
    /// <c>[0]</c>). This is why opening stock templates such as <c>Expanse.rmg.json</c> failed.</item>
    /// <item><see cref="SingleObjectOrArrayConverterFactory"/> — a list-of-objects field may be a
    /// single bare object instead of an array (e.g. <c>gameRules.bonuses</c> in <c>Wastelands.rmg.json</c>
    /// is one <c>{…}</c> rather than <c>[{…}]</c>).</item>
    /// </list>
    /// On write both always emit an array, preserving the generator's existing (array-only) output.
    /// </para>
    /// </summary>
    public static class JsonExport
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters =
            {
                new StringOrStringArrayConverter(),
                new SingleObjectOrArrayConverterFactory(),
            },
        };
    }

    /// <summary>
    /// Reads a JSON value that the game writes as EITHER a single scalar (string or number) OR an
    /// array of scalars into a <see cref="List{String}"/>. The official <c>.rmg.json</c> templates
    /// use both forms interchangeably for sid-list fields (e.g. a zone's <c>contentCountLimits</c> is
    /// a bare string in some zones and an array in others) and occasionally use numeric values where
    /// a string is expected (rule <c>args</c> like <c>[0]</c>). The default deserializer only accepts
    /// an array of strings, so importing such a template failed with
    /// <c>"The JSON value could not be converted to System.Collections.Generic.List`1[System.String]"</c>.
    /// On write we always emit an array of strings, preserving the generator's existing output.
    /// </summary>
    public sealed class StringOrStringArrayConverter : JsonConverter<List<string>>
    {
        public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;

                case JsonTokenType.String:
                case JsonTokenType.Number:
                    return new List<string> { ReadScalarAsString(ref reader) };

                case JsonTokenType.StartArray:
                    var list = new List<string>();
                    while (reader.Read())
                    {
                        switch (reader.TokenType)
                        {
                            case JsonTokenType.EndArray:
                                return list;
                            case JsonTokenType.String:
                            case JsonTokenType.Number:
                                list.Add(ReadScalarAsString(ref reader));
                                break;
                            default:
                                throw new JsonException(
                                    $"Unexpected token '{reader.TokenType}' inside a string-array; expected a string or number.");
                        }
                    }
                    throw new JsonException("Unterminated array while reading a string list.");

                default:
                    throw new JsonException(
                        $"Expected a string, a number, or an array of them, but got '{reader.TokenType}'.");
            }
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (string item in value)
                writer.WriteStringValue(item);
            writer.WriteEndArray();
        }

        /// <summary>Returns a string token verbatim, or a number token's raw text (e.g. <c>0</c> → <c>"0"</c>).</summary>
        private static string ReadScalarAsString(ref Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.String)
                return reader.GetString()!;

            // Number: preserve the exact written form (int vs decimal) by taking the raw UTF-8 bytes.
            ReadOnlySpan<byte> raw = reader.ValueSpan;
            if (reader.HasValueSequence)
                raw = reader.ValueSequence.ToArray();
            return System.Text.Encoding.UTF8.GetString(raw);
        }
    }

    /// <summary>
    /// Lets any <c>List&lt;T&gt;</c> of reference-type elements be deserialized from EITHER a JSON
    /// array OR a single bare object (which is wrapped into a one-element list). The game writes
    /// single-element object collections without the surrounding <c>[ ]</c> (e.g.
    /// <c>gameRules.bonuses</c> in some templates). <see cref="string"/> lists are handled by
    /// <see cref="StringOrStringArrayConverter"/> instead; value-type lists keep default behaviour.
    /// On write the list is always emitted as an array.
    /// </summary>
    public sealed class SingleObjectOrArrayConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            if (!typeToConvert.IsGenericType || typeToConvert.GetGenericTypeDefinition() != typeof(List<>))
                return false;
            Type element = typeToConvert.GetGenericArguments()[0];
            return !element.IsValueType && element != typeof(string);
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            Type element = typeToConvert.GetGenericArguments()[0];
            return (JsonConverter)Activator.CreateInstance(
                typeof(SingleObjectOrArrayConverter<>).MakeGenericType(element))!;
        }
    }

    /// <summary>Worker for <see cref="SingleObjectOrArrayConverterFactory"/>.</summary>
    public sealed class SingleObjectOrArrayConverter<T> : JsonConverter<List<T>> where T : class
    {
        public override List<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                // A single bare element — deserialize it and wrap. (Deserialize<T>, NOT List<T>,
                // so this converter is not re-entered and we don't recurse.)
                T single = JsonSerializer.Deserialize<T>(ref reader, options)!;
                return new List<T> { single };
            }

            var list = new List<T>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    return list;
                list.Add(JsonSerializer.Deserialize<T>(ref reader, options)!);
            }
            throw new JsonException("Unterminated array while reading a list of objects.");
        }

        public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (T item in value)
                JsonSerializer.Serialize(writer, item, options);
            writer.WriteEndArray();
        }
    }
}
