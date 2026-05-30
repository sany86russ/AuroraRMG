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
            TitleText.Text = $"Загрузка AuroraRMG {Format(info.Version)}";
            Loaded += async (_, _) => await RunAsync();
        }

        private async Task RunAsync()
        {
            var progress = new Progress<double>(p =>
            {
                if (p < 0)
                {
                    ProgressBar.IsIndeterminate = true;
                    StatusText.Text = "Загрузка…";
                }
                else
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = p * 100;
                    StatusText.Text = $"Загружено {p * 100:0}%"
                        + (_info.AssetSize > 0 ? $"  ({Mb(_info.AssetSize * p)} из {Mb(_info.AssetSize)} МБ)" : "");
                }
            });

            try
            {
                StatusText.Text = "Подключение к GitHub…";
                string file = await UpdateService.DownloadAsync(_info, progress, _cts.Token);

                StatusText.Text = "Подготовка к установке…";
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
                    $"Не удалось загрузить обновление:\n\n{ex.Message}\n\n" +
                    "Можно скачать новую версию вручную со страницы релизов на GitHub.",
                    "Обновление AuroraRMG", MessageBoxButton.OK, MessageBoxImage.Warning);
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
