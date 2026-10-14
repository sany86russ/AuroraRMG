using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Services.Analysis;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public sealed class TemplateJsonReaderTests
{
    [Theory]
    [InlineData("{\"name\":\"first\",\"name\":\"second\"}", "name", "$.name")]
    [InlineData("{\"name\":\"same\",\"name\":\"same\"}", "name", "$.name")]
    [InlineData("{\"name\":\"same\",\"\\u006eame\":\"same\"}", "name", "$.name")]
    [InlineData("{\"variants\":[{\"future\":{\"weight\":1,\"weight\":2}}]}", "weight", "$.variants[0].future.weight")]
    public void RepeatedPropertiesAreRejectedBeforeTypedOrUnknownValuesCanBeDiscarded(string json, string property, string path)
    {
        var error = Assert.Throws<DuplicateJsonPropertyException>(() => TemplateJsonReader.Deserialize(json, JsonExport.Options));
        Assert.Equal(property, error.PropertyName); Assert.Equal(path, error.Path);
    }

    [Fact]
    public void PropertyMatchingIsOrdinalAndRepeatedWordsInsideStringsDoNotBecomeProperties()
    {
        const string json = """{"name":"Exact ","future":{"name":1,"Name":2},"description":"\"name\":\"word\", \"name\":\"word\"","sizeX":128,"sizeZ":128}""";
        var result = TemplateJsonReader.Deserialize(json, JsonExport.Options)!;
        Assert.Equal("Exact ", result.Name);
        var future = result.Extra!["future"];
        Assert.Equal(1, future.GetProperty("name").GetInt32()); Assert.Equal(2, future.GetProperty("Name").GetInt32());
    }

    [Fact]
    public void GuardAlsoRejectsDuplicatesInRawObjectArrayFragments()
    {
        var error = Assert.Throws<DuplicateJsonPropertyException>(() => TemplateJsonReader.GuardBeforeDeserialize("""[{"type":"first","type":"second"}]"""));
        Assert.Equal("$[0].type", error.Path);
    }

    [Fact]
    public void ConflictingMandatoryArraysAreRejectedWithoutWritingOrPretendingTheRoundTripIsLossless()
    {
        const string source = """{"name":"Ambiguous","sizeX":128,"sizeZ":128,"mandatoryContent":[{"name":"First","content":[{"sid":"one"}]},{"name":"Second"}],"mandatoryContent":[{"name":"Replacement"}]}""";
        string path = Path.Combine(Path.GetTempPath(), "aurora-duplicate-test-" + Guid.NewGuid().ToString("N") + ".rmg.json");
        try
        {
            File.WriteAllText(path, source, new UTF8Encoding(false)); byte[] hash = SHA256.HashData(File.ReadAllBytes(path));
            var error = Assert.Throws<DuplicateJsonPropertyException>(() => TemplateJsonReader.Deserialize(File.ReadAllText(path), JsonExport.Options));
            Assert.Equal("mandatoryContent", error.PropertyName); Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(path)));
            var unsafeLastValue = JsonSerializer.Deserialize<RmgTemplate>(source, JsonExport.Options)!;
            var comparison = TemplateRoundTripAudit.Compare(source, JsonSerializer.Serialize(unsafeLastValue, JsonExport.Options));
            Assert.False(comparison.PreservesValues);
            Assert.Contains(comparison.Differences, difference => difference.Path == "$.mandatoryContent" && difference.Message == "Array length changed.");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RejectedInputReceiptIsSeparateFromPreservedValuesAndNeedsEvidence()
    {
        var rejected = new PatchCorpusReceipt("ambiguous.rmg.json", new string('A', 64), 0, false,
            [new("$.mandatoryContent", "Repeated property")], [], [], null)
        { SafelyRejected = true, RejectionReason = "Duplicate property was rejected by the shared input reader; source hash unchanged." };
        var receipt = new PatchAcceptanceReceipt("output", DateTime.UtcNow, "core", "schema", "test", "test", [rejected],
            [new("case", "test", 1, true, "hash", [], null, null, null)], false);
        Assert.True(receipt.OfflinePassed); Assert.False(rejected.PreservesValues);
        Assert.False((receipt with { Corpus = [rejected with { RejectionReason = null }] }).OfflinePassed);
        Assert.False((receipt with { Corpus = [rejected with { Differences = [] }] }).OfflinePassed);
        Assert.False((receipt with { Corpus = [rejected with { SourceSha256 = "" }] }).OfflinePassed);
        Assert.False((receipt with { Corpus = [rejected with { Error = "Source changed" }] }).OfflinePassed);
    }
}
