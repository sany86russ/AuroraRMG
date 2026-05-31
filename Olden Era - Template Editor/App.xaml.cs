using System.Globalization;
using System.Windows;
using Olden_Era___Template_Editor.Services.GameData;
using Olden_Era___Template_Editor.Services.Localization;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Pick the UI language BEFORE the StartupUri window is created:
            // saved preference → else the Windows UI locale (ru → Russian, anything else → English).
            // Optional CLI override (used by --shoot verification): --lang ru|en
            string? cliLang = null;
            for (int i = 0; i < e.Args.Length - 1; i++)
                if (e.Args[i].Equals("--lang", System.StringComparison.OrdinalIgnoreCase))
                    cliLang = e.Args[i + 1].ToLowerInvariant();

            string saved = cliLang ?? AppSettings.Current.Language;
            AppLanguage lang = saved switch
            {
                "en" => AppLanguage.En,
                "ru" => AppLanguage.Ru,
                _    => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                            .Equals("ru", System.StringComparison.OrdinalIgnoreCase)
                        ? AppLanguage.Ru : AppLanguage.En,
            };
            LocalizationManager.Instance.Initialize(lang);

            base.OnStartup(e);
        }
    }
}
