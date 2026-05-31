using System.Diagnostics;
using System.Windows;
using Olden_Era___Template_Editor.Services.Update;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Auto-update wiring for the main window: a quiet startup check against the
    /// public GitHub repository, plus the banner shown when a newer release is
    /// available ("notify + button" mode).
    /// </summary>
    public partial class MainWindow
    {
        private UpdateInfo? _pendingUpdate;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Skip the network check on a quiet/minimized launch (e.g. started
            // from a shortcut while gaming) — same flags as the constructor.
            bool startMinimized = Environment.GetCommandLineArgs().Any(a =>
                a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-m", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/min", StringComparison.OrdinalIgnoreCase));
            if (startMinimized) return;

            // Fire-and-forget: a failed check must never block or crash startup.
            _ = CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            UpdateInfo? info;
            try { info = await UpdateService.CheckForUpdateAsync(); }
            catch { info = null; }
            if (info is null) return;

            _pendingUpdate = info;
            ShowUpdateBanner(info);
        }

        private void ShowUpdateBanner(UpdateInfo info)
        {
            string newLabel = FormatVersion(info.Version);
            string curLabel = FormatVersion(UpdateService.CurrentVersion);
            TxtUpdateBanner.Text = L.Get("S.Upd.Available", newLabel, curLabel);
            BtnUpdateNotes.Visibility = string.IsNullOrWhiteSpace(info.ReleaseNotes)
                ? Visibility.Collapsed
                : Visibility.Visible;
            UpdateBanner.Visibility = Visibility.Visible;
        }

        private void BtnUpdateNow_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdate is null) return;

            var dlg = new UpdateProgressWindow(_pendingUpdate) { Owner = this };
            bool launched = dlg.ShowDialog() == true;
            if (launched)
            {
                // The helper is waiting for us to exit before swapping the exe.
                Application.Current.Shutdown();
            }
        }

        private void BtnUpdateNotes_Click(object sender, RoutedEventArgs e)
        {
            string url = _pendingUpdate?.ReleaseUrl
                         ?? $"https://github.com/{UpdateService.Owner}/{UpdateService.Repo}/releases/latest";
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show(this, url, L.Get("S.Upd.RelPage"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnUpdateDismiss_Click(object sender, RoutedEventArgs e)
        {
            UpdateBanner.Visibility = Visibility.Collapsed;
        }
    }
}
