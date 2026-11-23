using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services.GameData;

namespace Olden_Era___Template_Editor.Tests;

public class GameProfileTests
{
    [Theory]
    [InlineData("0.81.04", "25672315", GenerationProfileOptions.PreviousProfile, GameCompatibilityProfile.InspectedCoreSha256)]
    [InlineData("0.82.10", "26640613", GenerationProfileOptions.CurrentProfile, GameCompatibilityProfile.InspectedCore08210Sha256)]
    public void AuditedBuild_HasDataCapabilitiesAndSeparateGameValidationBoundary(string version, string build, string id, string core)
    {
        var profile = GameCompatibilityProfile.FromEvidence(version, build, core.ToLowerInvariant(), GameCompatibilityProfile.InspectedSchemaSha256);
        Assert.Equal(id, profile.Id);
        Assert.Equal(GameProfileStatus.InspectedGameData, profile.Status);
        Assert.True(profile.Supports(GameCapability.Alliances));
        Assert.True(profile.Supports(GameCapability.Native256Maps));
        Assert.Contains("Game-level", profile.Evidence);
    }
    [Theory]
    [InlineData("0.81.04", "different-build")]
    [InlineData("0.82.00", "25672315")]
    [InlineData("0.82.10", "26615913")]
    [InlineData("0.81.04", "26640613")]
    [InlineData(null, null)]
    public void UnknownVersions_AreUnverifiedWithoutClaimedCapabilities(string? version, string? build)
    {
        var profile = GameCompatibilityProfile.FromEvidence(version, build);
        Assert.Equal(GameProfileStatus.UnknownVersion, profile.Status);
        Assert.Empty(profile.ConfirmedCapabilities);
        Assert.Contains("generation remains available", profile.Evidence);
    }
    [Fact]
    public void MissingInstallation_ExplicitlyUsesLegacyOfflineProfile()
    {
        using var fixture = new SyntheticInstalledGame();
        var service = new GameInstallationService(Array.Empty<string>());
        Assert.Equal(GameProfileStatus.LegacyAurora, service.GetProfile().Status);
        Assert.Equal("aurora-legacy", service.GetProfile().Id);
    }
    [Theory]
    [InlineData("0.81.04", "25672315", null, null)]
    [InlineData("0.81.04", "25672315", "changed-core", "changed-schema")]
    [InlineData("0.82.10", "26640613", GameCompatibilityProfile.InspectedCoreSha256, GameCompatibilityProfile.InspectedSchemaSha256)]
    [InlineData("0.82.10", "26640613", GameCompatibilityProfile.InspectedCore08210Sha256, "changed-schema")]
    public void RecognizedVersionWithDifferentDataCannotClaimInspectedCapabilities(string version, string build, string? core, string? schema)
    {
        var profile = GameCompatibilityProfile.FromEvidence(version, build, core, schema);
        Assert.Equal("olden-era-unverified-data", profile.Id);
        Assert.Empty(profile.ConfirmedCapabilities);
        Assert.Contains("fingerprints differ", profile.Evidence);
    }
}
