using System.Linq;
using Olden_Era___Template_Editor.Localization;

namespace Olden_Era___Template_Editor.Tests;

public class LocalizationTests
{
    [Fact]
    public void Ru_And_En_HaveIdenticalKeySets()
    {
        var ruOnly = Strings.Ru.Keys.Except(Strings.En.Keys).OrderBy(k => k).ToList();
        var enOnly = Strings.En.Keys.Except(Strings.Ru.Keys).OrderBy(k => k).ToList();
        Assert.True(ruOnly.Count == 0, "Keys missing from EN: " + string.Join(", ", ruOnly));
        Assert.True(enOnly.Count == 0, "Keys missing from RU: " + string.Join(", ", enOnly));
    }

    [Fact]
    public void NoTranslationIsEmpty()
    {
        Assert.DoesNotContain(Strings.Ru, kv => string.IsNullOrWhiteSpace(kv.Value));
        Assert.DoesNotContain(Strings.En, kv => string.IsNullOrWhiteSpace(kv.Value));
    }

    [Fact]
    public void EveryKeyHasBothLanguages()
    {
        Assert.NotEmpty(Strings.Ru);
        foreach (var key in Strings.Ru.Keys)
            Assert.True(Strings.En.ContainsKey(key), $"EN missing key: {key}");
    }
}
