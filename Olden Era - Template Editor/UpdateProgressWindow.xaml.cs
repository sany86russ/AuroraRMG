using System.ComponentModel;
using System.Windows;
using Olden_Era___Template_Editor.Services.Update;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Modal progress window that downloads a pending <see cref="UpdateInfo"/>
    /// and, on success, hands control to <see cref="UpdateService.InstallAndRestart"/>.
    ///
    /// <see cref="ShowDialog"/> returns <c>true</c> once the download finished
    /// and the install helper was launched — the caller should then shut the
    /// application down so the new build can replace it.
    /// </summary>
    public partial class UpdateProgressWindow : Window
    {
        private readonly UpdateInfo _info;
        private readonly CancellationTokenSource _cts = new();
        private bool _completed;
        private bool _installing;

        public UpdateProgressWindow(UpdateInfo info)
        {
            InitializeComponent();
            _info = info;
            TitleText.Text = Services.Localization.LocalizationManager.T("S.Upd.DownTitle", Format(info.Version));
            Loaded += async (_, _) => await RunAsync();
        }

        private async Task RunAsync()
        {
            var progress = new Progress<double>(p =>
            {
                if (p < 0)
                {
                    ProgressBar.IsIndeterminate = true;
                    StatusText.Text = Services.Localization.LocalizationManager.T("S.Upd.Loading");
                }
                else
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = p * 100;
                    StatusText.Text = Services.Localization.LocalizationManager.T("S.Upd.Pct", (p * 100).ToString("0"))
                        + (_info.AssetSize > 0 ? Services.Localization.LocalizationManager.T("S.Upd.OfMb", Mb(_info.AssetSize * p), Mb(_info.AssetSize)) : "");
                }
            });

            try
            {
                StatusText.Text = Services.Localization.LocalizationManager.T("S.Upd.ConnGitHub");
                string file = await UpdateService.DownloadAsync(_info, progress, _cts.Token);

                StatusText.Text = Services.Localization.LocalizationManager.T("S.Upd.Preparing");
                _installing = true;
                UpdateService.InstallAndRestart(file);

                _completed = true;
                DialogResult = true;   // caller shuts the app down → helper swaps the exe
            }
            catch (OperationCanceledException)
            {
                DialogResult = false;
            }
            catch (Exception ex)
            {
                _installing = false;
                ProgressBar.IsIndeterminate = false;
                MessageBox.Show(this,
                    Services.Localization.LocalizationManager.T("S.Upd.Failed", ex.Message),
                    Services.Localization.LocalizationManager.T("S.Upd.001"), MessageBoxButton.OK, MessageBoxImage.Warning);
                DialogResult = false;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_completed || _installing) return;
            _cts.Cancel();
            DialogResult = false;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Block closing while the installer is being launched.
            if (_installing && !_completed) { e.Cancel = true; return; }
            if (!_completed) _cts.Cancel();
        }

        private static string Format(Version v)
            => v.Build > 0 ? $"v{v.Major}.{v.Minor}.{v.Build}" : $"v{v.Major}.{v.Minor}";

        private static string Mb(double bytes) => (bytes / 1024d / 1024d).ToString("0.0");
    }
}
