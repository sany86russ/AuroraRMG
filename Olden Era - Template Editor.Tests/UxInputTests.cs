using Olden_Era___Template_Editor.Services;
using Olden_Era___Template_Editor.Models;

namespace Olden_Era___Template_Editor.Tests;

public class UxInputTests
{
    [Theory]
    [InlineData("0,7", true)]
    [InlineData("NaN", false)]
    [InlineData("0", false)]
    [InlineData("-2", false)]
    public void ArmyMultiplier_ValidatesInputAndExportsInvariantDecimal(string text, bool expected)
    {
        var bonus = new OldenEraTemplateEditor.Models.BonusEntry { PresetType = OldenEraTemplateEditor.Models.BonusPresetType.UnitMultiplier, Param = text };
        Assert.Equal(expected, bonus.HasValidParameters());
        if (expected) Assert.Equal("0.7", Assert.Single(bonus.ToBonuses()).Parameters![0]);
    }
    [Theory]
    [InlineData("0,7", 0.7)]
    [InlineData("0.7", 0.7)]
    [InlineData("-15", -15)]
    [InlineData("1e2", 100)]
    public void DecimalInput_DoesNotInterpretCommaAsThousands(string text, double expected)
    {
        Assert.True(NumericInput.TryOptionalDouble(text, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1,2.3")]
    [InlineData("1e999")]
    [InlineData("invalid")]
    public void DecimalInput_RejectsMalformedAndNonFiniteNumbers(string text)
        => Assert.False(NumericInput.TryOptionalDouble(text, out _));

    [Fact]
    public void EmptyNumericFields_AreOptional_ButFractionalIntegersAreRejected()
    {
        Assert.True(NumericInput.TryOptionalDouble(" ", out var d)); Assert.Null(d);
        Assert.True(NumericInput.TryOptionalInt(" ", out var i)); Assert.Null(i);
        Assert.False(NumericInput.TryOptionalInt("1.5", out _));
        Assert.False(NumericInput.TryOptionalInt("NaN", out _));
    }

    [Fact]
    public void Selection_RemainsOnModelAfterFiltering()
    {
        var choices = new[] { new SelectableSid("first"), new SelectableSid("second") };
        choices[0].IsSelected = true;
        var filtered = choices.Where(c => c.Id.Contains("second")).ToArray();
        filtered[0].IsSelected = true;
        Assert.All(choices, c => Assert.True(c.IsSelected));
    }
}
