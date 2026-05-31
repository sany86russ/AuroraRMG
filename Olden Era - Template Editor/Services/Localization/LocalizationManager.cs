using System;
using System.Collections.Generic;
using System.Windows;
using Olden_Era___Template_Editor.Localization;

namespace Olden_Era___Template_Editor.Services.Localization
{
    public enum AppLanguage { Ru, En }

    /// <summary>
    /// Runtime language switcher. Builds a <see cref="ResourceDictionary"/> from the active
    /// <see cref="Strings"/> table and swaps it into the app's merged dictionaries, so every
    /// <c>{DynamicResource S.X.Y}</c> in XAML updates live. Code-behind reads via <see cref="Get"/>.
    /// </summary>
    public sealed class LocalizationManager
    {
        public static LocalizationManager Instance { get; } = new();

        public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Ru;

        /// <summary>Raised after the language changes so code-driven text (combos, dialogs) can refresh.</summary>
        public event EventHandler? LanguageChanged;

        private ResourceDictionary? _merged;

        private static Dictionary<string, string> Table(AppLanguage lang) =>
            lang == AppLanguage.En ? Strings.En : Strings.Ru;

        /// <summary>Applies a language without firing the change event (use at startup).</summary>
        public void Initialize(AppLanguage lang) => Apply(lang);

        public void SetLanguage(AppLanguage lang)
        {
            if (lang == CurrentLanguage && _merged != null) return;
            Apply(lang);
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Toggle() => SetLanguage(CurrentLanguage == AppLanguage.Ru ? AppLanguage.En : AppLanguage.Ru);

        private void Apply(AppLanguage lang)
        {
            CurrentLanguage = lang;

            var dict = new ResourceDictionary();
            foreach (var kv in Table(lang))
                dict[kv.Key] = kv.Value;

            var app = Application.Current;
            if (app is null) { _merged = dict; return; }

            if (_merged != null)
                app.Resources.MergedDictionaries.Remove(_merged);
            app.Resources.MergedDictionaries.Add(dict);
            _merged = dict;
        }

        /// <summary>Localized string for code-behind, with optional <see cref="string.Format"/> args.</summary>
        public string Get(string key, params object[] args)
        {
            var table = Table(CurrentLanguage);
            string value = table.TryGetValue(key, out var s) ? s : key;
            return args is { Length: > 0 } ? string.Format(value, args) : value;
        }

        /// <summary>Static shortcut so any window/code-behind can localize without holding a reference.</summary>
        public static string T(string key, params object[] args) => Instance.Get(key, args);
    }
}
