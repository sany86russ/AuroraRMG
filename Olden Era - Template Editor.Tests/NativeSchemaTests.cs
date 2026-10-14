using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Olden_Era___Template_Editor.Models.NativeSchema;
using Olden_Era___Template_Editor.Services.NativeSchema;

namespace Olden_Era___Template_Editor.Tests;

public sealed class NativeSchemaTests
{
    private const string Schema = """
        {"unknown":{"later":[1,true,"x"]},"defaultZoneClass":[{"value":"Start "}],
        "zoneClasses":[{"name":"Start ","mainObjectClass":[{"value":"Town"}],
        "guardedContentValuePerArea":[{"conditionsAnd":{"richnesses":[0]},"weight":0.5,"value":1700},
        {"conditionsAnd":{"richnesses":[4]},"value":3500},{"fallback":true,"value":2200}],
        "arrayValue":[{"value":[true,false]},{"fallback":true,"future":"keep","value":[1,2]}]}],
        "mainObjectClasses":[{"name":"Town","type":"City"}]}
        """;

    [Fact]
    public void SchemaRoundTripPreservesUnknownFieldsAndExactIdentifierWhitespace()
    {
        var schema = NativeSchemaDocument.Parse(Schema);
        Assert.Equal(["Start "], schema.Names("zoneClasses"));
        Assert.Throws<KeyNotFoundException>(() => schema.Find("zoneClasses", "Start"));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Schema), JsonNode.Parse(schema.ToJson())));
        Assert.Empty(schema.ValidateLocalReferences());
    }

    [Fact]
    public void ConditionalEditPreservesOtherRowsConditionsAndUnknownValues()
    {
        var schema = NativeSchemaDocument.Parse(Schema);
        var zone = schema.Find("zoneClasses", "Start ");
        NativeSchemaDocument.SetConditionalValue(zone, "arrayValue", 1, JsonNode.Parse("[0.5,7]"));
        Assert.Equal("keep", zone["arrayValue"]![1]!["future"]!.GetValue<string>());
        Assert.True(zone["arrayValue"]![0]!["value"]![0]!.GetValue<bool>());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("[0.5,7]"), zone["arrayValue"]![1]!["value"]));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeSchemaDocument.SetConditionalValue(zone, "arrayValue", -1, null));
    }

    [Theory]
    [InlineData(0, 1700, false)]
    [InlineData(4, 3500, false)]
    [InlineData(2, 2200, true)]
    public void ConditionalSelectorUsesMatchingRowsBeforeFallback(int richness, int expected, bool fallback)
    {
        var schema = NativeSchemaDocument.Parse(Schema);
        var rows = NativeSchemaDocument.ReadTable(schema.Find("zoneClasses", "Start "), "guardedContentValuePerArea");
        var selection = NativeSchemaEvaluator.Select(rows, new(2, richness, NativeSchemaFlags.ClassicWinCon, 128), new Random(7));
        Assert.NotNull(selection);
        Assert.Equal(expected, selection.Value!.GetValue<int>());
        Assert.Equal(fallback, selection.UsedFallback);
    }

    [Fact]
    public void ConditionsCombineAndOrFlagsAndInclusiveBounds()
    {
        var context = new NativeSchemaContext(3, 2, NativeSchemaFlags.ClassicWinCon | NativeSchemaFlags.Symmetrical, 128, 2, 100, 3);
        var conditions = JsonNode.Parse("""{"flagOn":["ClassicWinCon","Symmetrical"],"flagOff":["SingleHero"],"zoneAreaMin":100,"zoneAreaMax":100} """);
        Assert.True(NativeSchemaEvaluator.Matches(conditions, context, false));
        Assert.False(NativeSchemaEvaluator.Matches(conditions, context with { ZoneArea = 99 }, false));
        Assert.True(NativeSchemaEvaluator.Matches(JsonNode.Parse("""{"difficulties":[0],"richnesses":[2]}"""), context, true));
        Assert.False(NativeSchemaEvaluator.Matches(JsonNode.Parse("""{"difficulties":[0],"richnesses":[2]}"""), context, false));
        Assert.True(NativeSchemaEvaluator.Matches(JsonNode.Parse("""{"difficulties":[],"zoneAreaMin":200}"""), context with { ZoneArea = -1 }, false));
        Assert.True(NativeSchemaEvaluator.Matches(new JsonObject(), context, true));
    }

    [Fact]
    public void UnknownConditionsArePreservedButCannotBeSilentlyEvaluated()
    {
        var schema = NativeSchemaDocument.Parse(Schema);
        var zone = schema.Find("zoneClasses", "Start ");
        zone["guardedContentValuePerArea"]![0]!["conditionsAnd"]!["futureRule"] = true;
        Assert.Contains("futureRule", schema.ToJson());
        var rows = NativeSchemaDocument.ReadTable(zone, "guardedContentValuePerArea");
        Assert.Throws<NotSupportedException>(() => NativeSchemaEvaluator.Select(rows, new(2, 0, NativeSchemaFlags.None, 128), new Random(1)));
    }

    [Fact]
    public void WeightedSelectionDoesNotSelectZeroWeightsAndRejectsInvalidDistribution()
    {
        var rows = new[]
        {
            new NativeConditionalEntry(0, JsonNode.Parse("""{"weight":0,"value":"zero"}""")!.AsObject()),
            new NativeConditionalEntry(1, JsonNode.Parse("""{"weight":0.25,"value":"positive"}""")!.AsObject())
        };
        var context = new NativeSchemaContext(2, 2, NativeSchemaFlags.None, 128);
        Assert.Equal("positive", NativeSchemaEvaluator.Select(rows, context, new Random(1))!.Value!.GetValue<string>());
        rows[1].Raw["weight"] = -1;
        Assert.Throws<JsonException>(() => NativeSchemaEvaluator.Select(rows, context, new Random(1)));
        rows[1].Raw["weight"] = 0;
        Assert.Throws<JsonException>(() => NativeSchemaEvaluator.Select(rows, context, new Random(1)));
    }

    [Fact]
    public void ProgrammaticNumericWeightsRemainSelectableAndValidAfterEditing()
    {
        var schema = NativeSchemaDocument.Parse(Schema);
        JsonNode[] weights = [JsonValue.Create(2)!, JsonValue.Create(3L)!, JsonValue.Create(4f)!, JsonValue.Create(5m)!, JsonValue.Create(6d)!];
        for (int index = 0; index < weights.Length; index++)
        {
            var row = new JsonObject { ["weight"] = weights[index], ["value"] = "selected" };
            schema.Find("zoneClasses", "Start ")["programmaticTable"] = new JsonArray(row);
            var entry = Assert.Single(NativeSchemaDocument.ReadTable(schema.Find("zoneClasses", "Start "), "programmaticTable"));
            Assert.Equal(index + 2d, entry.Weight);
            Assert.Equal("selected", NativeSchemaEvaluator.Select([entry], new(0, 0, 0, 128), new Random(1))!.Value!.GetValue<string>());
            Assert.Empty(schema.ValidateLocalReferences());
        }
    }

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("true")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void NonNumericWeightsFailAsJsonErrorsRatherThanClrCastErrors(string json)
    {
        var row = new NativeConditionalEntry(0, new JsonObject { ["weight"] = JsonNode.Parse(json), ["value"] = 1 });
        Assert.Throws<JsonException>(() => NativeSchemaEvaluator.Select([row], new(0, 0, 0, 128), new Random(1)));
    }

    [Fact]
    public void ProgrammaticNonfiniteWeightsCannotReachSelection()
    {
        foreach (double weight in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var row = new NativeConditionalEntry(0, new JsonObject { ["weight"] = weight, ["value"] = 1 });
            Assert.Throws<JsonException>(() => NativeSchemaEvaluator.Select([row], new(0, 0, 0, 128), new Random(1)));
        }
    }

    [Fact]
    public void MissingLocalReferencesAreReportedWithoutTrimmingIdentifiers()
    {
        var schema = NativeSchemaDocument.Parse(Schema);
        schema.Root["defaultZoneClass"]![0]!["value"] = "Start";
        Assert.Single(schema.ValidateLocalReferences());
    }

    [Fact]
    public void RawSchemaEditingCannotHideInvalidConditionalWeightsAndMissingHoldCityClass()
    {
        var schema = NativeSchemaDocument.Parse(Schema);
        schema.Find("zoneClasses", "Start ")["guardedContentValuePerArea"]![0]!["weight"] = -0.5;
        schema.Root["holdCityMainObjectClass"] = JsonNode.Parse("""[{"value":"missing"}]""");
        var findings = schema.ValidateLocalReferences();
        Assert.Contains(findings, finding => finding.Contains("weight"));
        Assert.Contains(findings, finding => finding.Contains("missing"));
    }

    [Fact]
    public void CompactConfigurationDecodesIndependentNativeBitLayoutWithoutInventingHeroLimits()
    {
        // Verified native layout: 16 flag bits, 5 size/16, 3 players, 4 richness, 4 difficulty.
        var code = Convert.ToBase64String([0x04, 0x00, 0x88, 0x32]);
        var decoded = NativeConfigurationCodec.Decode(code);
        Assert.Equal(NativeSchemaFlags.ClassicWinCon, decoded.Flags);
        Assert.Equal(128, decoded.MapSize);
        Assert.Equal(4, decoded.Players);
        Assert.Equal(2, decoded.Richness);
        Assert.Equal(3, decoded.Difficulty);
        Assert.Null(decoded.HeroCountMin);
        Assert.Null(decoded.HeroCountMax);
        Assert.True(decoded.FromCompactCode);
        Assert.Equal(code, NativeConfigurationCodec.Encode(decoded));
    }

    [Fact]
    public void CompactTournamentConfigurationDecodesVariableDays()
    {
        var code = Convert.ToBase64String([0x80, 0x00, 0x48, 0x32, 0x73, 0xA8, 0xE0, 0x00]);
        var decoded = NativeConfigurationCodec.Decode(code);
        Assert.Equal(NativeSchemaFlags.TournamentWinCon, decoded.Flags);
        Assert.Equal(new[] { 14, 21, 28 }, decoded.Raw["tournamentDays"]!.AsArray().Select(day => day!.GetValue<int>()).ToArray());
        Assert.Equal(code, NativeConfigurationCodec.Encode(decoded));
    }

    [Fact]
    public void NativeCalendarIsZeroBasedAndRawOneRepresentsSecondGameDay()
    {
        var settings = NativeConfigurationCodec.ParseJson("""{"flags":128,"size":128,"spawnCount":2,"tournamentDays":[1,8]}""");
        Assert.Equal(1, NativeConfigurationCodec.Decode(NativeConfigurationCodec.Encode(settings)).Raw["tournamentDays"]![0]!.GetValue<int>());
        Assert.Throws<JsonException>(() => NativeConfigurationCodec.ParseJson("""{"flags":128,"size":128,"spawnCount":2,"tournamentDays":[0,7]}"""));
    }

    [Fact]
    public void CompactFinalBattlePreservesRawZeroBasedDate()
    {
        var code = Convert.ToBase64String([0x40, 0x00, 0x48, 0x32, 0x01]);
        var decoded = NativeConfigurationCodec.Decode(code);
        Assert.Equal(1, decoded.Raw["finalBattleDay"]!.GetValue<int>());
        Assert.Equal(code, NativeConfigurationCodec.Encode(decoded));
    }

    [Fact]
    public void CompactConfigurationRejectsTruncationFuturePayloadsAndHeroLoss()
    {
        Assert.Throws<FormatException>(() => NativeConfigurationCodec.Decode("AA=="));
        Assert.Throws<FormatException>(() => NativeConfigurationCodec.Decode(Convert.ToBase64String([4, 0, 0x88, 0x32, 1])));
        var settings = NativeConfigurationCodec.ParseJson("""{"flags":"ClassicWinCon","size":128,"spawnCount":4,"richness":2,"difficulty":3,"heroCountMin":1,"heroCountMax":10,"future":true}""");
        Assert.True(settings.Raw["future"]!.GetValue<bool>());
        Assert.Throws<InvalidOperationException>(() => NativeConfigurationCodec.Encode(settings));
        Assert.Throws<NotSupportedException>(() => NativeConfigurationCodec.ParseJson("""{"flags":65536,"size":128,"spawnCount":4}"""));
    }

    [Fact]
    public void EightPlayersRemainRepresentableInJsonButCannotBeSilentlyTruncatedInCompactCode()
    {
        var settings = NativeConfigurationCodec.ParseJson("""{"flags":4,"size":256,"spawnCount":8,"richness":2,"difficulty":3}""");
        Assert.Equal(8, settings.Players);
        Assert.Throws<InvalidOperationException>(() => NativeConfigurationCodec.Encode(settings));
    }

    [Fact]
    public void DebugConfigurationBooleansAreImportedAndUnknownPayloadIsRetained()
    {
        var settings = NativeConfigurationCodec.ParseJson("""{"seed":17,"size":128,"spawnCount":2,"richness":4,"difficulty":5,"singleHero":true,"classicWinCon":true,"symmetrical":true,"spawnFactions":["temple","dungeon"]}""");
        Assert.Equal(NativeSchemaFlags.SingleHero | NativeSchemaFlags.ClassicWinCon | NativeSchemaFlags.Symmetrical, settings.Flags);
        Assert.Equal(17, settings.Raw["seed"]!.GetValue<int>());
        Assert.Equal("dungeon", settings.Raw["spawnFactions"]![1]!.GetValue<string>());
        Assert.Throws<JsonException>(() => NativeConfigurationCodec.ParseJson(Schema));
    }

    [Theory]
    [InlineData(2000, 1700, 100, 19000)]
    [InlineData(2000, 1700, 0, 2000)]
    [InlineData(0, 5, 1, 0)]
    [InlineData(0, 15, 1, 2)]
    public void NativeAreaBudgetUsesTenEligibleFreeCellsAndEvenRounding(int absolute, int coefficient, int cells, int expected)
        => Assert.Equal(expected, NativeAreaBudget.Calculate(absolute, coefficient, cells));

    [Fact]
    public void NativeAreaBudgetRejectsOverflowAndNegativeInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeAreaBudget.Calculate(0, 1, -1));
        Assert.Throws<OverflowException>(() => NativeAreaBudget.Calculate(0, int.MaxValue, 2));
    }

    [Fact]
    public void AiCatalogUsesDatabaseIdsRatherThanFilenamesAndKeepsRoleEvidence()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aurora-ai-test-{Guid.NewGuid():N}.zip");
        try
        {
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(archive.CreateEntry("DB/ai_world/ai/multi_hero_1_easy.json").Open()))
                    writer.Write("""{"array":[{"id":"multi_hero_easy","strategies":[{"roles":["main"],"actionsBefore":[{"sid":"cityBuilder"}]}]}]}""");
                using (var writer = new StreamWriter(archive.CreateEntry("DB/ai_world/ai/campaign/test.json").Open()))
                    writer.Write("""{"array":[{"id":"campaign_only","strategies":[]}]}""");
            }
            var rows = NativeAiCatalog.Read(path);
            var profile = Assert.Single(rows);
            Assert.Equal("multi_hero_easy", profile.Id);
            Assert.Equal(["main"], profile.Roles);
            Assert.Equal(["cityBuilder"], profile.Actions);
            Assert.Equal(2, NativeAiCatalog.Read(path, true).Count);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"array\":[null]}")]
    [InlineData("{\"array\":[{\"id\":\"known\",\"strategies\":null}]}")]
    public void AiArchiveOverloadReportsMalformedShapesWithoutDisposingCallerArchive(string json)
    {
        using var stream = new MemoryStream();
        using (var writing = new ZipArchive(stream, ZipArchiveMode.Create, true))
        using (var writer = new StreamWriter(writing.CreateEntry("DB/ai_world/ai/test.json").Open())) writer.Write(json);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
        Assert.Throws<JsonException>(() => NativeAiCatalog.Read(archive));
        Assert.Single(archive.Entries); Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData(1e300)]
    [InlineData(1e-300)]
    public void NativeConditionalWeightsRejectFloatOverflowAndPositiveUnderflow(double weight)
    {
        var owner = new JsonObject { ["budget"] = new JsonArray(new JsonObject { ["weight"] = weight, ["value"] = 1 }) };
        Assert.Throws<JsonException>(() => NativeSchemaEvaluator.Select(NativeSchemaDocument.ReadTable(owner, "budget"), new(0, 0, 0, 128), new Random(1)));
        var schema = NativeSchemaDocument.Parse($$"""{"zoneClasses":[{"name":"Zone","valueTable":[{"weight":{{weight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}},"value":1}]}],"mainObjectClasses":[]}""");
        Assert.Contains(schema.ValidateLocalReferences(), item => item.Contains("weight", StringComparison.Ordinal));
    }

    [Fact]
    public void NativeConditionalWeightSumCannotOverflowItsNativeFloatRange()
    {
        var rows = new[] { new NativeConditionalEntry(0, new JsonObject { ["weight"] = (double)float.MaxValue, ["value"] = 1 }),
            new NativeConditionalEntry(1, new JsonObject { ["weight"] = (double)float.MaxValue, ["value"] = 2 }) };
        Assert.Throws<JsonException>(() => NativeSchemaEvaluator.Select(rows, new(0, 0, 0, 128), new Random(1)));
        var schema = NativeSchemaDocument.Parse("""{"zoneClasses":[{"name":"Zone","table":[{"weight":3e38,"value":1},{"weight":3e38,"value":2}]}],"mainObjectClasses":[]}""");
        Assert.Contains(schema.ValidateLocalReferences(), item => item.Contains("sum", StringComparison.Ordinal));
    }
}
