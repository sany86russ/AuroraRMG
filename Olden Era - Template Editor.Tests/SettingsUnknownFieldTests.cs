using System.Text.Json;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;

namespace Olden_Era___Template_Editor.Tests;

public class SettingsUnknownFieldTests
{
    [Fact]
    public void FullSnapshotsAndProfileClonesRetainFutureInputs()
    {
        var input = JsonSerializer.Deserialize<GeneratorSettings>("""{"FutureInput":{"option":3},"ProfileOptions":{"FutureProfile":{"value":4}}}""")!;
        var restored = Presets.ToGeneratorSettings(SettingsSnapshotMapper.FromGeneratorSettings(input));
        Assert.Equal(3, restored.Extra!["FutureInput"].GetProperty("option").GetInt32());
        Assert.Equal(4, restored.ProfileOptions.Clone().Extra!["FutureProfile"].GetProperty("value").GetInt32());
    }

    [Fact]
    public void SettingsFileRoundTripRetainsFutureEnvelopeMetadata()
    {
        var input = JsonSerializer.Deserialize<SettingsFile>("""{"futureEnvelope":{"k":9}}""")!;
        var restored = JsonSerializer.Deserialize<SettingsFile>(JsonSerializer.Serialize(input))!;
        Assert.Equal(9, restored.Extra!["futureEnvelope"].GetProperty("k").GetInt32());
    }
}
