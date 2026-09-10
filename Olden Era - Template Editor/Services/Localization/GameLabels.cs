using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using OldenEraTemplateEditor.Models;
using OldenEraTemplateEditor.Services.ContentManagement;

namespace Olden_Era___Template_Editor.Services.Localization;

/// <summary>Presentation only: never substitute translated strings into game identifiers.</summary>
public static class GameLabels
{
    private static readonly IReadOnlyDictionary<string, string> English = KnownValues.BannableItems
        .Select(x => (x.Id, Name: x.DisplayName)).Concat(KnownValues.KnownSpells.Select(x => (x.Id, x.Name)))
        .GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().Name);
    private static readonly IReadOnlyDictionary<string, SidMapping> Content = ContentIds.GetAll()
        .GroupBy(x => x.Sid).ToDictionary(g => g.Key, g => g.First());

    public static string Name(string id, string? fallback = null)
    {
        bool en = LocalizationManager.Instance.CurrentLanguage == AppLanguage.En;
        if (en && English.TryGetValue(id, out var english)) return english;
        if (!en && BuiltInGameNames.Ru.TryGetValue(id, out var russian)) return russian;
        if (!en && id.EndsWith("_alt", StringComparison.Ordinal)
            && BuiltInGameNames.Ru.TryGetValue(id[..^4], out var basic)) return basic + " " + LocalizationManager.T("S.Label.Alternative");
        if (Content.TryGetValue(id, out var content)) return ContentNamesEn.Of(content, en);
        int hero = id.IndexOf("_hero_", StringComparison.Ordinal);
        if (hero > 0)
        {
            if (GameData.AppSettings.Current.UseGameAssets && GameData.GameCatalogService.Instance.TryResolveHero(id, out var name, out _, out _)) return name;
            return LocalizationManager.T("S.Label.Hero", Category(id[..hero]), id[(hero + 6)..]);
        }
        return fallback ?? id;
    }

    public static string Category(string value)
    {
        string key = "S.Category." + value.ToLowerInvariant();
        string text = LocalizationManager.T(key);
        return text == key ? value : text;
    }

    public static string Token(string value)
    {
        string key = "S.Token." + value.Replace(' ', '_');
        string text = LocalizationManager.T(key);
        return text == key ? value : text;
    }
}

public sealed class GameTokenConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null ? GameLabels.Token("Default") : value is string text ? GameLabels.Token(text) : value;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
