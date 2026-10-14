using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services.Export;
using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Tests;

public class TemplateBatchTests
{
    private static GeneratorSettings Settings() => new() { TemplateName = "Batch fixture", PlayerCount = 2, MapSize = 128, Seed = 999 };
    [Fact]
    public async Task BatchIsDeterministicPerSeedAndDoesNotMutateInput()
    {
        using var fixture = new SyntheticInstalledGame(); string first = Path.Combine(fixture.Root, "first"), second = Path.Combine(fixture.Root, "second");
        Directory.CreateDirectory(first); Directory.CreateDirectory(second); var settings = Settings();
        settings.PlayerZoneMandatoryContent = [new ContentItem { Sid = "mine_wood", IsMine = true }];
        var a = await TemplateBatchService.GenerateAsync(new(settings, [11, 12], first));
        var b = await TemplateBatchService.GenerateAsync(new(settings, [11, 12], second));
        Assert.Equal(2, a.SuccessfulCount); Assert.Equal(0, a.FailedCount);
        Assert.Equal(a.Cases.Select(c => c.TemplateSha256), b.Cases.Select(c => c.TemplateSha256));
        Assert.Equal(999, settings.Seed); Assert.Single(settings.PlayerZoneMandatoryContent);
        Assert.False(a.Cancelled);
    }
    [Fact]
    public async Task BatchRejectsEmptyDuplicateAndOversizedSeedLists()
    {
        using var fixture = new SyntheticInstalledGame();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await TemplateBatchService.GenerateAsync(new(Settings(), [], fixture.Root)));
        await Assert.ThrowsAsync<ArgumentException>(async () => await TemplateBatchService.GenerateAsync(new(Settings(), [1, 1], fixture.Root)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await TemplateBatchService.GenerateAsync(new(Settings(), Enumerable.Range(0, 129).ToArray(), fixture.Root)));
    }
    [Fact]
    public async Task ExistingCaseReportsFailureWithoutOverwritingOrBlockingLaterCases()
    {
        using var fixture = new SyntheticInstalledGame(); string existing = Path.Combine(fixture.Root, "template-Batch fixture-11.rmg.json");
        File.WriteAllText(existing, "keep existing");
        var receipt = await TemplateBatchService.GenerateAsync(new(Settings(), [11, 12], fixture.Root));
        Assert.Equal(1, receipt.FailedCount); Assert.Equal(1, receipt.SuccessfulCount);
        Assert.Contains("already exists", receipt.Cases[0].Error); Assert.Equal("keep existing", File.ReadAllText(existing));
    }
    [Fact]
    public async Task CancellationBetweenCasesPreservesFinishedCaseAndReportsCancellation()
    {
        using var fixture = new SyntheticInstalledGame(); using var cancellation = new CancellationTokenSource();
        var progress = new ImmediateProgress(_ => cancellation.Cancel());
        var receipt = await TemplateBatchService.GenerateAsync(new(Settings(), [11, 12, 13], fixture.Root), cancellation.Token, progress);
        Assert.True(receipt.Cancelled); Assert.Single(receipt.Cases); Assert.True(receipt.Cases[0].Success);
        Assert.Single(Directory.GetFiles(fixture.Root, "*.rmg.json"));
    }
    [Fact]
    public void CloneRetainsNestedProfileContentAndSeversAliases()
    {
        var source = Settings(); source.ProfileOptions.Alliances.Slots = [new AllianceSlotSettings { Player = 1, Alliance = 0 }];
        source.PlayerZoneMandatoryContent = [new ContentItem { Sid = "mine_wood", Rules = [new ContentPlacementRule { Type = "Range", Args = ["original"] }] }];
        var cloned = TemplateBatchService.CloneSettings(source);
        cloned.ProfileOptions.Alliances.Slots[0].Alliance = 7; cloned.PlayerZoneMandatoryContent[0].Rules![0].Args![0] = "changed";
        Assert.Equal(0, source.ProfileOptions.Alliances.Slots[0].Alliance);
        Assert.Equal("original", source.PlayerZoneMandatoryContent[0].Rules![0].Args![0]);
    }
    private sealed class ImmediateProgress(Action<TemplateBatchProgress> callback) : IProgress<TemplateBatchProgress>
    { public void Report(TemplateBatchProgress value) => callback(value); }
}
