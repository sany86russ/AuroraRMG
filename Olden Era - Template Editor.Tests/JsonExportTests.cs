using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;
using System.Text.Json;
using Xunit;

namespace Olden_Era___Template_Editor.Tests;

public class JsonExportTests
{
    // Regression for the "/xxx breaks the template" report: System.Text.Json was escaping
    // Cyrillic to \uXXXX. The shared export options must emit literal UTF-8 instead.
    [Fact]
    public void Options_WriteCyrillicAsLiteralUtf8_NotUnicodeEscapes()
    {
        var settings = new GeneratorSettings { TemplateName = "1v1 Дуэль (тест)", PlayerCount = 2, MapSize = 96 };
        RmgTemplate template = TemplateGenerator.Generate(settings);

        string json = JsonSerializer.Serialize(template, JsonExport.Options);

        Assert.Contains("Дуэль", json);       // literal Cyrillic present in the output
        Assert.DoesNotContain("\\u0", json);   // and NO \u0XXX escapes (the reported "/xxx" junk)
    }

    [Fact]
    public void Generate_NewFields_HeroHireBanAndHeroLighting_FlowThrough()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "T",
            HeroSettings = new HeroSettings { HeroCountMin = 4, HeroCountMax = 8, HeroCountIncrement = 1, HeroHireBan = true },
            GameEndConditions = new GameEndConditions { HeroLighting = true, HeroLightingDay = 5 },
        };

        RmgTemplate t = TemplateGenerator.Generate(settings);

        Assert.True(t.GameRules?.HeroHireBan);
        Assert.True(t.GameRules?.WinConditions?.HeroLighting);
        Assert.Equal(5, t.GameRules?.WinConditions?.HeroLightingDay);
    }

    [Fact]
    public void Generate_HeroLightingOff_StillWritesDayOne()
    {
        var settings = new GeneratorSettings
        {
            TemplateName = "T",
            GameEndConditions = new GameEndConditions { HeroLighting = false, HeroLightingDay = 9 },
        };

        RmgTemplate t = TemplateGenerator.Generate(settings);

        Assert.False(t.GameRules?.WinConditions?.HeroLighting);
        Assert.Equal(1, t.GameRules?.WinConditions?.HeroLightingDay);
    }
}
