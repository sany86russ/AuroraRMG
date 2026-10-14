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
            if (e.Args.Any(a => a is "--shoot-patch" or "--shoot-docs" or "--make-patch-kit"))
            {
                System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
                string? requested = Environment.GetEnvironmentVariable("AURORARMG_DATA_DIRECTORY");
                string isolated = requested is { Length: > 0 } ? Path.GetFullPath(requested)
                    : Path.Combine(Path.GetTempPath(), "AuroraRMG-verification-" + Guid.NewGuid().ToString("N"));
                string leaf = Path.GetFileName(isolated.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!isolated.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) || !leaf.StartsWith("AuroraRMG-verification-", StringComparison.Ordinal)
                    || !Guid.TryParseExact(leaf["AuroraRMG-verification-".Length..], "N", out _))
                    throw new ArgumentException("Verification requires an isolated AuroraRMG-verification-<guid> data directory under the temporary folder.");
                if (Directory.Exists(isolated) && (File.GetAttributes(isolated) & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("Verification data cannot use a linked directory.");
                Directory.CreateDirectory(isolated);
                Environment.SetEnvironmentVariable("AURORARMG_DATA_DIRECTORY", isolated);
            }
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
