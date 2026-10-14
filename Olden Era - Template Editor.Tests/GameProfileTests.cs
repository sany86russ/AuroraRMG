using Olden_Era___Template_Editor.Services.GameData;

namespace Olden_Era___Template_Editor.Tests;

public class GameProfileTests
{
    [Fact]
    public void AuditedBuild_HasDataCapabilitiesAndSeparateGameValidationBoundary()
    {
        var profile = GameCompatibilityProfile.FromEvidence("0.81.04", "25672315",
            GameCompatibilityProfile.InspectedCoreSha256, GameCompatibilityProfile.InspectedSchemaSha256);
        Assert.Equal(GameProfileStatus.InspectedGameData, profile.Status);
        Assert.True(profile.Supports(GameCapability.Alliances));
        Assert.True(profile.Supports(GameCapability.Native256Maps));
        Assert.Contains("Game-level", profile.Evidence);
    }
    [Theory]
    [InlineData("0.81.04", "different-build")]
    [InlineData("0.82.00", "25672315")]
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
    [InlineData(null, null)]
    [InlineData("changed-core", "changed-schema")]
    public void RecognizedVersionWithDifferentDataCannotClaimInspectedCapabilities(string? core, string? schema)
    {
        var profile = GameCompatibilityProfile.FromEvidence("0.81.04", "25672315", core, schema);
        Assert.Equal("olden-era-unverified-data", profile.Id);
        Assert.Empty(profile.ConfirmedCapabilities);
        Assert.Contains("fingerprints differ", profile.Evidence);
    }
}
