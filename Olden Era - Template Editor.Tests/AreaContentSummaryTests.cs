using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services.Analysis;

namespace Olden_Era___Template_Editor.Tests;

public class AreaContentSummaryTests
{
    [Fact]
    public void AreaCoefficientsAreReportedSeparatelyWithoutInventingAreaOrBudget()
    {
        var template = new RmgTemplate { Variants = [new() { Zones = [new() { Name = "a", GuardedContentValuePerArea = 2200, UnguardedContentValuePerArea = 490, ResourcesValuePerArea = 400 }] }] };
        var result = TemplateContentSummary.Analyze(template);
        Assert.True(result.HasAreaBudgets);
        Assert.Equal(0, result.TotalTreasure);
        Assert.Equal(2690, result.TotalTreasureAreaCoefficient);
        Assert.Equal(400, result.TotalResourcesAreaCoefficient);
    }

    [Fact]
    public void LargeImportedAbsoluteBudgetsCannotWrapNegative()
    {
        var template = new RmgTemplate { Variants = [new() { Zones = [new() { Name = "a", GuardedContentValue = int.MaxValue, UnguardedContentValue = int.MaxValue }] }] };
        Assert.Equal(2L * int.MaxValue, TemplateContentSummary.Analyze(template).TotalTreasure);
    }
}
