using System.Linq;
using Olden_Era___Template_Editor.Localization;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor.Tests;

[Collection("Localization")]
public class LocalizationTests
{
    [Fact]
    public void EveryReferencedLiteralResource_ExistsInBothLanguages()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Olden Era - Template Editor.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var files = Directory.EnumerateFiles(Path.Combine(root!.FullName, "Olden Era - Template Editor"), "*", SearchOption.AllDirectories)
            .Where(p => (p.EndsWith(".xaml") || p.EndsWith(".cs")) && !p.Split(Path.DirectorySeparatorChar).Intersect(new[] { "obj", "bin" }).Any());
        foreach (string file in files)
        foreach (string line in File.ReadLines(file).Where(l => !l.TrimStart().StartsWith("//")))
        foreach (Match match in Regex.Matches(line, "(?:\"|DynamicResource\\s+)(S\\.[A-Za-z0-9_.]+)"))
        {
            string key = match.Groups[1].Value;
            if (key.EndsWith('.')) continue; // runtime prefix, covered by label/enum coverage tests
            Assert.True(Strings.Ru.ContainsKey(key) && Strings.En.ContainsKey(key), $"{Path.GetFileName(file)}: {key}");
        }
    }

    [Fact]
    public void FormatArguments_MatchAcrossLanguages()
    {
        foreach (var (key, ru) in Strings.Ru)
        {
            string en = Strings.En[key];
            if (!Regex.IsMatch(ru + en, @"\{\d")) continue;
            Assert.Equal(CompositeFormat.Parse(ru).MinimumArgumentCount, CompositeFormat.Parse(en).MinimumArgumentCount);
            Assert.Equal(Regex.Matches(ru, @"\{(\d+)").Select(m => m.Groups[1].Value).Order(),
                Regex.Matches(en, @"\{(\d+)").Select(m => m.Groups[1].Value).Order());
        }
    }

    [Fact]
    public void BuiltInNames_CategoriesAndConnections_HaveRussianLabels_WithoutChangingIds()
    {
        var manager = LocalizationManager.Instance; var previous = manager.CurrentLanguage;
        try
        {
            manager.SetLanguage(AppLanguage.Ru);
            foreach (var item in KnownValues.BannableItems)
            {
                var row = new BanEntry { Id = item.Id, DisplayName = item.DisplayName, Category = item.Category };
                Assert.Matches("[А-Яа-яЁё]", row.DisplayName);
                Assert.Matches("[А-Яа-яЁё]", row.CategoryLabel);
                Assert.Equal(item.Id, row.Id);
            }
            foreach (var spell in KnownValues.KnownSpells)
            {
                Assert.Matches("[А-Яа-яЁё]", GameLabels.Name(spell.Id));
                Assert.Matches("[А-Яа-яЁё]", GameLabels.Category(spell.School));
            }
            foreach (string type in KnownValues.ConnectionTypes) Assert.Matches("[А-Яа-яЁё]", GameLabels.Token(type));
            Assert.Contains("0,7", manager.Get("S.Label.Size", 0.7));
            manager.SetLanguage(AppLanguage.En);
            foreach (var spell in KnownValues.KnownSpells) Assert.Equal(spell.Name, GameLabels.Name(spell.Id));
            Assert.Contains("0.7", manager.Get("S.Label.Size", 0.7));
        }
        finally { manager.SetLanguage(previous); }
    }

    [Fact]
    public void EveryEditorCommand_HasBothLabels()
    {
        foreach (var action in Enum.GetValues<Olden_Era___Template_Editor.Services.EditorAction>())
        {
            var key = Olden_Era___Template_Editor.Services.EditorHotkeys.LabelKey(action);
            Assert.True(Strings.Ru.ContainsKey(key) && Strings.En.ContainsKey(key), key);
        }
    }

    [Fact]
    public void PresetNamesDescriptionsAndContentChoices_HaveEnglishTranslations()
    {
        foreach (var preset in Olden_Era___Template_Editor.Models.Presets.All)
        {
            Assert.True(Olden_Era___Template_Editor.Models.Presets.EnDesc.ContainsKey(preset.Name), preset.Name);
            Assert.DoesNotMatch("[А-Яа-яЁё]", preset.ShortNameLocalized(true));
            Assert.DoesNotMatch("[А-Яа-яЁё]", preset.DescriptionLocalized(true));
        }
        foreach (var content in OldenEraTemplateEditor.Services.ContentManagement.ContentIds.GetAll())
            Assert.DoesNotMatch("[А-Яа-яЁё]", OldenEraTemplateEditor.Services.ContentManagement.ContentNamesEn.Of(content, true));
        foreach (var text in Strings.En.Values) Assert.DoesNotMatch("[А-Яа-яЁё]", text);
    }
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

[CollectionDefinition("Localization", DisableParallelization = true)]
public class LocalizationCollection { }
