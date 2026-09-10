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
        private bool _running = true;
        private string _statusKey = "S.Upd.ConnGitHub";
        private double? _progress;

        private void RefreshLanguage()
        {
            TitleText.Text = Services.Localization.LocalizationManager.T("S.Upd.DownTitle", Format(_info.Version));
            StatusText.Text = _progress is { } p
                ? Services.Localization.LocalizationManager.T("S.Upd.Pct", (p * 100).ToString("0")) + (_info.AssetSize > 0 ? Services.Localization.LocalizationManager.T("S.Upd.OfMb", Mb(_info.AssetSize * p), Mb(_info.AssetSize)) : "")
                : Services.Localization.LocalizationManager.T(_statusKey);
        }

        public UpdateProgressWindow(UpdateInfo info)
        {
            InitializeComponent();
            _info = info;
            RefreshLanguage();
            Services.Localization.LocalizationManager.Observe(this, RefreshLanguage);
            Loaded += async (_, _) => await RunAsync();
        }

        private async Task RunAsync()
        {
            var progress = new Progress<double>(p =>
            {
                if (p < 0)
                {
                    ProgressBar.IsIndeterminate = true;
                    _statusKey = "S.Upd.Loading"; _progress = null; RefreshLanguage();
                }
                else
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = p * 100;
                    _progress = p; RefreshLanguage();
                }
            });

            try
            {
                _statusKey = "S.Upd.ConnGitHub"; _progress = null; RefreshLanguage();
                string file = await UpdateService.DownloadAsync(_info, progress, _cts.Token);
                _cts.Token.ThrowIfCancellationRequested();

                _statusKey = "S.Upd.Preparing"; _progress = null; RefreshLanguage();
                _installing = true;
                UpdateService.InstallAndRestart(file);

                _completed = true;
                _running = false;
                DialogResult = true;   // caller shuts the app down → helper swaps the exe
            }
            catch (OperationCanceledException)
            {
                _running = false;
                DialogResult = false;
            }
            catch (Exception ex)
            {
                _installing = false;
                _running = false;
                ProgressBar.IsIndeterminate = false;
                MessageBox.Show(this,
                    Services.Localization.LocalizationManager.T("S.Upd.Failed", ex.Message),
                    Services.Localization.LocalizationManager.T("S.Upd.001"), MessageBoxButton.OK, MessageBoxImage.Warning);
                DialogResult = false;
            }
            finally { _cts.Dispose(); }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_completed || _installing || !_running) return;
            _cts.Cancel();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Block closing while the installer is being launched.
            if (_installing && !_completed) { e.Cancel = true; return; }
            if (_running)
            {
                e.Cancel = true;
                _cts.Cancel(); // RunAsync closes the dialog after download cleanup finishes.
            }
        }

        private static string Format(Version v)
            => v.Build > 0 ? $"v{v.Major}.{v.Minor}.{v.Build}" : $"v{v.Major}.{v.Minor}";

        private static string Mb(double bytes) => (bytes / 1024d / 1024d).ToString("0.0", Services.Localization.LocalizationManager.Instance.Culture);
    }
}
